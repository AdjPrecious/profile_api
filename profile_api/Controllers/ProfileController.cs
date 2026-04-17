using Microsoft.AspNetCore.Mvc;
using ProfilesApi.DTOs;
using ProfilesApi.Services;

namespace ProfilesApi.Controllers;

[ApiController]
[Route("api/profiles")]
public class ProfilesController(IProfileService profileService, ILogger<ProfilesController> logger)
    : ControllerBase
{
    // ── POST /api/profiles ────────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> CreateProfile([FromBody] CreateProfileRequest? request)
    {
        // Validate content-type / body parse
        if (request is null)
            return UnprocessableEntity(Error("Request body is invalid or missing"));

        // Validate name field
        if (request.Name is null)
            return BadRequest(Error("name is required"));

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(Error("name must not be empty"));

        if (request.Name is not string)
            return UnprocessableEntity(Error("name must be a string"));

        try
        {
            var (profile, existed) = await profileService.CreateProfileAsync(request.Name.Trim());

            if (existed)
                return Ok(new { status = "success", message = "Profile already exists", data = profile });

            return StatusCode(201, new { status = "success", data = profile });
        }
        catch (ExternalApiException ex)
        {
            logger.LogWarning("External API failure: {Message}", ex.Message);
            return StatusCode(502, Error(ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in CreateProfile");
            return StatusCode(500, Error("An unexpected error occurred"));
        }
    }

    // ── GET /api/profiles/{id} ────────────────────────────────────────────────
    [HttpGet("{id}")]
    public async Task<IActionResult> GetProfile(string id)
    {
        if (!Guid.TryParse(id, out var guid))
            return NotFound(Error("Profile not found"));

        try
        {
            var profile = await profileService.GetProfileByIdAsync(guid);
            if (profile is null)
                return NotFound(Error("Profile not found"));

            return Ok(new { status = "success", data = profile });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching profile {Id}", id);
            return StatusCode(500, Error("An unexpected error occurred"));
        }
    }

    // ── GET /api/profiles ─────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetAllProfiles(
        [FromQuery] string? gender,
        [FromQuery] string? country_id,
        [FromQuery] string? age_group)
    {
        try
        {
            var profiles = await profileService.GetAllProfilesAsync(gender, country_id, age_group);
            return Ok(new { status = "success", count = profiles.Count, data = profiles });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching profiles");
            return StatusCode(500, Error("An unexpected error occurred"));
        }
    }

    // ── DELETE /api/profiles/{id} ─────────────────────────────────────────────
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProfile(string id)
    {
        if (!Guid.TryParse(id, out var guid))
            return NotFound(Error("Profile not found"));

        try
        {
            var deleted = await profileService.DeleteProfileAsync(guid);
            if (!deleted)
                return NotFound(Error("Profile not found"));

            return NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting profile {Id}", id);
            return StatusCode(500, Error("An unexpected error occurred"));
        }
    }

    // ── Helper ────────────────────────────────────────────────────────────────
    private static object Error(string message) => new { status = "error", message };
}

