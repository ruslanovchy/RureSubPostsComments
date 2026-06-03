using RureSubPostsComments.Models.Dtos;

namespace RureSubPostsComments.Services;

public interface IProfileService
{
    Task<GetProfileDto?> GetProfile(Guid id);
}