using CompileTimeFirst.Sample.Application;
using CompileTimeFirst.Sample.Application.Dashboard;
using CompileTimeFirst.Sample.Application.Exports;
using CompileTimeFirst.Sample.Application.Grades;
using CompileTimeFirst.Sample.Application.QuestionOptions;
using CompileTimeFirst.Sample.Application.Questions;
using CompileTimeFirst.Sample.Application.Subjects;
using CompileTimeFirst.Sample.Data;
using CompileTimeFirst.Sample.ReadModel;
using CompileTimeFirst.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CompileTimeFirst.Sample.ConsoleApp;

public static class CompositionRoot
{
    private const string DatabaseName = "compile-time-first-school";

    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        var databaseRoot = new InMemoryDatabaseRoot();

        var writeOptions = new DbContextOptionsBuilder<SchoolDbContext>()
            .UseInMemoryDatabase(DatabaseName, databaseRoot)
            .Options;
        var readOptions = new DbContextOptionsBuilder<ReadOnlySchoolDbContext>()
            .UseInMemoryDatabase(DatabaseName, databaseRoot)
            .Options;

        services.AddSingleton<IDbContextFactory<SchoolDbContext>>(
            new SimpleDbContextFactory<SchoolDbContext>(() => new SchoolDbContext(writeOptions)));
        services.AddSingleton<IDbContextFactory<ReadOnlySchoolDbContext>>(
            new SimpleDbContextFactory<ReadOnlySchoolDbContext>(() => new ReadOnlySchoolDbContext(readOptions)));

        services.AddSingleton<IReadSchoolDbFactory, ReadSchoolDbFactory>();
        services.AddSingleton<IReadQueryExecutor, EfReadQueryExecutor>();

        services.AddScoped<ICreateSubjectUseCase, CreateSubjectUseCase>();
        services.AddScoped<ICreateGradeUseCase, CreateGradeUseCase>();
        services.AddScoped<ICreateQuestionUseCase, CreateQuestionUseCase>();
        services.AddScoped<ICreateQuestionOptionUseCase, CreateQuestionOptionUseCase>();
        services.AddScoped<IGetSchoolDashboardUseCase, GetSchoolDashboardUseCase>();
        services.AddScoped<IExportQuestionsUseCase, ExportQuestionsUseCase>();
        services.AddScoped<QuestionEditorViewModel>();
        services.AddScoped<SampleRunner>();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    public static void Validate(IServiceProvider provider)
    {
        DependencyInjectionGraphValidator.Validate(
            provider,
            new DependencyInjectionValidationOptions(
                Assemblies:
                [
                    typeof(IUseCase).Assembly,
                    typeof(QuestionEditorViewModel).Assembly
                ],
                MarkerInterfaces:
                [
                    typeof(IUseCase),
                    typeof(IViewModel)
                ],
                ValidateBlazorComponents: false));
    }
}
