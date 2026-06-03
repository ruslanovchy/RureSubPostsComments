using Confluent.Kafka;
using MongoDB.Driver;
using RureSubPostsComments.Models;
using RureSubPostsComments.Models.Dtos;
using RureSubPostsComments.Services;
using System.Text.Json;

namespace RureSubPostsComments.Workers;

public class PostDeletedWorker : BackgroundService
{
    private readonly ConsumerConfig config;
    private readonly IMongoDbService mongoDb;
    private readonly ILogger<CommentLikedWorker> logger;

    public PostDeletedWorker(ConsumerConfig config, IMongoDbService mongoDb, ILogger<CommentLikedWorker> logger)
    {
        this.config = config;
        this.mongoDb = mongoDb;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = new ConsumerBuilder<string, string>(config).Build();

        consumer.Subscribe("post-deleted");

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
                    Topic = "post-deleted",
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

            var dto = JsonSerializer.Deserialize<DeletePostDto>(result.Message.Value);

            if (dto == null || dto.Id == Guid.Empty)
            {
                consumer.Commit(result);
                continue;
            }

            var filter = Builders<CommentDocument>.Filter.Eq(c => c.PostId, dto.Id);

            try
            {
                await mongoDb.Comments.DeleteManyAsync(filter, cancellationToken: stoppingToken);
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
