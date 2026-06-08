using Confluent.Kafka;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using RureSubPostsComments.Models;
using RureSubPostsComments.Services;
using StackExchange.Redis;
using System.Security.Claims;
using System.Text.Json;

namespace RureSubPostsComments.Controllers;

[ApiController]
[Route("/like")]
public class LikesController : Controller
{
    private IProducer<string, string> producer;

    public LikesController(ProducerConfig config)
    {
        producer = new ProducerBuilder<string, string>(config).Build();
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Like(
        [FromServices] IConnectionMultiplexer redis,
        [FromServices] IMongoDbService mongoDb,
        [FromQuery] Guid? commentId)
    {
        if (commentId == null || commentId == Guid.Empty)
        {
            return BadRequest();
        }

        var userIdRaw = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        if (userIdRaw == null || string.IsNullOrEmpty(userIdRaw.Value) || !Guid.TryParse(userIdRaw.Value, out var userId))
        {
            return Unauthorized();
        }

        var db = redis.GetDatabase();

        string? userRedisId = await db.StringGetAsync($"user:id:{userId}");

        if (string.IsNullOrEmpty(userRedisId))
        {
            return NotFound();
        }

        var filter = Builders<CommentDocument>.Filter.Eq(c => c.Id, commentId);

        var update = Builders<CommentDocument>.Update.Inc(c => c.LikesCount, 1);

        await mongoDb.Comments.UpdateOneAsync(filter, update);

        bool changed = await db.SetAddAsync($"comments:{commentId}:likes", userRedisId);
        await db.HashSetAsync($"user:{userRedisId}:liked_comments", commentId.ToString(), DateTime.UtcNow.ToString());
        await db.SortedSetAddAsync($"user:{userRedisId}:liked_comments_sorted", commentId.ToString(), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        if (changed)
        {
            await producer.ProduceAsync("comment-liked", new Message<string, string>
            {
                Key = Guid.CreateVersion7().ToString(),
                Value = JsonSerializer.Serialize(new
                {
                    CommentId = commentId,
                    Value = 1
                })
            });
        }

        return Ok();
    }

    [HttpDelete]
    [Authorize]
    public async Task<IActionResult> UnLike(
        [FromServices] IConnectionMultiplexer redis,
        [FromServices] IMongoDbService mongoDb,
        [FromQuery] Guid? commentId)
    {
        if (commentId == null || commentId == Guid.Empty)
        {
            return BadRequest();
        }

        var userIdRaw = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        if (userIdRaw == null || string.IsNullOrEmpty(userIdRaw.Value) || !Guid.TryParse(userIdRaw.Value, out var userId))
        {
            return Unauthorized();
        }

        var db = redis.GetDatabase();

        string? userRedisId = await db.StringGetAsync($"user:id:{userId}");

        if (string.IsNullOrEmpty(userRedisId) || string.IsNullOrEmpty(userRedisId))
        {
            return NotFound();
        }

        var filter = Builders<CommentDocument>.Filter.Eq(c => c.Id, commentId);

        var update = Builders<CommentDocument>.Update.Inc(c => c.LikesCount, -1);

        await mongoDb.Comments.UpdateOneAsync(filter, update);

        bool wasPresent = await db.SetRemoveAsync($"comments:{commentId}:likes", userRedisId);
        await db.HashDeleteAsync($"user:{userRedisId}:liked_comments", commentId.ToString());
        await db.SortedSetRemoveAsync($"user:{userRedisId}:liked_comments_sorted", commentId.ToString());

        if (wasPresent)
        {
            await producer.ProduceAsync("comment-liked", new Message<string, string>
            {
                Key = Guid.CreateVersion7().ToString(),
                Value = JsonSerializer.Serialize(new
                {
                    CommentId = commentId,
                    Value = -1
                })
            });
        }

        return Ok();
    }
}
