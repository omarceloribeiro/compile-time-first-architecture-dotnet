using CompileTimeFirst.Sample.ReadModel;
using Microsoft.OData.Client;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace CompileTimeFirst.Sample.Web.Client.OData;

public sealed class ODataReadSchoolDbFactory(Uri serviceRoot)
    : IReadSchoolDbFactory, IReadProviderInfo
{
    public string Name => "Microsoft.OData.Client LINQ + browser HttpClient";

    public Task<IReadSchoolDbScope> CreateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadSchoolDbScope>(new ODataReadSchoolDbScope(serviceRoot));
    }
}

public sealed class ODataReadSchoolDbScope : IReadSchoolDbScope
{
    private readonly DataServiceContext _context;

    public ODataReadSchoolDbScope(Uri serviceRoot)
    {
        _context = new DataServiceContext(serviceRoot)
        {
            MergeOption = MergeOption.NoTracking
        };
    }

    public IQueryable<SubjectReadItem> Subjects => _context.CreateQuery<SubjectReadItem>("Subjects");
    public IQueryable<GradeReadItem> Grades => _context.CreateQuery<GradeReadItem>("Grades");
    public IQueryable<QuestionReadItem> Questions => _context.CreateQuery<QuestionReadItem>("Questions");
    public IQueryable<QuestionOptionReadItem> QuestionOptions =>
        _context.CreateQuery<QuestionOptionReadItem>("QuestionOptions");

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class ODataReadQueryExecutor(HttpClient httpClient) : IReadQueryExecutor
{
    // DataServiceQuery is deliberately used only for LINQ-to-OData translation. Materializing a
    // QueryOperationResponse can enter a synchronous wait that is unsupported in browser WebAssembly.
    public async Task<List<T>> ToListAsync<T>(
        IQueryable<T> query,
        CancellationToken cancellationToken = default)
    {
        var requestUri = GetDataServiceQuery(query).RequestUri;
        var results = new List<T>();

        while (requestUri is not null)
        {
            var page = await GetPageAsync<T>(requestUri, cancellationToken);
            results.AddRange(page.Value);
            requestUri = page.NextLink;
        }

        return results;
    }

    public async Task<T?> FirstOrDefaultAsync<T>(
        IQueryable<T> query,
        CancellationToken cancellationToken = default)
        => (await ToListAsync(query.Take(1), cancellationToken)).FirstOrDefault();

    public async Task<T?> SingleOrDefaultAsync<T>(
        IQueryable<T> query,
        CancellationToken cancellationToken = default)
        => (await ToListAsync(query.Take(2), cancellationToken)).SingleOrDefault();

    public async Task<int> CountAsync<T>(
        IQueryable<T> query,
        CancellationToken cancellationToken = default)
    {
        var requestUri = GetDataServiceQuery(query)
            .IncludeCount()
            .AddQueryOption("$top", 0)
            .RequestUri;
        var page = await GetPageAsync<T>(requestUri, cancellationToken);
        return checked((int)(page.Count
            ?? throw new InvalidOperationException("The OData response did not include @odata.count.")));
    }

    public async Task<bool> AnyAsync<T>(
        IQueryable<T> query,
        CancellationToken cancellationToken = default)
        => (await ToListAsync(query.Take(1), cancellationToken)).Count != 0;

    private static DataServiceQuery<T> GetDataServiceQuery<T>(IQueryable<T> query)
        => query as DataServiceQuery<T>
           ?? throw new InvalidOperationException(
               $"The query provider '{query.Provider.GetType().FullName}' is not an OData client provider.");

    private async Task<ODataPage<T>> GetPageAsync<T>(
        Uri requestUri,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            requestUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ODataPage<T>>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("The OData response body was empty.");
    }

    private sealed class ODataPage<T>
    {
        [JsonPropertyName("value")]
        public List<T> Value { get; init; } = [];

        [JsonPropertyName("@odata.count")]
        public long? Count { get; init; }

        [JsonPropertyName("@odata.nextLink")]
        public Uri? NextLink { get; init; }
    }
}
