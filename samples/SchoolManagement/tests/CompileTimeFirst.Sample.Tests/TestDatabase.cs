using CompileTimeFirst.Sample.Data;
using CompileTimeFirst.Sample.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CompileTimeFirst.Sample.Tests;

internal sealed class TestCurrentUser(Guid? tenantId) : ICurrentUser
{
    public Guid? TenantId { get; set; } = tenantId;
}

/// <summary>
/// One SQLite in-memory database per test, with two tenants already present.
///
/// A relational provider is used deliberately: the composite foreign keys that make a cross-tenant
/// reference impossible are only enforced by a database that enforces foreign keys, so the
/// in-memory provider could not demonstrate the guarantee the architecture claims.
/// </summary>
internal sealed class TestDatabase : IAsyncDisposable
{
    public static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly SqliteConnection _keeperConnection;

    private TestDatabase(
        SqliteConnection keeperConnection,
        DbContextOptions<SchoolDbContext> writeOptions,
        DbContextOptions<ReadOnlySchoolDbContext> readOptions,
        TestCurrentUser currentUser)
    {
        _keeperConnection = keeperConnection;
        WriteOptions = writeOptions;
        ReadOptions = readOptions;
        CurrentUser = currentUser;
    }

    public DbContextOptions<SchoolDbContext> WriteOptions { get; }

    public DbContextOptions<ReadOnlySchoolDbContext> ReadOptions { get; }

    public TestCurrentUser CurrentUser { get; }

    public IDbContextFactory<SchoolDbContext> WriteFactory =>
        new TenantSchoolDbContextFactory(WriteOptions, CurrentUser);

    public IDbContextFactory<ReadOnlySchoolDbContext> ReadFactory =>
        new TenantReadOnlySchoolDbContextFactory(ReadOptions, CurrentUser);

    public static async Task<TestDatabase> CreateAsync(Guid? tenantId = null)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"CompileTimeFirst.Tests.{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared
        }.ToString();
        var keeperConnection = new SqliteConnection(connectionString);
        await keeperConnection.OpenAsync();

        var writeOptions = new DbContextOptionsBuilder<SchoolDbContext>()
            .UseSqlite(connectionString)
            .Options;
        var readOptions = new DbContextOptionsBuilder<ReadOnlySchoolDbContext>()
            .UseSqlite(connectionString)
            .Options;

        var currentUser = new TestCurrentUser(tenantId ?? TenantA);
        var database = new TestDatabase(keeperConnection, writeOptions, readOptions, currentUser);

        await using var seed = new SchoolDbContext(writeOptions);
        await seed.Database.EnsureCreatedAsync();
        seed.Tenants.AddRange(new Tenant(TenantA, "Tenant A"), new Tenant(TenantB, "Tenant B"));
        await seed.SaveChangesAsync();

        return database;
    }

    /// <summary>
    /// A context that can see every tenant, for arranging and asserting across the boundary the
    /// production code is not allowed to cross.
    /// </summary>
    public SchoolDbContext CreateUnfilteredContext() => new(WriteOptions) { TenantId = null };

    public SchoolDbContext CreateContextFor(Guid tenantId) => new(WriteOptions) { TenantId = tenantId };

    public async ValueTask DisposeAsync()
    {
        await _keeperConnection.DisposeAsync();
    }
}
