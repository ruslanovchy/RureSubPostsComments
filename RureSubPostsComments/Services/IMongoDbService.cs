using MongoDB.Driver;
using RureSubPostsComments.Models;

namespace RureSubPostsComments.Services;

public interface IMongoDbService
{
    IMongoCollection<CommentDocument> Comments { get; }
    IMongoCollection<InboxMessage> InboxMessages { get; }
}