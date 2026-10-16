using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace JobTrack.Tests.Integration;

/// <summary>
/// Hosts the real API against a throw-away PostgreSQL database (created per test run, dropped afterwards).
/// The server is taken from JOBTRACK_TEST_DB (an admin-capable connection string); by default the local dev server.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string DefaultServer = "Host=127.0.0.1;Port=5432;Username=jobtrack;Password=jobtrack_dev;Database=postgres";

    private readonly string _serverConnectionString =
        Environment.GetEnvironmentVariable("JOBTRACK_TEST_DB") ?? DefaultServer;

    private readonly string _databaseName = $"jobtrack_test_{Guid.NewGuid():N}";

    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), $"jobtrack-tests-{Guid.NewGuid():N}");

    public string ConnectionString => new NpgsqlConnectionStringBuilder(_serverConnectionString)
    {
        Database = _databaseName,
        // Tests drop the database at the end; pooled idle connections would block DROP DATABASE.
        Pooling = false,
    }.ConnectionString;

    public async Task InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(_serverConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", connection);
        await command.ExecuteNonQueryAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await using var connection = new NpgsqlConnection(_serverConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = ConnectionString,
            ["Jwt:SigningKey"] = "integration-tests-signing-key-0123456789abcdef",
            ["Database:MigrateOnStartup"] = "true",
            ["Storage:RootPath"] = _storageRoot,
            // Never call a real AI provider from tests, even if the developer has a key configured.
            ["Ai:Anthropic:ApiKey"] = "",
            ["RateLimiting:AuthPerMinute"] = "10000",
            ["RateLimiting:UploadsPerHour"] = "10000",
            ["RateLimiting:AnalysesPerHour"] = "10000",
        }));
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}

