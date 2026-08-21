# Changelog

## v0.3

- makes `IReadQueryExecutor` the terminal path for every incidental UI read;
- adds `PageResult<T>` and provider-independent `ToPageAsync`;
- keeps `IQueryable<T>`, read scopes and DbContexts local to one operation;
- forbids binding live query providers to visual components;
- maps UI controls from the feature specification to explicit query terminals;
- pages sample tables and result lists before materialization;
- validates EF Core and browser OData paging with contract and end-to-end tests;
- preserves the v0 write, business-read, DI-validation and optional Auto/OData foundations.
