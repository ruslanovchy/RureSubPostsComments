namespace RureSubPostsComments.Services;

public interface ILikesService
{
    Task<bool[]> IsCommentsLiked(Guid userId, Guid[] commentsIds);
}