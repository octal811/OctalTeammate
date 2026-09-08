using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctalPulse.Application.Features.Post.Commands.AddPostComment;
using OctalPulse.Application.Features.Post.Commands.CreatePost;
using OctalPulse.Application.Features.Post.Commands.DeletePost;
using OctalPulse.Application.Features.Post.Commands.DeletePostComment;
using OctalPulse.Application.Features.Post.Commands.TogglePostReaction;
using OctalPulse.Application.Features.Post.Commands.UpdatePost;
using OctalPulse.Application.Features.Post.Models;
using OctalPulse.Application.Features.Post.Queries.GetFeedPosts;
using OctalPulse.Application.Features.Post.Queries.GetPostById;
using OctalPulse.Application.Features.Post.Queries.GetPostComments;
using OctalPulse.Application.Features.Post.Queries.GetProfilePosts;
using OctalPulse.Domain.Enums;

namespace OctalPulse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PostsController : ControllerBase
{
    private readonly ISender _sender;

    public PostsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    public async Task<ActionResult<PostResponse>> Create(
        [FromBody] CreatePostRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreatePostCommand(
            request.Content,
            request.PhotoUrl,
            request.ProjectId,
            request.TrackId,
            GetUserId());

        var result = await _sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("feed")]
    public async Task<ActionResult<PaginatedListResponse<PostResponse>>> GetFeed(
        [FromBody] GetFeedPostsRequest request,
        CancellationToken cancellationToken)
    {
        var query = new GetFeedPostsQuery(
            request.PageNumber,
            request.PageSize,
            request.ProjectId,
            request.TrackId,
            request.OnlyGeneral,
            GetUserId());

        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("profile/{userId}")]
    public async Task<ActionResult<PaginatedListResponse<PostResponse>>> GetProfilePosts(
        Guid userId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new GetProfilePostsQuery(userId, pageNumber, pageSize, GetUserId());
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PostResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetPostByIdQuery(id, GetUserId());
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<PostResponse>> Update(
        Guid id,
        [FromBody] UpdatePostApiRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdatePostCommand(id, request.Content, request.PhotoUrl, GetUserId());
        var result = await _sender.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeletePostCommand(id, GetUserId());
        await _sender.Send(command, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id}/reactions")]
    public async Task<ActionResult<PostResponse>> ToggleReaction(
        Guid id,
        [FromBody] ToggleReactionApiRequest request,
        CancellationToken cancellationToken)
    {
        var command = new TogglePostReactionCommand(id, request.Type, GetUserId());
        var result = await _sender.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id}/comments")]
    public async Task<ActionResult<CommentResponse>> AddComment(
        Guid id,
        [FromBody] AddCommentApiRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AddPostCommentCommand(id, request.Content, request.ParentCommentId, GetUserId());
        var result = await _sender.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id}/comments")]
    public async Task<ActionResult<PaginatedListResponse<CommentResponse>>> GetComments(
        Guid id,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new GetPostCommentsQuery(id, pageNumber, pageSize, GetUserId());
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id}/comments/{commentId}")]
    public async Task<ActionResult> DeleteComment(
        Guid id,
        Guid commentId,
        CancellationToken cancellationToken)
    {
        var command = new DeletePostCommentCommand(id, commentId, GetUserId());
        await _sender.Send(command, cancellationToken);
        return NoContent();
    }

    private Guid GetUserId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var parsed) ? parsed : Guid.Empty;
    }
}

public record CreatePostRequest(
    string Content,
    string? PhotoUrl = null,
    Guid? ProjectId = null,
    Guid? TrackId = null);

public record GetFeedPostsRequest(
    int PageNumber = 1,
    int PageSize = 10,
    Guid? ProjectId = null,
    Guid? TrackId = null,
    bool OnlyGeneral = false);

public record UpdatePostApiRequest(
    string Content,
    string? PhotoUrl = null);

public record ToggleReactionApiRequest(
    ReactionType Type);

public record AddCommentApiRequest(
    string Content,
    Guid? ParentCommentId = null);
