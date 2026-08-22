# Arquitetura Compile-Time First para .NET

Referência v0.4 de arquitetura .NET fortemente tipada, simples para humanos e previsível para agentes de IA.

> Se uma inconsistência puder ser encontrada na compilação, ela não deve esperar até o runtime.

Este repositório não pretende ser um framework. A proposta é documentar um conjunto pequeno e coerente de decisões:

- **Well-Known First:** APIs, protocolos, tipos e convenções públicas conhecidas são usados
  diretamente quando resolvem adequadamente o problema;
- abstrações privadas precisam acrescentar semântica de produto, política, uma fronteira concreta,
  providers realmente suportados ou isolamento externo necessário;
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

APIs públicas formam uma **Public Semantic Surface**: significado que humanos, ferramentas e agentes
já conhecem fora do repositório. Isso reduz **Context Debt**, o conhecimento privado necessário antes
de alterar uma feature. A documentação oficial e a versão instalada continuam sendo autoridade; o
compilador, os analyzers e os testes validam o uso real.

`IReadQueryExecutor` é o exemplo oficial de abstração justificada: `IQueryable<T>` continua sendo a
linguagem pública de composição, enquanto o executor resolve somente a diferença concreta entre os
terminais assíncronos do EF Core e do OData no navegador.

Consulte [Architecture.md](Architecture.md), [AGENTS.md](AGENTS.md) e o
[ADR 0006](docs/adr/0006-well-known-first.md).
