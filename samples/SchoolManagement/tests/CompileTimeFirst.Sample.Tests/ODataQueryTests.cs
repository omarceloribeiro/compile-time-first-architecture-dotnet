using CompileTimeFirst.Sample.Web.Client.OData;

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
}
