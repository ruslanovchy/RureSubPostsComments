namespace RureSubPostsComments.Models.Dtos;

public class ProfilePropertyChangedDto
{
    public Guid ProfileId { get; set; }
    public Guid UserId { get; set; }

    public string? PropertyName { get; set; }
    public string? Value { get; set; }
}
