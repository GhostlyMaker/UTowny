using Microsoft.Extensions.Options;
namespace UTowny.Configuration;

public sealed class ConfigValidator : IValidateOptions<UTownyOptions>
{
    public ValidateOptionsResult Validate(string? name, UTownyOptions o)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(o.Database.FileName)) errors.Add("database.file_name must not be empty");
        if (o.Towns.CreationPrice < 0) errors.Add("towns.creation_price must be >= 0");
        if (o.Towns.MinimumPlaytimeHours < 0) errors.Add("towns.minimum_playtime_hours must be >= 0");
        if (o.Towns.StartingClaims < 1) errors.Add("towns.starting_claims must be >= 1");
        if (o.Towns.ClaimsPerResident < 0) errors.Add("towns.claims_per_resident must be >= 0");
        if (o.Claims.GridSizeMeters < 8 || o.Claims.GridSizeMeters > 1024) errors.Add("claims.grid_size_meters must be between 8 and 1024");
        if (o.Claims.ClaimPrice < 0) errors.Add("claims.claim_price must be >= 0");
        if (o.Taxes.IntervalHours < 1) errors.Add("taxes.interval_hours must be >= 1");
        if (o.Upkeep.IntervalHours < 1) errors.Add("upkeep.interval_hours must be >= 1");
        if (o.Wars.PreparationHours < 0 || o.Wars.DurationMinutes < 1) errors.Add("war timing is invalid");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
