using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.Configuration;
using WileyWidget.Data;
using Microsoft.EntityFrameworkCore;

Console.WriteLine("🔍 Testing Enterprise Database Connection (tools/DbConnTest)...");

// Locate appsettings.json by walking up parent directories so the test works
// regardless of the current working directory when running dotnet run.
string? FindAppSettings()
{
    var dir = new System.IO.DirectoryInfo(System.IO.Directory.GetCurrentDirectory());
    for (int i = 0; i < 8 && dir != null; i++)
    {
        var candidate = System.IO.Path.Combine(dir.FullName, "appsettings.json");
        if (System.IO.File.Exists(candidate)) return candidate;
        dir = dir.Parent;
    }
    return null;
}

var appSettingsPath = FindAppSettings();
if (appSettingsPath is null)
{
    Console.WriteLine("❌ Could not locate appsettings.json in parent directories. Place it near the solution root or run from repository root.");
    return;
}

var configuration = new ConfigurationBuilder()
    .AddJsonFile(appSettingsPath, optional: false, reloadOnChange: false)
    .AddEnvironmentVariables()
    .Build();

var services = new ServiceCollection()
    .AddLogging(config => config.AddConsole())
    .AddSingleton<IConfiguration>(configuration)
    .AddEnterpriseDatabaseServices(configuration);

var sp = services.BuildServiceProvider();

try
{
    var factory = sp.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var ctx = await factory.CreateDbContextAsync();
    Console.WriteLine("✅ DbContext created");
    var canConnect = await ctx.Database.CanConnectAsync();
    Console.WriteLine($"✅ Database.CanConnectAsync: {canConnect}");
    if (canConnect)
    {
        var conn = ctx.Database.GetDbConnection();
        Console.WriteLine($"Connected to: {conn.DataSource} / {conn.Database}");
        var count = await ctx.Enterprises.CountAsync();
        Console.WriteLine($"Enterprise count: {count}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Test failed: {ex.Message}");
    Console.WriteLine(ex);
}

Console.WriteLine("Done.");