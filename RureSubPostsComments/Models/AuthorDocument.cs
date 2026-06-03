using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RureSubPostsComments.Models;

public class AuthorDocument
{
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid UserId { get; set; }

    public string UserName { get; set; } = null!;
    public string DisplayName { get; set; } = null!;

    public string? AvatarUrl { get; set; }

    public bool IsVerified { get; set; } = false;
}
