using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using OctalPulse.Application.Features.Command.UserProfile.SetUserImage;
using OctalPulse.Application.Features.Command.UserProfile.UpdateProfile;
using OctalPulse.Application.Features.Query.UserProfile.GetProfile;
using OctalPulse.Application.Features.Query.UserSearch;
using OctalPulse.Domain.Enums;

namespace OctalPulse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private const long MaxImageSizeBytes = 5 * 1024 * 1024;

    private readonly ISender _sender;
    private readonly IWebHostEnvironment _environment;

    public UsersController(ISender sender, IWebHostEnvironment environment)
    {
        _sender = sender;
        _environment = environment;
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserProfileResponse>> GetMe(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await _sender.Send(new GetProfileQuery(userId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<UserProfileResponse>> GetById(Guid userId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetProfileQuery(userId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("search")]
    public async Task<ActionResult<SearchUsersResponse>> Search(
        [FromQuery] string query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest("Search query is required.");

        var trimmed = query.Trim();
        if (trimmed.Length > 100)
            return BadRequest("Search query is too long.");

        var result = await _sender.Send(new SearchUsersQuery(trimmed), cancellationToken);
        return Ok(result);
    }

    [HttpPut("profile")]
    public async Task<ActionResult<UserProfileResponse>> UpdateProfile(
        [FromBody] UpdateProfileCommand command,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await _sender.Send(command with { UserId = userId }, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Multipart form body for uploading a profile or background image.
    /// </summary>
    public class UploadImageRequest
    {
        public string Type { get; set; } = "profile";
        public IFormFile? File { get; set; }
    }

    [HttpPost("profile/image")]
    [RequestSizeLimit(MaxImageSizeBytes + 1024)]
    public async Task<ActionResult<UserProfileResponse>> UploadProfileImage(
        [FromForm] UploadImageRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var type = request.Type;
        var file = request.File;

        if (!Enum.TryParse<UserImageType>(type, ignoreCase: true, out var imageType) ||
            imageType is not (UserImageType.Profile or UserImageType.Background))
        {
            return BadRequest("Image type must be 'profile' or 'background'.");
        }

        if (file is null || file.Length == 0)
            return BadRequest("No file was uploaded.");

        if (file.Length > MaxImageSizeBytes)
            return BadRequest("Image must not exceed 5 MB.");

        var extension = file.ContentType?.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => string.Empty
        };
        if (string.IsNullOrEmpty(extension))
            return BadRequest("Only PNG, JPEG, WebP or GIF images are allowed.");

        var folderName = imageType == UserImageType.Background ? "BackgroundImages" : "ProfileImages";
        var directory = Path.Combine(_environment.ContentRootPath, "Resources", "Users", folderName);
        Directory.CreateDirectory(directory);

        var currentProfile = await _sender.Send(new GetProfileQuery(userId), cancellationToken);
        var previousUrl = imageType == UserImageType.Background
            ? currentProfile.BackgroundImageUrl
            : currentProfile.ProfilePictureUrl;

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(directory, fileName);
        await using (var stream = System.IO.File.Create(filePath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var imageUrl = $"/Resources/Users/{folderName}/{fileName}";
        var result = await _sender.Send(new SetUserImageCommand(userId, imageType, imageUrl), cancellationToken);

        TryDeletePreviousFile(previousUrl, imageUrl, directory);

        return Ok(result);
    }

    private static void TryDeletePreviousFile(string? previousUrl, string newUrl, string directory)
    {
        if (string.IsNullOrWhiteSpace(previousUrl) ||
            previousUrl.Equals(newUrl, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var previousName = Path.GetFileName(previousUrl);
        if (string.IsNullOrEmpty(previousName)) return;

        try
        {
            var existing = Path.Combine(directory, previousName);
            if (System.IO.File.Exists(existing))
            {
                System.IO.File.Delete(existing);
            }
        }
        catch
        {
            // Non-critical cleanup failure is ignored.
        }
    }

    private Guid GetUserId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var parsed) ? parsed : Guid.Empty;
    }
}