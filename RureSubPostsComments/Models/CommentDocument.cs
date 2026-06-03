using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RureSubPostsComments.Models;

public class CommentDocument
{
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid PostId { get; set; }

    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid? RootCommentId { get; set; }

    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid AuthorId { get; set; }
    public AuthorDocument? Author { get; set; }

    public ReplyToSelectionDto? ReplyToSelection { get; set; }

    public BsonDocument? Content { get; set; }
    public int LikesCount { get; set; }
    public int RepliesCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
