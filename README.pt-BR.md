# Arquitetura Compile-Time First para .NET

Referência v0.3 de arquitetura .NET fortemente tipada, simples para humanos e previsível para agentes de IA.

> Se uma inconsistência puder ser encontrada na compilação, ela não deve esperar até o runtime.

Este repositório não pretende ser um framework. A proposta é documentar um conjunto pequeno e coerente de decisões:

- escritas passam por casos de uso fortemente tipados;
- leituras incidentais da interface usam um banco somente leitura com `IQueryable<T>`;
- toda execução terminal de leitura incidental usa `IReadQueryExecutor`;
- grids, tabelas, listas de resultados, autocompletes e históricos usam `ToPageAsync`;
- `IQueryable<T>`, contexto e read scope permanecem locais à operação;
- dashboards, indicadores, relatórios e exportações são casos de uso de leitura;
- `IDbContextFactory` cria um contexto por operação;
- ViewModels permanecem livres para mudar com a tela;
- contratos da aplicação permanecem estáveis;
- Server e WebAssembly podem compartilhar consultas LINQ portáveis por providers diferentes;
- a spec escolhe o componente e o agente não inventa limites ou comportamento adaptativo;
- o agente deve compilar, testar e corrigir antes de entregar.

Consulte [Architecture.md](Architecture.md), [AGENTS.md](AGENTS.md) e a análise histórica em [docs/HISTORY-AND-ANALYSIS.pt-BR.md](docs/HISTORY-AND-ANALYSIS.pt-BR.md).
