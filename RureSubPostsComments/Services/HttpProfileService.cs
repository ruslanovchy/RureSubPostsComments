using RureSubPostsComments.Models.Dtos;

namespace RureSubPostsComments.Services;

public class HttpProfileService(HttpClient client) : IProfileService
{
    private readonly HttpClient client = client;

    public async Task<GetProfileDto?> GetProfile(Guid id)
    {
        var response = await client.GetAsync($"profile?id={id}");

        if (response.StatusCode != System.Net.HttpStatusCode.OK)
        {
            return null;
        }

        var json = await response.Content.ReadFromJsonAsync<GetProfileDto>();

        return json;
    }
}
