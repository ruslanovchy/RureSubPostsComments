using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RureSubPostsComments.Models;

public class InboxMessage
{
    [BsonId]
    public ObjectId InternalId { get; set; }
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid MessageId { get; set; }
    public string Topic { get; set; } = null!;
    public string Content { get; set; } = null!;
    public DateTime ProcessedAt { get; set; }
}
