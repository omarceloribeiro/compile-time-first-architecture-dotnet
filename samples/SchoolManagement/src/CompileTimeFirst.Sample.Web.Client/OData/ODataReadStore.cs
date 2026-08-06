using CompileTimeFirst.Sample.ReadModel;
using Microsoft.OData.Client;

namespace CompileTimeFirst.Sample.Web.Client.OData;

public sealed class ODataReadSchoolDbFactory(Uri serviceRoot, HttpClient? httpClient = null)
    : IReadSchoolDbFactory, IReadProviderInfo
{
    public string Name => "Microsoft.OData.Client (WebAssembly)";

    public Task<IReadSchoolDbScope> CreateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadSchoolDbScope>(new ODataReadSchoolDbScope(serviceRoot, httpClient));
    }
}

public sealed class ODataReadSchoolDbScope : IReadSchoolDbScope
{
    private readonly DataServiceContext _context;

    public ODataReadSchoolDbScope(Uri serviceRoot, HttpClient? httpClient)
    {
        _context = new DataServiceContext(serviceRoot)
        {
            MergeOption = MergeOption.NoTracking
        };

        if (httpClient is not null)
        {
            _context.HttpClientFactory = new FixedHttpClientFactory(httpClient);
        }
    }

    public IQueryable<SubjectReadItem> Subjects => _context.CreateQuery<SubjectReadItem>("Subjects");
    public IQueryable<GradeReadItem> Grades => _context.CreateQuery<GradeReadItem>("Grades");
    public IQueryable<QuestionReadItem> Questions => _context.CreateQuery<QuestionReadItem>("Questions");
    public IQueryable<QuestionOptionReadItem> QuestionOptions =>
        _context.CreateQuery<QuestionOptionReadItem>("QuestionOptions");

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private sealed class FixedHttpClientFactory(HttpClient httpClient) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => httpClient;
    }
}

public sealed class ODataReadQueryExecutor : IReadQueryExecutor
{
    public async Task<List<T>> ToListAsync<T>(
        IQueryable<T> query,
        CancellationToken cancellationToken = default)
    {
        var dataServiceQuery = GetDataServiceQuery(query);
        var response = (QueryOperationResponse<T>)await dataServiceQuery.ExecuteAsync(cancellationToken);
        var results = response.ToList();
        var continuation = response.GetContinuation();

        while (continuation is not null)
        {
            response = (QueryOperationResponse<T>)await dataServiceQuery.Context.ExecuteAsync(
                continuation,
                cancellationToken);
            results.AddRange(response);
            continuation = response.GetContinuation();
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
        var dataServiceQuery = GetDataServiceQuery(query).IncludeCount();
        var response = (QueryOperationResponse<T>)await dataServiceQuery.ExecuteAsync(cancellationToken);
        return checked((int)response.Count);
    }

    public async Task<bool> AnyAsync<T>(
        IQueryable<T> query,
        CancellationToken cancellationToken = default)
        => (await ToListAsync(query.Take(1), cancellationToken)).Count != 0;

    private static DataServiceQuery<T> GetDataServiceQuery<T>(IQueryable<T> query)
        => query as DataServiceQuery<T>
           ?? throw new InvalidOperationException(
               $"The query provider '{query.Provider.GetType().FullName}' is not an OData client provider.");
}
