namespace UTowny.Configuration;

public sealed class UTownyOptions
{
    public DatabaseOptions Database { get; set; } = new();
    public TownOptions Towns { get; set; } = new();
    public ClaimOptions Claims { get; set; } = new();
    public PlotOptions Plots { get; set; } = new();
    public EconomyOptions Economy { get; set; } = new();
    public TaxOptions Taxes { get; set; } = new();
    public UpkeepOptions Upkeep { get; set; } = new();
    public NationOptions Nations { get; set; } = new();
    public WarOptions Wars { get; set; } = new();
}
public sealed class DatabaseOptions { public string FileName { get; set; } = "utowny.db"; }
public sealed class TownOptions
{
    public long CreationPrice { get; set; } = 25000;
    public int MinimumPlaytimeHours { get; set; } = 20;
    public int StartingClaims { get; set; } = 8;
    public int ClaimsPerResident { get; set; } = 2;
    public bool DefaultPvp { get; set; }
    public bool CoMayorCanDisband { get; set; }
    public bool CoMayorCanTransferMayor { get; set; }
}
public sealed class ClaimOptions
{
    public int GridSizeMeters { get; set; } = 64;
    public long ClaimPrice { get; set; } = 2500;
    public bool RequireAdjacency { get; set; } = true;
    public string MapIdOverride { get; set; } = "";
}
public sealed class PlotOptions { public bool OutsidersCanBuy { get; set; } }
public sealed class EconomyOptions { public long StartingBalance { get; set; } }
public sealed class TaxOptions { public int IntervalHours { get; set; } = 24; public int MissedCyclesBeforeKick { get; set; } = 1; public string[] ExemptRoles { get; set; } = new[] { "Mayor" }; }
public sealed class UpkeepOptions { public int IntervalHours { get; set; } = 24; public long BaseAmount { get; set; } = 1000; public long PerClaimAmount { get; set; } = 100; public long PerResidentAmount { get; set; } = 250; }
public sealed class NationOptions { public long CreationPrice { get; set; } = 100000; }
public sealed class WarOptions { public int PreparationHours { get; set; } = 12; public int DurationMinutes { get; set; } = 30; }
