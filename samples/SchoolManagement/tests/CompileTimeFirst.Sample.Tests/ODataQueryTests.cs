using CompileTimeFirst.Sample.Web.Client.OData;
using System.Net;
using System.Text;

namespace CompileTimeFirst.Sample.Tests;

public sealed class ODataQueryTests
{
    [Fact]
    public async Task Portable_subject_query_translates_filter_and_orderby()
    {
        var factory = new ODataReadSchoolDbFactory(new Uri("https://localhost/odata/"));
        await using var db = await factory.CreateAsync();

        var query = db.Subjects.Where(x => x.IsActive).OrderBy(x => x.Name);
        var uri = query.ToString();

        Assert.Contains("$filter", uri, StringComparison.Ordinal);
        Assert.Contains("$orderby", uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Browser_executor_materializes_all_server_driven_pages_with_http_client()
    {
        var requests = new List<Uri>();
        using var httpClient = new HttpClient(new StubHandler((request, requestNumber) =>
        {
            requests.Add(request.RequestUri!);
            var json = requestNumber == 1
                ? """
                  {
                    "value": [{ "Id": "11111111-1111-1111-1111-111111111111", "Name": "Computing", "IsActive": true }],
                    "@odata.nextLink": "https://localhost/odata/Subjects?$skip=1"
                  }
                  """
                : """
                  {
                    "value": [{ "Id": "22222222-2222-2222-2222-222222222222", "Name": "Mathematics", "IsActive": true }]
                  }
                  """;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }));
        var factory = new ODataReadSchoolDbFactory(new Uri("https://localhost/odata/"));
        var executor = new ODataReadQueryExecutor(httpClient);

        await using var db = await factory.CreateAsync();
        var subjects = await executor.ToListAsync(db.Subjects.OrderBy(x => x.Name));

        Assert.Equal(["Computing", "Mathematics"], subjects.Select(x => x.Name));
        Assert.Equal(2, requests.Count);
        Assert.Contains("$orderby", requests[0].Query, StringComparison.Ordinal);
        Assert.Contains("$skip=1", requests[1].Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Browser_executor_requests_only_one_page_and_its_total_count()
    {
        Uri? requestedUri = null;
        using var httpClient = new HttpClient(new StubHandler((request, _) =>
        {
            requestedUri = request.RequestUri;
            const string json = """
                {
                  "@odata.count": 3,
                  "value": [{ "Id": "22222222-2222-2222-2222-222222222222", "Name": "Mathematics", "IsActive": true }]
                }
                """;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }));
        var factory = new ODataReadSchoolDbFactory(new Uri("https://localhost/odata/"));
        var executor = new ODataReadQueryExecutor(httpClient);

        await using var db = await factory.CreateAsync();
        var page = await executor.ToPageAsync(
            db.Subjects.OrderBy(x => x.Name),
            skip: 1,
            take: 1);

        Assert.Equal(["Mathematics"], page.Items.Select(x => x.Name));
        Assert.Equal(3, page.TotalCount);
        Assert.NotNull(requestedUri);
        Assert.Contains("$count=true", requestedUri.Query, StringComparison.Ordinal);
        Assert.Contains("$skip=1", requestedUri.Query, StringComparison.Ordinal);
        Assert.Contains("$top=1", requestedUri.Query, StringComparison.Ordinal);
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, int, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        private int _requestCount;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = responseFactory(request, Interlocked.Increment(ref _requestCount));
            return Task.FromResult(response);
        }
    }
}
