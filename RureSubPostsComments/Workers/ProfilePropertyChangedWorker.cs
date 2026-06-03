using Confluent.Kafka;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using RureSubPostsComments.Models;
using RureSubPostsComments.Models.Dtos;
using RureSubPostsComments.Services;
using System.Text.Json;

namespace RureSubPostsComments.Workers;

public class ProfilePropertyChangedWorker : BackgroundService
{
    private readonly IMongoDbService mongoService;
    private readonly ConsumerConfig config;
    private readonly ILogger<ProfilePropertyChangedWorker> logger;

    public ProfilePropertyChangedWorker(
        [FromServices] IMongoDbService mongoService,
        [FromServices] ConsumerConfig config,
        [FromServices] ILogger<ProfilePropertyChangedWorker> logger)
    {
        this.mongoService = mongoService;
        this.config = config;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var consumer = new ConsumerBuilder<string, string>(config).Build();

        consumer.Subscribe(["profile-display-name-changed", "profile-avatar-changed"]);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);

                if (result == null || result.Message == null || result.Message.Value == null)
                {
                    consumer.Commit(result);
                    continue;
                }

                //var messageIdRaw = result.Message.Key;
                //if (string.IsNullOrEmpty(messageIdRaw) || !Guid.TryParse(messageIdRaw, out var messageId))
                //{
                //    logger.LogError("Message key, that should represents inbox message id is null or empty!");
                //    consumer.Commit(result);
                //    continue;
                //}

                var dto = JsonSerializer.Deserialize<ProfilePropertyChangedDto>(result.Message.Value);

                if (dto == null)
                {
                    logger.LogError("Message value is null or empty!");
                    consumer.Commit(result);
                    continue;
                }

                //var inboxMessage = new InboxMessage
                //{
                //    MessageId = messageId,
                //    Topic = dto.PropertyName == "DisplayName" ? "profile-display-name-changed" : "profile-avatar-changed",
                //    ProcessedAt = DateTime.UtcNow
                //};

                //try
                //{
                //    await mongoService.InboxMessages.InsertOneAsync(inboxMessage, cancellationToken: stoppingToken);
                //}
                //catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
                //{
                //    consumer.Commit(result);
                //    continue;
                //}

                var filter = Builders<CommentDocument>.Filter.And(
                    Builders<CommentDocument>.Filter.Eq(p => p.AuthorId, dto.UserId),
                    Builders<CommentDocument>.Filter.Ne(p => p.Author, null)
                );

                var update = dto.PropertyName switch
                {
                    "DisplayName" => Builders<CommentDocument>.Update.Set(p => p.Author!.DisplayName, dto.Value),
                    "AvatarUrl" or _ => Builders<CommentDocument>.Update.Set(p => p.Author!.AvatarUrl, dto.Value)
                };

                await mongoService.Comments.UpdateManyAsync(filter, update, cancellationToken: stoppingToken);
                consumer.Commit(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while processing kafka message!");
                continue;
            }
        }
    }
}
