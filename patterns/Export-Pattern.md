# Export Pattern

1. The use case validates actor, tenant, period and filters.
2. It queries through the read model.
3. It builds one strongly typed report model.
4. It delegates formatting to an exporter.
5. It returns a file result or a streaming handle.

Do not create one use case per format when the business report is the same.
