; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
CTFA001 | Architecture | Error | Blazor components cannot inject write DbContext
CTFA002 | Architecture | Error | Blazor components must use IReadQueryExecutor.ToListAsync() instead of EF Core ToListAsync()
CTFA003 | Architecture | Error | Blazor components must use IReadQueryExecutor.FirstOrDefaultAsync() instead of EF Core FirstOrDefaultAsync()
CTFA004 | Architecture | Error | Blazor components cannot store IQueryable or read scopes as UI state
CTFA005 | Architecture | Error | Blazor components must use IReadQueryExecutor for SingleOrDefaultAsync(), CountAsync() and AnyAsync()
CTFA006 | Architecture | Error | Concrete use cases must be classified as either read or write
CTFA007 | Architecture | Error | Read use cases cannot call or capture EF persistence methods
CTFA009 | Architecture | Error | Read queries require an immediate no-tracking modifier at their source and cannot use AsTracking
