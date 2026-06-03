namespace RureSubPostsComments.Models.Dtos;

public class GetProfileDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    public string UserName { get; set; } = null!;
    public string DisplayName { get; set; } = null!;

    public string? AvatarUrl { get; set; }

    public bool IsVerified { get; set; } = false;
}
