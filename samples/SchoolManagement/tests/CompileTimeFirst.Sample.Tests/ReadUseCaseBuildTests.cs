using System.Diagnostics;
using System.Xml.Linq;
using CompileTimeFirst.Sample.Analyzers;
using CompileTimeFirst.Sample.Application;
using Microsoft.EntityFrameworkCore;

namespace CompileTimeFirst.Sample.Tests;

public sealed class ReadUseCaseBuildTests
{
    [Theory]
    [InlineData("db.SaveChanges();", "_ = db.Model;", ReadUseCaseAnalyzer.PersistenceId)]
    [InlineData("_ = db.Set<Row>().Count();", "_ = db.Set<Row>().AsNoTracking().Count();", ReadUseCaseAnalyzer.NoTrackingId)]
    public async Task Consumer_build_fails_on_read_violations_and_accepts_the_correction(
        string invalidStatement, string validStatement, string diagnosticId)
    {
        var directory = Directory.CreateTempSubdirectory("ctfa-read-build-");
        try
        {
            var project = new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                new XElement("PropertyGroup",
                    new XElement("TargetFramework", "net10.0"),
                    new XElement("ImplicitUsings", "enable")),
                new XElement("ItemGroup",
                    Reference(typeof(IReadUseCase).Assembly.Location),
                    Reference(typeof(DbContext).Assembly.Location),
                    new XElement("Analyzer", new XAttribute("Include", typeof(ReadUseCaseAnalyzer).Assembly.Location))));
            var projectPath = Path.Combine(directory.FullName, "Consumer.csproj");
            await File.WriteAllTextAsync(projectPath, project.ToString());
            var sourcePath = Path.Combine(directory.FullName, "Report.cs");
            var source = $$"""
                using CompileTimeFirst.Sample.Application;
                using Microsoft.EntityFrameworkCore;
                public sealed class Row { public int Id { get; set; } }
                public sealed class Report : IReadUseCase
                {
                    public void Run(DbContext db) { {{invalidStatement}} }
                }
                """;
            await File.WriteAllTextAsync(sourcePath, source);

            var invalid = await BuildAsync(projectPath);
            Assert.NotEqual(0, invalid.ExitCode);
            Assert.Contains($"error {diagnosticId}", invalid.Output, StringComparison.Ordinal);

            await File.WriteAllTextAsync(sourcePath, source.Replace(invalidStatement, validStatement, StringComparison.Ordinal));
            var valid = await BuildAsync(projectPath);
            Assert.True(valid.ExitCode == 0, valid.Output);
        }
        finally
        {
            // Delete only the fresh directory created for this test, inside the system temp root.
            var relative = Path.GetRelativePath(Path.GetFullPath(Path.GetTempPath()), directory.FullName);
            if (Path.IsPathRooted(relative) || relative.StartsWith("..", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Test directory escaped the temporary root.");
            }

            directory.Delete(recursive: true);
        }
    }

    private static XElement Reference(string path) =>
        new("Reference", new XAttribute("Include", Path.GetFileNameWithoutExtension(path)),
            new XElement("HintPath", path));

    private static async Task<(int ExitCode, string Output)> BuildAsync(string projectPath)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in new[] { "build", projectPath, "--nologo", "-v:minimal", "-p:NuGetAudit=false" })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw;
        }

        return (process.ExitCode, await output + await error);
    }
}
