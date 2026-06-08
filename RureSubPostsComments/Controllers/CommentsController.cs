using Confluent.Kafka;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using RureSubPostsComments.Models;
using RureSubPostsComments.Models.Dtos;
using RureSubPostsComments.Services;
using StackExchange.Redis;
using System.Security.Claims;
using System.Text.Json;

namespace RureSubPostsComments.Controllers;

[ApiController]
[Route("/")]
public class CommentsController : Controller
{
    private IProducer<string, string> producer;

    public CommentsController(ProducerConfig producerConfig)
    {
        producer = new ProducerBuilder<string, string>(producerConfig).Build();
    }

    [HttpPost]
    public async Task<IActionResult> PostComment(
        [FromServices] IMongoDbService db,
        [FromServices] IProfileService profileApiClient,
        [FromForm] CreateCommentDto dto)
    {
        if (!ModelState.IsValid || string.IsNullOrEmpty(dto.Content))
        {
            return BadRequest();
        }

        var userIdRaw = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        if (userIdRaw == null || string.IsNullOrEmpty(userIdRaw.Value) || !Guid.TryParse(userIdRaw.Value, out var userId))
        {
            return Unauthorized();
        }

        var profile = await profileApiClient.GetProfile(userId);

        if (profile == null)
        {
            return NotFound();
        }

        if (!BsonDocument.TryParse(dto.Content, out var content))
        {
            return BadRequest();
        }

        ReplyToSelectionDto? replyToSelectionDto = null;

        if (dto.RootCommentId != null && dto.ReplyCommentAuthorId != null)
        {
            var rootCommentAuthorProfile = await profileApiClient.GetProfile(dto.ReplyCommentAuthorId.Value);

            if (rootCommentAuthorProfile != null)
            {
                replyToSelectionDto ??= new()
                {
                    UserId = rootCommentAuthorProfile.UserId,
                    UserDisplayName = rootCommentAuthorProfile.DisplayName
                };
            }

        }
        if (dto.RootCommentId != null)
        {
            await db.Comments.UpdateOneAsync(Builders<CommentDocument>.Filter.Eq(c => c.Id, dto.RootCommentId),
                Builders<CommentDocument>.Update.Inc(c => c.RepliesCount, 1));
        }

        var comment = new CommentDocument
        {
            AuthorId = userId,
            Author = new AuthorDocument
            {
                Id = profile.Id,
                AvatarUrl = profile.AvatarUrl,
                DisplayName = profile.DisplayName,
                IsVerified = profile.IsVerified,
                UserId = profile.UserId,
                UserName = profile.UserName
            },
            Content = content,
            CreatedAt = DateTime.UtcNow,
            PostId = dto.PostId,
            RootCommentId = dto.RootCommentId,
            ReplyToSelection = replyToSelectionDto
        };

        await db.Comments.InsertOneAsync(comment);

        producer.Produce("post-commented", new Message<string, string>
        {
            Key = Guid.CreateVersion7().ToString(),
            Value = JsonSerializer.Serialize(new
            {
                CommentId = comment.Id,
                dto.PostId,
                Value = 1
            })
        });

        return Ok(new
        {
            comment.Id,
            comment.PostId,
            comment.RootCommentId,
            comment.AuthorId,
            comment.Author,
            Content = BsonTypeMapper.MapToDotNetValue(comment.Content),
            comment.LikesCount,
            comment.ReplyToSelection,
            comment.CreatedAt,
            comment.RepliesCount,
            IsLiked = false
        });
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteComment(
        [FromServices] IMongoDbService db,
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

        var specificFilter = Builders<CommentDocument>.Filter.And(
            Builders<CommentDocument>.Filter.Eq(c => c.Id, commentId),
            Builders<CommentDocument>.Filter.Eq(c => c.AuthorId, userId)
        );

        var filter = Builders<CommentDocument>.Filter.Or(
            specificFilter,
            Builders<CommentDocument>.Filter.Eq(c => c.RootCommentId, commentId)
        );

        var specificComment = await db.Comments.Find(specificFilter).FirstOrDefaultAsync();

        if (specificComment == null)
        {
            return NotFound();
        }

        var result = await db.Comments.DeleteManyAsync(filter);

        if (result.DeletedCount <= 0)
        {
            return NotFound();
        }

        if (specificComment.RootCommentId != null)
        {
            await db.Comments.UpdateOneAsync(Builders<CommentDocument>.Filter.Eq(c => c.Id, specificComment.RootCommentId),
                Builders<CommentDocument>.Update.Inc(c => c.RepliesCount, -result.DeletedCount));
        }

        producer.Produce("comments-deleted", new Message<string, string>
        {
            Key = Guid.CreateVersion7().ToString(),
            Value = JsonSerializer.Serialize(new
            {
                RootCommentId = specificComment.Id,
                specificComment.PostId,
                Value = -result.DeletedCount
            })
        });

        return Ok();
    }

    [HttpGet]
    public async Task<IActionResult> GetComments(
        [FromServices] IMongoDbService db,
        [FromServices] ILikesService likesService,
        [FromQuery] Guid? postId,
        [FromQuery] Guid? rootCommentId,
        [FromQuery] Guid? lastCommentId,
        [FromQuery] DateTime? lastCommentCreatedAt,
        [FromQuery] int pageSize = 6) 
    {
        if (postId == null)
        {
            return BadRequest();
        }

        lastCommentId ??= Guid.Empty;
        pageSize = (pageSize < 4) ? 4 : (pageSize > 100) ? 100 : pageSize;

        FilterDefinition<CommentDocument>? filter = null;

        SortDefinition<CommentDocument>? sort = null;

        if (rootCommentId != null)
        {
            lastCommentCreatedAt ??= DateTime.MinValue;
            filter = Builders<CommentDocument>.Filter.And(
                Builders<CommentDocument>.Filter.Gt(d => d.CreatedAt, lastCommentCreatedAt),
                Builders<CommentDocument>.Filter.Eq(d => d.PostId, postId),
                Builders<CommentDocument>.Filter.Eq(d => d.RootCommentId, rootCommentId)
            );

            sort = Builders<CommentDocument>.Sort
                .Ascending(d => d.CreatedAt);
        }
        else
        {
            lastCommentCreatedAt ??= DateTime.MaxValue;
            filter = Builders<CommentDocument>.Filter.And(
                Builders<CommentDocument>.Filter.Lt(d => d.CreatedAt, lastCommentCreatedAt),
                Builders<CommentDocument>.Filter.Eq(d => d.PostId, postId),
                Builders<CommentDocument>.Filter.Eq(d => d.RootCommentId, rootCommentId)
            );
            sort = Builders<CommentDocument>.Sort
                .Descending(d => d.CreatedAt);
        }

        var comments = await db.Comments
            .Find(filter)
            .Sort(sort)
            .Limit(pageSize)
            .ToListAsync();

        if (comments == null)
        {
            return NotFound();
        }

        var result = comments.Select(c => new GetCommentDto
        {
            Id = c.Id,
            PostId = c.PostId,
            RootCommentId = c.RootCommentId,
            AuthorId = c.AuthorId,
            Author = c.Author,
            Content = BsonTypeMapper.MapToDotNetValue(c.Content),
            LikesCount = c.LikesCount,
            ReplyToSelection = c.ReplyToSelection,
            CreatedAt = c.CreatedAt,
            RepliesCount = c.RepliesCount,
            IsLiked = false
        }).ToList();

        #region likes

        var userIdRaw = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        if (userIdRaw == null || string.IsNullOrEmpty(userIdRaw.Value) || !Guid.TryParse(userIdRaw.Value, out var userId))
        {
            return Ok(result);
        }


        var likes = await likesService.IsCommentsLiked(userId, [.. result.Select(c => c.Id)]);

        if (likes.Length == result.Count)
        {
            for (int i = 0; i < likes.Length; i++)
            {
                result[i].IsLiked = likes[i];
            }
        }

        #endregion

        return Ok(result);
    }
}
