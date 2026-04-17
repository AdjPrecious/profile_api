using Microsoft.EntityFrameworkCore;
using ProfilesApi.Data;
using ProfilesApi.DTOs;
using ProfilesApi.Models;
using ProfilesApi.Utilities;

namespace ProfilesApi.Services;

public interface IProfileService
{
    Task<(ProfileResponse profile, bool alreadyExisted)> CreateProfileAsync(string name);
    Task<ProfileResponse?> GetProfileByIdAsync(Guid id);
    Task<List<ProfileListItemResponse>> GetAllProfilesAsync(string? gender, string? countryId, string? ageGroup);
    Task<bool> DeleteProfileAsync(Guid id);
}

public class ProfileService(
    AppDbContext db,
    IExternalApiService externalApi,
    ILogger<ProfileService> logger) : IProfileService
{
    // ── Create ────────────────────────────────────────────────────────────────
    public async Task<(ProfileResponse, bool)> CreateProfileAsync(string name)
    {
        // Duplicate check (case-insensitive)
        var existing = await db.Profiles
            .FirstOrDefaultAsync(p => p.Name.ToLower() == name.ToLower());

        if (existing is not null)
            return (MapToResponse(existing), true);

        // Call all three APIs concurrently
        var genderTask = externalApi.GetGenderAsync(name);
        var ageTask    = externalApi.GetAgeAsync(name);
        var natTask    = externalApi.GetNationalityAsync(name);

        await Task.WhenAll(genderTask, ageTask, natTask);

        var genderData = await genderTask;
        var ageData    = await ageTask;
        var natData    = await natTask;

        // Validate responses (502 rules)
        if (string.IsNullOrWhiteSpace(genderData.Gender) || genderData.Count == 0)
            throw new ExternalApiException("Genderize");

        if (ageData.Age is null)
            throw new ExternalApiException("Agify");

        if (natData.Country is null || natData.Country.Count == 0)
            throw new ExternalApiException("Nationalize");

        // Pick top nationality
        var topCountry = natData.Country.OrderByDescending(c => c.Probability).First();

        var profile = new Profile
        {
            Id                 = UuidV7.NewGuid(),
            Name               = name.ToLower(),
            Gender             = genderData.Gender,
            GenderProbability  = genderData.Probability,
            SampleSize         = genderData.Count,
            Age                = ageData.Age.Value,
            AgeGroup           = ClassifyAge(ageData.Age.Value),
            CountryId          = topCountry.CountryId,
            CountryProbability = topCountry.Probability,
            CreatedAt          = DateTime.UtcNow
        };

        db.Profiles.Add(profile);
        await db.SaveChangesAsync();

        logger.LogInformation("Created profile {Id} for name '{Name}'", profile.Id, name);
        return (MapToResponse(profile), false);
    }

    // ── Read single ───────────────────────────────────────────────────────────
    public async Task<ProfileResponse?> GetProfileByIdAsync(Guid id)
    {
        var profile = await db.Profiles.FindAsync(id);
        return profile is null ? null : MapToResponse(profile);
    }

    // ── Read all ──────────────────────────────────────────────────────────────
    public async Task<List<ProfileListItemResponse>> GetAllProfilesAsync(
        string? gender, string? countryId, string? ageGroup)
    {
        var query = db.Profiles.AsQueryable();

        if (!string.IsNullOrWhiteSpace(gender))
            query = query.Where(p => p.Gender!.ToLower() == gender.ToLower());

        if (!string.IsNullOrWhiteSpace(countryId))
            query = query.Where(p => p.CountryId.ToLower() == countryId.ToLower());

        if (!string.IsNullOrWhiteSpace(ageGroup))
            query = query.Where(p => p.AgeGroup.ToLower() == ageGroup.ToLower());

        return await query
            .OrderBy(p => p.CreatedAt)
            .Select(p => new ProfileListItemResponse
            {
                Id       = p.Id.ToString(),
                Name     = p.Name,
                Gender   = p.Gender,
                Age      = p.Age,
                AgeGroup = p.AgeGroup,
                CountryId = p.CountryId
            })
            .ToListAsync();
    }

    // ── Delete ────────────────────────────────────────────────────────────────
    public async Task<bool> DeleteProfileAsync(Guid id)
    {
        var profile = await db.Profiles.FindAsync(id);
        if (profile is null) return false;

        db.Profiles.Remove(profile);
        await db.SaveChangesAsync();
        return true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static string ClassifyAge(int age) => age switch
    {
        <= 12             => "child",
        >= 13 and <= 19   => "teenager",
        >= 20 and <= 59   => "adult",
        _                 => "senior"
    };

    private static ProfileResponse MapToResponse(Profile p) => new()
    {
        Id                 = p.Id.ToString(),
        Name               = p.Name,
        Gender             = p.Gender,
        GenderProbability  = p.GenderProbability,
        SampleSize         = p.SampleSize,
        Age                = p.Age,
        AgeGroup           = p.AgeGroup,
        CountryId          = p.CountryId,
        CountryProbability = p.CountryProbability,
        CreatedAt          = p.CreatedAt.ToUniversalTime()
                              .ToString("yyyy-MM-ddTHH:mm:ssZ")
    };
}
