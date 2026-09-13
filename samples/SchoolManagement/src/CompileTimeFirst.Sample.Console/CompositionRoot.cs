using CompileTimeFirst.Sample.Application;
using CompileTimeFirst.Sample.Application.Dashboard;
using CompileTimeFirst.Sample.Application.Exports;
using CompileTimeFirst.Sample.Application.Grades;
using CompileTimeFirst.Sample.Application.QuestionOptions;
using CompileTimeFirst.Sample.Application.Questions;
using CompileTimeFirst.Sample.Application.Subjects;
using CompileTimeFirst.Sample.Data;
using CompileTimeFirst.Sample.Domain;
using CompileTimeFirst.Sample.ReadModel;
using CompileTimeFirst.Validation;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CompileTimeFirst.Sample.ConsoleApp;

public static class CompositionRoot
{
    public static readonly Guid DemoTenantId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"CompileTimeFirst.Console.{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        // The keeper owns only the database lifetime. Every context created by a factory opens an
        // independently owned connection to the same named in-memory database.
        services.AddSingleton(_ =>
        {
            var keeperConnection = new SqliteConnection(connectionString);
            keeperConnection.Open();
            return keeperConnection;
        });

        var writeOptions = new DbContextOptionsBuilder<SchoolDbContext>()
            .UseSqlite(connectionString)
            .Options;
        var readOptions = new DbContextOptionsBuilder<ReadOnlySchoolDbContext>()
            .UseSqlite(connectionString)
            .Options;

        // A non-interactive host has no user to resolve a tenant from, so it declares one.
        services.AddSingleton<ICurrentUser>(new FixedTenant(DemoTenantId));

        services.AddScoped<IDbContextFactory<SchoolDbContext>>(provider =>
            new TenantSchoolDbContextFactory(writeOptions, provider.GetRequiredService<ICurrentUser>()));
        services.AddScoped<IDbContextFactory<ReadOnlySchoolDbContext>>(provider =>
            new TenantReadOnlySchoolDbContextFactory(readOptions, provider.GetRequiredService<ICurrentUser>()));

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IReadSchoolDbFactory, ReadSchoolDbFactory>();
        services.AddSingleton<IReadQueryExecutor, EfReadQueryExecutor>();

        services.AddScoped<ICreateSubjectUseCase, CreateSubjectUseCase>();
        services.AddScoped<ICreateGradeUseCase, CreateGradeUseCase>();
        services.AddScoped<ICreateQuestionUseCase, CreateQuestionUseCase>();
        services.AddScoped<ICreateQuestionOptionUseCase, CreateQuestionOptionUseCase>();
        services.AddScoped<IGetSchoolDashboardUseCase, GetSchoolDashboardUseCase>();
        services.AddScoped<IExportQuestionsUseCase, ExportQuestionsUseCase>();
        services.AddScoped<SampleRunner>();

        var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        _ = serviceProvider.GetRequiredService<SqliteConnection>();
        return serviceProvider;
    }

    public static void Validate(IServiceProvider provider)
    {
        DependencyInjectionGraphValidator.Validate(
            provider,
            new DependencyInjectionValidationOptions(
                Assemblies:
                [
                    typeof(IUseCase).Assembly,
                    typeof(SampleRunner).Assembly
                ],
                MarkerInterfaces: [typeof(IUseCase)],
                ValidateBlazorComponents: false));
    }

    private sealed class FixedTenant(Guid tenantId) : ICurrentUser
    {
        public Guid? TenantId { get; } = tenantId;
    }
}
