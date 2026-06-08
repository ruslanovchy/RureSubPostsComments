using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace RureSubPostsComments.Services;

public class LikesService : ILikesService
{
    private readonly IConnectionMultiplexer redis;

    public LikesService(
        [FromServices] IConnectionMultiplexer redis)
    {
        this.redis = redis;
    }

    public async Task<bool[]> IsCommentsLiked(Guid userId, Guid[] commentsIds)
    {
        var db = redis.GetDatabase();

        var userRedisId = await db.StringGetAsync($"user:id:{userId}");

        if (string.IsNullOrEmpty(userRedisId))
        {
            var result = new bool[commentsIds.Length];
            Array.Fill(result, false);
            return result;
        }

        var key = $"user:{userRedisId}:liked_comments";

        RedisValue[] fields = [.. commentsIds.Select(i => (RedisValue)i.ToString())];

        RedisValue[] values = await db.HashGetAsync(key, fields);

        return [.. values.Select(v => !v.IsNull)];
    }
}
