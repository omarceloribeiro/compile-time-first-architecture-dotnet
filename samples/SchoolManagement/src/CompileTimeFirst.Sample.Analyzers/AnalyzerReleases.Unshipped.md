; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
CTFA001 | Architecture | Error | ViewModels and Blazor components cannot inject write DbContext
CTFA002 | Architecture | Error | ViewModels must use IReadQueryExecutor.ToListAsync() instead of EF Core ToListAsync()
CTFA003 | Architecture | Error | ViewModels must use IReadQueryExecutor.FirstOrDefaultAsync() instead of EF Core FirstOrDefaultAsync()
