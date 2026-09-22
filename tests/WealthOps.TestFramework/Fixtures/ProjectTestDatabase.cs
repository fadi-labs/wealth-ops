using Npgsql;
using Xunit.v3;

namespace WealthOps.TestFramework.Fixtures;

/// <summary>
/// Factory for per-test isolated databases used in L1 component tests.
/// Creates a fresh database on demand against the Aspire-hosted PostgreSQL container.
/// </summary>
/// <remarks>
/// Schema creation is supplied by the caller through an <c>initialize</c> callback rather than
/// referenced directly. TestFramework is referenced by every test project including
/// <c>Domain.UnitTest</c>, so taking a project reference on Infrastructure here would drag EF Core
/// and Npgsql into projects whose whole point is that they have no such dependency.
/// </remarks>
public sealed class WealthOpsTestDatabase : IAsyncDisposable
{
    private readonly string _maintenanceConnectionString;
    private readonly Func<string, CancellationToken, Task>? _initialize;

    private WealthOpsTestDatabase(
        string connectionString,
        string databaseName,
        string maintenanceConnectionString,
        Func<string, CancellationToken, Task>? initialize)
    {
        ConnectionString = connectionString;
        DatabaseName = databaseName;
        _maintenanceConnectionString = maintenanceConnectionString;
        _initialize = initialize;
    }

    public string ConnectionString { get; }

    public string DatabaseName { get; }

    /// <param name="aspire">The container fixture supplying the server.</param>
    /// <param name="databaseName">Name for the isolated database. Recreated if it exists.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <param name="output">Test output for diagnostics.</param>
    /// <param name="initialize">
    /// Applies the schema to the new database — typically an EF Core migration run. Receives the
    /// connection string. Omit for tests that only need an empty database.
    /// </param>
    public static async Task<WealthOpsTestDatabase> CreateAsync(
        AspireFixture aspire,
        string databaseName,
        CancellationToken cancellationToken = default,
        ITestOutputHelper? output = null,
        Func<string, CancellationToken, Task>? initialize = null)
    {
        ArgumentNullException.ThrowIfNull(aspire);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

        string maintenanceConnectionString = aspire.CreateDatabaseConnectionString("postgres");
        Log(output, maintenanceConnectionString, $"Recreating database '{databaseName}'...");
        await PostgreSqlDatabaseManager.RecreateDatabaseAsync(maintenanceConnectionString, databaseName);

        string connectionString = aspire.CreateDatabaseConnectionString(databaseName);

        var database = new WealthOpsTestDatabase(
            connectionString,
            databaseName,
            maintenanceConnectionString,
            initialize);

        await database.InitializeAsync(cancellationToken, output);

        Log(output, connectionString, $"Database '{databaseName}' ready.");

        return database;
    }

    /// <summary>Drops and recreates the database, then reapplies the schema.</summary>
    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await PostgreSqlDatabaseManager.RecreateDatabaseAsync(_maintenanceConnectionString, DatabaseName);
        await InitializeAsync(cancellationToken, output: null);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task InitializeAsync(CancellationToken cancellationToken, ITestOutputHelper? output)
    {
        if (_initialize is null)
        {
            return;
        }

        try
        {
            await _initialize(ConnectionString, cancellationToken);
        }
        catch (PostgresException ex) when (IsMissingVectorExtension(ex))
        {
            // The overwhelmingly likely cause is a persistent test container created before the
            // image moved to pgvector. Say so, rather than surfacing a raw PostgreSQL error.
            throw new InvalidOperationException(
                "The test PostgreSQL server does not provide the 'vector' extension. " +
                "This usually means a pre-warmed container from before the image changed to " +
                "pgvector/pgvector is still running. Remove it and let it be recreated: " +
                "`docker rm -f project-test-postgres` (or `podman rm -f ...`).",
                ex);
        }

        Log(output, ConnectionString, "Schema applied.");
    }

    private static bool IsMissingVectorExtension(PostgresException ex)
        // 0A000 feature_not_supported / 58P01 undefined_file are what a missing extension surfaces as.
        => (ex.SqlState is "0A000" or "58P01" or "42704")
           && ex.MessageText.Contains("vector", StringComparison.OrdinalIgnoreCase);

    private static void Log(ITestOutputHelper? output, string connectionString, string message)
    {
        try
        {
            var b = new NpgsqlConnectionStringBuilder(connectionString);
            output?.WriteLine($"[WealthOpsTestDatabase {DateTime.UtcNow:HH:mm:ss.fff}] {b.Host}:{b.Port} — {message}");
        }
        catch (InvalidOperationException)
        {
            // Output helper is no longer active (test has ended).
        }
        catch (Exception)
        {
            try
            {
                output?.WriteLine($"[WealthOpsTestDatabase {DateTime.UtcNow:HH:mm:ss.fff}] {message}");
            }
            catch (InvalidOperationException)
            {
            }
        }
    }
}
