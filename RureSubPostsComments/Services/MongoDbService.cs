using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RureSubPostsComments.Models;

namespace RureSubPostsComments.Services;

public class MongoDbService : IMongoDbService
{
    private readonly IMongoClient client;
    private readonly IMongoCollection<CommentDocument> comments;
    public IMongoCollection<CommentDocument> Comments => comments;
    private readonly IMongoCollection<InboxMessage> inboxMessages;
    public IMongoCollection<InboxMessage> InboxMessages => inboxMessages;

    public MongoDbService(
        IOptions<MongoDbSettings> settings,
        IMongoClient client)
    {
        this.client = client;

        var database = client.GetDatabase(settings.Value.DatabaseName);

        comments = database.GetCollection<CommentDocument>("comments");
        inboxMessages = database.GetCollection<InboxMessage>("inbox_messages");
    }
}
