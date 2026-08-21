using CompileTimeFirst.Sample.Data;
using CompileTimeFirst.Sample.Domain;
using Microsoft.EntityFrameworkCore;

namespace CompileTimeFirst.Sample.Tests;

public sealed class ReadQueryExecutorTests
{
    [Fact]
    public async Task Ef_executor_supports_all_contract_terminals()
    {
        var options = new DbContextOptionsBuilder<SchoolDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new SchoolDbContext(options);
        db.Grades.AddRange(
            new Grade { Id = Guid.NewGuid(), Name = "One", Order = 1 },
            new Grade { Id = Guid.NewGuid(), Name = "Two", Order = 2 },
            new Grade { Id = Guid.NewGuid(), Name = "Three", Order = 3 });
        await db.SaveChangesAsync();

        var values = db.Grades.OrderBy(x => x.Order).Select(x => x.Order);
        var executor = new EfReadQueryExecutor();

        Assert.Equal(new[] { 1, 2, 3 }, await executor.ToListAsync(values));
        var page = await executor.ToPageAsync(values, skip: 1, take: 1);
        Assert.Equal([2], page.Items);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(1, await executor.FirstOrDefaultAsync(values));
        Assert.Equal(2, await executor.SingleOrDefaultAsync(values.Where(x => x == 2)));
        Assert.Equal(3, await executor.CountAsync(values));
        Assert.True(await executor.AnyAsync(values.Where(x => x == 3)));
    }
}
