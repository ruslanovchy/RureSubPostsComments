namespace RureSubPostsComments.Models.Dtos;

public class ProfileCreatedWorkerDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public long RedisId { get; set; }
}
