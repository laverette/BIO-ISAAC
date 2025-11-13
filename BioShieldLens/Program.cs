using Microsoft.EntityFrameworkCore;
using BioShieldLens.Data;
using BioShieldLens.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews();

// Configure MySQL connection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    // Try to parse Heroku DATABASE_URL if available
    var dbUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (!string.IsNullOrEmpty(dbUrl) && dbUrl.StartsWith("mysql://"))
    {
        var uri = new Uri(dbUrl);
        var userInfo = uri.UserInfo.Split(':');
        connectionString = $"Server={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};User Id={userInfo[0]};Password={userInfo[1]};CharSet=utf8mb4;SslMode=Required;";
    }
}

if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Database connection string is required");
}

// Log connection string (without password) for debugging
var connectionStringForLogging = connectionString.Contains("Password=") 
    ? connectionString.Substring(0, connectionString.IndexOf("Password=")) + "Password=***" 
    : connectionString;
Console.WriteLine($"Using connection string: {connectionStringForLogging}");

// Use MySQL 8.0 server version (common for Heroku/ClearDB) instead of AutoDetect to avoid connection during startup
var serverVersion = ServerVersion.Parse("8.0.33-mysql");

builder.Services.AddDbContext<BioShieldDbContext>(options =>
    options.UseMySql(connectionString, serverVersion, mySqlOptions =>
    {
        // Enable retry logic for transient failures (common with cloud databases)
        mySqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null);
    }));

// Register services
builder.Services.AddScoped<INvdDataService, NvdDataService>();
builder.Services.AddScoped<IAiClassificationService, AiClassificationService>();
builder.Services.AddScoped<IVulnerabilityService, VulnerabilityService>();
builder.Services.AddScoped<ITrendService, TrendService>();

// Add HttpClient for API calls
builder.Services.AddHttpClient();

// Register background service for automatic data fetching
builder.Services.AddHostedService<BackgroundDataService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Ensure database is created (with error handling)
try
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<BioShieldDbContext>();
        db.Database.EnsureCreated();
        Console.WriteLine("Database connection successful and tables created/verified.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Warning: Could not connect to database: {ex.Message}");
    Console.WriteLine("The app will continue, but database operations may fail.");
    Console.WriteLine("Please check your connection string and network access.");
}

app.Run();
