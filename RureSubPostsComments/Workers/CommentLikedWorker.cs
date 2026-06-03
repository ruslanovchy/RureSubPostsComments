using Confluent.Kafka;
using MongoDB.Driver;
using RureSubPostsComments.Models;
using RureSubPostsComments.Models.Dtos;
using RureSubPostsComments.Services;
using System.Text.Json;

namespace RureSubPostsComments.Workers;

public class CommentLikedWorker : BackgroundService
{
    private readonly ConsumerConfig config;
    private readonly IMongoDbService mongoDb;
    private readonly ILogger<CommentLikedWorker> logger;

    public CommentLikedWorker(ConsumerConfig config, IMongoDbService mongoDb, ILogger<CommentLikedWorker> logger)
    {
        this.config = config;
        this.mongoDb = mongoDb;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = new ConsumerBuilder<string, string>(config).Build();

        consumer.Subscribe("comment-liked");

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

            try
            {
                await mongoDb.InboxMessages.InsertOneAsync(new InboxMessage
                {
                    Topic = "comment-liked",
                    MessageId = messageId,
                    ProcessedAt = DateTime.UtcNow
                }, cancellationToken: stoppingToken);
            }
            catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                consumer.Commit(result);
                continue;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occured while processing kafka event!");
                continue;
            }

            var dto = JsonSerializer.Deserialize<CounterChangeDto>(result.Message.Value);

            if (dto == null || dto.CommentId == Guid.Empty || dto.Value == 0)
            {
                consumer.Commit(result);
                continue;
            }

            var filter = Builders<CommentDocument>.Filter.Eq(c => c.Id, dto.CommentId);

            var update = Builders<CommentDocument>.Update.Inc(c => c.LikesCount, dto.Value);

            try
            {
                await mongoDb.Comments.UpdateOneAsync(filter, update, cancellationToken: stoppingToken);
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
