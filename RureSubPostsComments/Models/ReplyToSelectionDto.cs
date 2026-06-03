using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RureSubPostsComments.Models;

public class ReplyToSelectionDto
{
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid UserId { get; set; }
    public string? UserDisplayName { get; set; }
}
