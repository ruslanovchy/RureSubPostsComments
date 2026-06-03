using MongoDB.Driver;
using RureSubPostsComments.Models;

namespace RureSubPostsComments.Services;

public class MongoDbInitializer(IMongoDbService mongoDbService) : IHostedService
{
    private readonly IMongoDbService mongoDbService = mongoDbService;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var inboxMessageIndexKeys = Builders<InboxMessage>.IndexKeys.Ascending(m => m.MessageId);
        var inboxMessageIndexOptions = new CreateIndexOptions
        {
            Unique = true,
            ExpireAfter = TimeSpan.FromDays(7)
        };

        var inboxMessageModel = new CreateIndexModel<InboxMessage>(inboxMessageIndexKeys, inboxMessageIndexOptions);

        await mongoDbService.InboxMessages.Indexes.CreateOneAsync(inboxMessageModel, cancellationToken: cancellationToken);

        var postIndexes = new List<CreateIndexModel<CommentDocument>>
        {
            new(Builders<CommentDocument>.IndexKeys.Ascending(d => d.CreatedAt).Ascending(d => d.Id)),
            new(Builders<CommentDocument>.IndexKeys.Ascending(d => d.AuthorId).Ascending(d => d.Id)),
        };

        await mongoDbService.Comments.Indexes.CreateManyAsync(postIndexes, cancellationToken: cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
