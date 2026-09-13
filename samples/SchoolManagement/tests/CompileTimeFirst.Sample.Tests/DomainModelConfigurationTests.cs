using CompileTimeFirst.Sample.Data;
using CompileTimeFirst.Sample.Domain;
using Microsoft.EntityFrameworkCore;

namespace CompileTimeFirst.Sample.Tests;

public sealed class DomainModelConfigurationTests
{
    [Fact]
    public async Task Write_and_read_contexts_map_the_same_entities_and_relationships()
    {
        await using var database = await TestDatabase.CreateAsync();
        using var write = new SchoolDbContext(database.WriteOptions);
        using var read = new ReadOnlySchoolDbContext(database.ReadOptions);

        Assert.Equal(Describe(write), Describe(read));
    }

    // A shared configuration is only shared if both models come out identical. Comparing the built
    // models is what catches a relationship configured on one context and forgotten on the other.
    private static List<string> Describe(DbContext context) =>
        [.. context.Model
            .GetEntityTypes()
            .Where(entityType => entityType.ClrType.Namespace == typeof(Tenant).Namespace)
            .SelectMany(entityType =>
                new[] { $"{entityType.ClrType.Name}:table:{entityType.GetTableName()}" }
                    .Concat(entityType.GetProperties()
                    .Select(property =>
                        $"{entityType.ClrType.Name}.{property.Name}:{property.ClrType.Name}:" +
                        $"{property.IsNullable}:{property.GetMaxLength()}:{property.IsConcurrencyToken}")
                    .Concat(entityType.GetForeignKeys()
                        .Select(foreignKey =>
                            $"{entityType.ClrType.Name}->{foreignKey.PrincipalEntityType.ClrType.Name}:" +
                            $"{string.Join(",", foreignKey.Properties.Select(p => p.Name))}:" +
                            $"{foreignKey.DeleteBehavior}"))))
            .OrderBy(value => value, StringComparer.Ordinal)];
}
