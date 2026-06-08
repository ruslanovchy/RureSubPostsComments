using Confluent.Kafka;
using MongoDB.Driver;
using RureSubPostsComments.Models;
using RureSubPostsComments.Models.Dtos;
using RureSubPostsComments.Services;
using StackExchange.Redis;
using System.Text.Json;

namespace RureSubPostsComments.Workers;

public class ProfileCreatedWorker : BackgroundService
{
    private readonly ConsumerConfig config;
    private readonly IConnectionMultiplexer redis;
    private readonly ILogger<ProfileCreatedWorker> logger;

    public ProfileCreatedWorker(
        ConsumerConfig config, 
        IConnectionMultiplexer redis,
        ILogger<ProfileCreatedWorker> logger)
    {
        this.config = config;
        this.redis = redis;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = new ConsumerBuilder<string, string>(config).Build();

        consumer.Subscribe("profile-created");

        while (!stoppingToken.IsCancellationRequested)
        {
            var result = consumer.Consume(stoppingToken);

            if (result.Message == null || result.Message.Value == null)
            {
                consumer.Commit(result);
                continue;
            }

            var messageIdRaw = result.Message.Key;

            if (string.IsNullOrEmpty(messageIdRaw) || !Guid.TryParse(messageIdRaw, out var messageId))
            {
                consumer.Commit(result);
                continue;
            }

            var redisDb = redis.GetDatabase();

            var dto = JsonSerializer.Deserialize<ProfileCreatedWorkerDto>(result.Message.Value);

            if (dto == null || dto.UserId == Guid.Empty || dto.RedisId == 0)
            {
                consumer.Commit(result);
                continue;
            }

            try
            {
                await redisDb.StringSetAsync($"user:id:{dto.UserId}", dto.RedisId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occured while processing kafka event!");
                continue;
            }

            consumer.Commit(result);
        }
    }
}
