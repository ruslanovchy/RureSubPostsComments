using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RureSubPostsComments.Models.Dtos;

public class GetCommentDto
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid PostId { get; set; }

    public Guid? RootCommentId { get; set; }

    public Guid AuthorId { get; set; }
    public AuthorDocument? Author { get; set; }

    public ReplyToSelectionDto? ReplyToSelection { get; set; }

    public object? Content { get; set; }
    public bool IsLiked { get; set; }
    public int LikesCount { get; set; }
    public int RepliesCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
