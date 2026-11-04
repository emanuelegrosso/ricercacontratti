using Microsoft.Data.Sqlite;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<ContractStore>();
var app = builder.Build();
app.Services.GetRequiredService<ContractStore>();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/contracts", async (HttpRequest request, ContractStore store) =>
{
    var all = await store.GetAllAsync();
    var q = request.Query;
    var filtered = all.AsEnumerable();
    string? S(string key) => q.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v.ToString().Trim() : null;
    decimal? D(string key) => decimal.TryParse(S(key), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
    int? I(string key) => int.TryParse(S(key), out var v) ? v : null;
    DateTime? Date(string key) => DateTime.TryParse(S(key), CultureInfo.InvariantCulture, DateTimeStyles.None, out var v) ? v.Date : null;
    bool? B(string key) => S(key) switch { "true" => true, "false" => false, _ => null };
    bool Has(string key) => q.TryGetValue(key, out var value) && value == "true";
    if (S("q") is { } text) filtered = filtered.Where(c => $"{c.Model} {c.Version} {c.Company} {c.Brand} {c.Title}".Contains(text, StringComparison.OrdinalIgnoreCase));
    if (S("company") is { } company) filtered = filtered.Where(c => c.Company == company);
    if (S("fuel") is { } fuel) filtered = filtered.Where(c => c.Fuel == fuel);
    if (S("transmission") is { } transmission) filtered = filtered.Where(c => c.Transmission == transmission);
    if (I("yearMin") is { } yearMin) filtered = filtered.Where(c => c.Year >= yearMin);
    if (I("yearMax") is { } yearMax) filtered = filtered.Where(c => c.Year <= yearMax);
    if (D("feeMin") is { } feeMin) filtered = filtered.Where(c => c.MonthlyFee >= feeMin);
    if (D("feeMax") is { } feeMax) filtered = filtered.Where(c => c.MonthlyFee <= feeMax);
    if (D("paymentMin") is { } paymentMin) filtered = filtered.Where(c => c.InitialPayment >= paymentMin);
    if (D("paymentMax") is { } paymentMax) filtered = filtered.Where(c => c.InitialPayment <= paymentMax);
    if (B("vat") is { } vat) filtered = filtered.Where(c => c.VatIncluded == vat);
    if (I("totalKmMin") is { } totalKmMin) filtered = filtered.Where(c => c.TotalKm >= totalKmMin);
    if (I("totalKmMax") is { } totalKmMax) filtered = filtered.Where(c => c.TotalKm <= totalKmMax);
    if (I("currentKmMin") is { } currentKmMin) filtered = filtered.Where(c => c.CurrentKm >= currentKmMin);
    if (I("currentKmMax") is { } currentKmMax) filtered = filtered.Where(c => c.CurrentKm <= currentKmMax);
    if (I("remainingKmMin") is { } remainingKmMin) filtered = filtered.Where(c => c.RemainingKm >= remainingKmMin);
    if (I("remainingKmMax") is { } remainingKmMax) filtered = filtered.Where(c => c.RemainingKm <= remainingKmMax);
    if (I("monthsMin") is { } monthsMin) filtered = filtered.Where(c => c.MonthsRemaining >= monthsMin);
    if (D("excessMin") is { } excessMin) filtered = filtered.Where(c => c.ExcessKmRate >= excessMin);
    if (D("excessMax") is { } excessMax) filtered = filtered.Where(c => c.ExcessKmRate <= excessMax);
    if (D("underMin") is { } underMin) filtered = filtered.Where(c => c.UnderKmRate >= underMin);
    if (D("underMax") is { } underMax) filtered = filtered.Where(c => c.UnderKmRate <= underMax);
    if (Date("endFrom") is { } endFrom) filtered = filtered.Where(c => c.EndDate.Date >= endFrom);
    if (Date("endTo") is { } endTo) filtered = filtered.Where(c => c.EndDate.Date <= endTo);
    if (S("contributionType") is { } contribution) filtered = filtered.Where(c => c.ContributionType == contribution);
    if (D("contributionMin") is { } contributionMin) filtered = filtered.Where(c => c.ContributionAmount >= contributionMin);
    if (D("contributionMax") is { } contributionMax) filtered = filtered.Where(c => c.ContributionAmount <= contributionMax);
    foreach (var (key, selector) in new (string, Func<Contract, bool>)[] {
        ("maintenance", c=>c.Maintenance), ("insurance", c=>c.Insurance), ("roadside", c=>c.Roadside),
        ("replacement", c=>c.ReplacementCar), ("tires", c=>c.Tires), ("roadTax", c=>c.RoadTax) })
        if (Has(key)) filtered = filtered.Where(selector);
    if (S("insuranceKind") is { } kind) filtered = filtered.Where(c => c.InsuranceKinds.Contains(kind));
    var result = filtered.OrderBy(c => c.EndDate).ToList();
    return Results.Ok(new { total = result.Count, contracts = result });
});

app.MapPost("/api/contracts", async (Contract input, ContractStore store) =>
{
    if (string.IsNullOrWhiteSpace(input.Model) || string.IsNullOrWhiteSpace(input.Company))
        return Results.BadRequest(new { message = "Modello e società di noleggio sono obbligatori." });
    input.RemainingKm = Math.Max(0, input.TotalKm - input.CurrentKm);
    input.MonthsRemaining = Math.Max(0, (input.EndDate.Year - DateTime.Today.Year) * 12 + input.EndDate.Month - DateTime.Today.Month);
    input.Id = Guid.NewGuid().ToString("N");
    input.Title = $"{input.Brand} {input.Model} {input.Version}".Trim();
    await store.AddAsync(input);
    return Results.Created($"/api/contracts/{input.Id}", input);
});

app.MapGet("/api/options", async (ContractStore store) => {
    var all = await store.GetAllAsync();
    return Results.Ok(new { companies = all.Select(c => c.Company).Distinct().Order().ToArray() });
});
app.Run();

public sealed class ContractStore
{
    private readonly string _connectionString;
    public ContractStore(IWebHostEnvironment env)
    {
        var folder = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(folder);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = Path.Combine(folder, "contracts.db") }.ToString();
        Initialize();
    }
    private void Initialize()
    {
        using var connection = new SqliteConnection(_connectionString); connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
        CREATE TABLE IF NOT EXISTS Contracts (
          Id TEXT PRIMARY KEY, Title TEXT NOT NULL, Company TEXT NOT NULL, Brand TEXT NOT NULL, Model TEXT NOT NULL, Version TEXT NOT NULL,
          Fuel TEXT NOT NULL, Transmission TEXT NOT NULL, Year INTEGER NOT NULL, StartDate TEXT, EndDate TEXT NOT NULL,
          MonthlyFee REAL NOT NULL, VatIncluded INTEGER NOT NULL, InitialPayment REAL NOT NULL, TotalKm INTEGER NOT NULL,
          CurrentKm INTEGER NOT NULL, RemainingKm INTEGER NOT NULL, ExcessKmRate REAL NOT NULL, UnderKmRate REAL NOT NULL,
          ContributionType TEXT NOT NULL, ContributionAmount REAL NOT NULL, Maintenance INTEGER NOT NULL, Insurance INTEGER NOT NULL,
          Roadside INTEGER NOT NULL, ReplacementCar INTEGER NOT NULL, Tires INTEGER NOT NULL, RoadTax INTEGER NOT NULL,
          InsuranceKinds TEXT NOT NULL, BusinessStatus TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_Contracts_Company ON Contracts(Company);
        CREATE INDEX IF NOT EXISTS IX_Contracts_EndDate ON Contracts(EndDate);
        """; command.ExecuteNonQuery();
        command.CommandText = "SELECT COUNT(*) FROM Contracts";
        if ((long)command.ExecuteScalar()! > 0) return;
        var samples = SeedData.Create();
        foreach (var item in samples) Insert(connection, item);
    }
    public async Task<List<Contract>> GetAllAsync()
    {
        var results = new List<Contract>();
        await using var connection = new SqliteConnection(_connectionString); await connection.OpenAsync();
        await using var command = connection.CreateCommand(); command.CommandText = "SELECT * FROM Contracts";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) results.Add(Read(reader));
        return results;
    }
    public async Task AddAsync(Contract item)
    {
        await using var connection = new SqliteConnection(_connectionString); await connection.OpenAsync();
        Insert(connection, item);
    }
    private static void Insert(SqliteConnection connection, Contract c)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """INSERT INTO Contracts VALUES ($id,$title,$company,$brand,$model,$version,$fuel,$transmission,$year,$start,$end,$fee,$vat,$payment,$total,$current,$remaining,$excess,$under,$contribution,$amount,$maintenance,$insurance,$roadside,$replacement,$tires,$roadTax,$kinds,$status)""";
        cmd.Parameters.AddWithValue("$id", c.Id); cmd.Parameters.AddWithValue("$title", c.Title); cmd.Parameters.AddWithValue("$company", c.Company);
        cmd.Parameters.AddWithValue("$brand", c.Brand); cmd.Parameters.AddWithValue("$model", c.Model); cmd.Parameters.AddWithValue("$version", c.Version);
        cmd.Parameters.AddWithValue("$fuel", c.Fuel); cmd.Parameters.AddWithValue("$transmission", c.Transmission); cmd.Parameters.AddWithValue("$year", c.Year);
        cmd.Parameters.AddWithValue("$start", c.StartDate.ToString("yyyy-MM-dd")); cmd.Parameters.AddWithValue("$end", c.EndDate.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$fee", c.MonthlyFee); cmd.Parameters.AddWithValue("$vat", c.VatIncluded ? 1 : 0); cmd.Parameters.AddWithValue("$payment", c.InitialPayment);
        cmd.Parameters.AddWithValue("$total", c.TotalKm); cmd.Parameters.AddWithValue("$current", c.CurrentKm); cmd.Parameters.AddWithValue("$remaining", c.RemainingKm);
        cmd.Parameters.AddWithValue("$excess", c.ExcessKmRate); cmd.Parameters.AddWithValue("$under", c.UnderKmRate);
        cmd.Parameters.AddWithValue("$contribution", c.ContributionType); cmd.Parameters.AddWithValue("$amount", c.ContributionAmount);
        cmd.Parameters.AddWithValue("$maintenance", c.Maintenance ? 1 : 0); cmd.Parameters.AddWithValue("$insurance", c.Insurance ? 1 : 0);
        cmd.Parameters.AddWithValue("$roadside", c.Roadside ? 1 : 0); cmd.Parameters.AddWithValue("$replacement", c.ReplacementCar ? 1 : 0);
        cmd.Parameters.AddWithValue("$tires", c.Tires ? 1 : 0); cmd.Parameters.AddWithValue("$roadTax", c.RoadTax ? 1 : 0);
        cmd.Parameters.AddWithValue("$kinds", string.Join(',', c.InsuranceKinds)); cmd.Parameters.AddWithValue("$status", c.BusinessStatus);
        cmd.ExecuteNonQuery();
    }
    private static Contract Read(SqliteDataReader r) => new() {
        Id=r.GetString(0),Title=r.GetString(1),Company=r.GetString(2),Brand=r.GetString(3),Model=r.GetString(4),Version=r.GetString(5),
        Fuel=r.GetString(6),Transmission=r.GetString(7),Year=r.GetInt32(8),StartDate=DateTime.Parse(r.GetString(9)),EndDate=DateTime.Parse(r.GetString(10)),
        MonthlyFee=r.GetDecimal(11),VatIncluded=r.GetInt32(12)==1,InitialPayment=r.GetDecimal(13),TotalKm=r.GetInt32(14),CurrentKm=r.GetInt32(15),RemainingKm=r.GetInt32(16),
        ExcessKmRate=r.GetDecimal(17),UnderKmRate=r.GetDecimal(18),ContributionType=r.GetString(19),ContributionAmount=r.GetDecimal(20),
        Maintenance=r.GetInt32(21)==1,Insurance=r.GetInt32(22)==1,Roadside=r.GetInt32(23)==1,ReplacementCar=r.GetInt32(24)==1,Tires=r.GetInt32(25)==1,RoadTax=r.GetInt32(26)==1,
        InsuranceKinds=r.GetString(27).Split(',',StringSplitOptions.RemoveEmptyEntries).ToList(),BusinessStatus=r.GetString(28),
        MonthsRemaining=Math.Max(0,(DateTime.Parse(r.GetString(10)).Year-DateTime.Today.Year)*12+DateTime.Parse(r.GetString(10)).Month-DateTime.Today.Month)
    };
}

public sealed class Contract
{
    public string Id { get; set; } = ""; public string Title { get; set; } = ""; public string Company { get; set; } = ""; public string Brand { get; set; } = "";
    public string Model { get; set; } = ""; public string Version { get; set; } = ""; public string Fuel { get; set; } = "Diesel"; public string Transmission { get; set; } = "Manual";
    public int Year { get; set; } = DateTime.Today.Year; public DateTime StartDate { get; set; } = DateTime.Today; public DateTime EndDate { get; set; } = DateTime.Today.AddYears(2);
    public decimal MonthlyFee { get; set; } public bool VatIncluded { get; set; } public decimal InitialPayment { get; set; } public int TotalKm { get; set; } = 60000;
    public int CurrentKm { get; set; } public int RemainingKm { get; set; } public decimal ExcessKmRate { get; set; } public decimal UnderKmRate { get; set; }
    public string ContributionType { get; set; } = "None"; public decimal ContributionAmount { get; set; }
    public bool Maintenance { get; set; } public bool Insurance { get; set; } public bool Roadside { get; set; } public bool ReplacementCar { get; set; } public bool Tires { get; set; } public bool RoadTax { get; set; }
    public List<string> InsuranceKinds { get; set; } = []; public string BusinessStatus { get; set; } = "Active"; public int MonthsRemaining { get; set; }
}

static class SeedData
{
    static readonly (string, string, string, string, string)[] Vehicles = [
        ("Fiat","500e","La Prima","Electric","Automatic"),("Volkswagen","Golf","Life 2.0 TDI","Diesel","Automatic"),("Toyota","Yaris","Hybrid Lounge","MildHybrid","Automatic"),
        ("Peugeot","208","Allure PureTech","Petrol","Manual"),("Tesla","Model 3","RWD","Electric","Automatic"),("BMW","X1","sDrive18d","Diesel","Automatic"),
        ("Jeep","Avenger","Longitude e-Hybrid","MildHybrid","Automatic"),("Renault","Clio","Techno TCe","Petrol","Manual"),("Audi","A3","Sportback TFSI e","PlugInHybrid","Automatic"),
        ("Ford","Puma","ST-Line EcoBoost","Petrol","Manual"),("Nissan","Qashqai","N-Connecta e-Power","MildHybrid","Automatic"),("Dacia","Duster","Journey ECO-G","LPG","Manual"),
        ("Mercedes-Benz","GLA","200d Progressive","Diesel","Automatic"),("Hyundai","Kona","EV 65 kWh","Electric","Automatic"),("Kia","Sportage","1.6 CRDi MHEV","Diesel","Automatic"),
        ("Alfa Romeo","Tonale","Sprint PHEV","PlugInHybrid","Automatic"),("Skoda","Octavia","Wagon 1.5 TSI","Petrol","Manual"),("Volvo","XC40","B3 Core","MildHybrid","Automatic"),
        ("Opel","Corsa","Edition Electric","Electric","Automatic"),("Peugeot","3008","Hybrid 136","MildHybrid","Automatic")
    ];
    static readonly string[] Companies = ["Arval","Ayvens","Leasys","Alphabet","Drivalia"];
    public static List<Contract> Create() => Vehicles.Select((v,i) => {
        var end=DateTime.Today.AddMonths(2+i*2);
        return new Contract { Id=Guid.NewGuid().ToString("N"), Brand=v.Item1,Model=v.Item2,Version=v.Item3,Fuel=v.Item4,Transmission=v.Item5,
            Title=$"{v.Item1} {v.Item2} {v.Item3}",Company=Companies[i%Companies.Length],Year=2022+i%5,StartDate=end.AddMonths(-36),EndDate=end,
            MonthlyFee=279+i*31, VatIncluded=i%3!=0,InitialPayment=(i%4)*750,TotalKm=60000+(i%4)*10000,CurrentKm=12000+i*1450,
            RemainingKm=Math.Max(0,60000+(i%4)*10000-(12000+i*1450)),ExcessKmRate=0.08m+(i%4)*0.03m,UnderKmRate=0.03m+(i%3)*0.01m,
            ContributionType=i%3==0?"Incentive":i%3==1?"Compensation":"None",ContributionAmount=i%3==2?0:500+i*100,
            Maintenance=i%2==0,Insurance=i%3!=1,Roadside=i%4!=0,ReplacementCar=i%3==0,Tires=i%2==1,RoadTax=i%4<2,
            InsuranceKinds=i%3==1?[]:["RCA","Kasko",i%2==0?"FurtoIncendio":"Cristalli"],BusinessStatus="Active" };
    }).ToList();
}



