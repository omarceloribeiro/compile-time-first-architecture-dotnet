# Changelog

## v0.4

- adopts Well-Known First as a core architecture and agent rule;
- defines Public Semantic Surface and Context Debt;
- prefers suitable public APIs, protocols, types and conventions over mechanical private wrappers;
- requires concrete product meaning, policy, provider variation or an architectural boundary before adding an abstraction;
- documents `IReadQueryExecutor` as a narrow, justified exception at the provider-specific async terminal boundary;
- relates Well-Known First to reads, UI, dependency injection, integrations and regenerable vendor boundaries;
- keeps official documentation and the installed package version authoritative over remembered API knowledge;
- records why v0.4 does not add a context-blind wrapper-detector analyzer.

## v0.3

- makes `IReadQueryExecutor` the terminal path for every incidental UI read;
- adds `PageResult<T>` and provider-independent `ToPageAsync`;
- keeps `IQueryable<T>`, read scopes and DbContexts local to one operation;
- forbids binding live query providers to visual components;
- maps UI controls from the feature specification to explicit query terminals;
- pages sample tables and result lists before materialization;
- validates EF Core and browser OData paging with contract and end-to-end tests;
- preserves the v0 write, business-read, DI-validation and optional Auto/OData foundations.
