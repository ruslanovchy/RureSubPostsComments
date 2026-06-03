using System.ComponentModel.DataAnnotations;

namespace RureSubPostsComments.Models.Dtos;

public class CreateCommentDto
{
    [Required]
    public Guid PostId { get; set; }
    [Required]
    public string? Content { get; set; }

    public Guid? RootCommentId { get; set; }
    public Guid? ReplyCommentAuthorId { get; set; }
}
