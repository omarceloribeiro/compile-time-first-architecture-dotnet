# Arquitetura Compile-Time First para .NET

Referência v0.5 de arquitetura .NET fortemente tipada, simples para humanos e previsível para agentes de IA.

> Se uma inconsistência puder ser encontrada na compilação, ela não deve esperar até o runtime.

Este repositório não pretende ser um framework. A proposta é documentar um conjunto pequeno e coerente de decisões:

- **Well-Known First:** APIs, protocolos, tipos e convenções públicas conhecidas são usados
  diretamente quando resolvem adequadamente o problema;
- abstrações privadas precisam acrescentar semântica de produto, política, uma fronteira concreta,
  providers realmente suportados ou isolamento externo necessário;
- alterações de estado do produto passam por casos de uso fortemente tipados;
- leituras incidentais da interface usam um banco somente leitura com `IQueryable<T>`;
- toda execução terminal de leitura incidental usa `IReadQueryExecutor`;
- grids, tabelas, listas de resultados, autocompletes e históricos usam `ToPageAsync`;
- `IQueryable<T>`, contexto e read scope permanecem locais à operação;
- dashboards, indicadores, relatórios e exportações são casos de uso de leitura;
- `IDbContextFactory` cria um contexto por operação;
- o estado de tela vive no próprio componente e muda junto com a tela; não há camada de ViewModel;
- contratos da aplicação permanecem estáveis;
- Server e WebAssembly podem compartilhar consultas LINQ portáveis por providers diferentes,
  em um caminho ainda experimental;
- a spec escolhe o componente e o agente não inventa limites ou comportamento adaptativo;
- o agente deve compilar, testar e corrigir antes de entregar.

APIs públicas formam uma **Public Semantic Surface**: significado que humanos, ferramentas e agentes
já conhecem fora do repositório. Isso reduz **Context Debt**, o conhecimento privado necessário antes
de alterar uma feature. A documentação oficial e a versão instalada continuam sendo autoridade; o
compilador, os analyzers e os testes validam o uso real.

`IReadQueryExecutor` é o exemplo oficial de abstração justificada: `IQueryable<T>` continua sendo a
linguagem pública de composição, enquanto o executor resolve somente a diferença concreta entre os
terminais assíncronos do EF Core e do OData no navegador.

O guia [Well-Known First e transparência semântica](docs/WELL-KNOWN-FIRST.md) detalha o custo de uma
linguagem privada, o papel do design system e como o uso explícito de uma biblioteca visual pode
tornar uma futura migração mais mecânica para agentes de IA.

Este repositório é deliberadamente específico para .NET. Perfis futuros de ASP.NET Core API, Razor
Pages e MVC podem entrar como projetos irmãos compartilhando as class libraries centrais. Perfis de
Java, Rust, Go e Python devem usar repositórios próprios, com regras, build e toolchains idiomáticos.

## Experimental: Interactive Auto, WebAssembly e OData

**`CompileTimeFirst.Sample.BlazorServer` é o caminho suportado.** Ele usa Interactive Server global;
o layout e seu `ErrorBoundary` genérico formam uma única árvore interativa, sem dependências de
WebAssembly ou OData.

Interactive Auto, WebAssembly e OData no navegador continuam experimentais. Eles permanecem na
mesma solution para cobertura de build e testes, mas estão fisicamente isolados nos projetos
`CompileTimeFirst.Sample.BlazorAuto` e `CompileTimeFirst.Sample.BlazorAuto.Client`. Um único
componente ainda exercita Server e WebAssembly sem acoplar o host suportado ao spike. Nada novo é
gerado nesse caminho sem uma spec que peça Interactive Auto explicitamente. Veja a seção
"Experimental render modes" em [AGENTS.md](AGENTS.md) e o [ADR 0009](docs/adr/0009-experimental-render-modes.md).

O sample valida autenticação ASP.NET Core Identity no mesmo domínio e propagação do tenant por uma
claim emitida pelo servidor. Antes de habilitar em produção, ainda valide a exposição e os limites
das consultas OData, o ciclo de metadados do cliente gerado e a compatibilidade com trimming/AOT.

Consulte também [Architecture.md](Architecture.md), [AGENTS.md](AGENTS.md) e o
[ADR 0006](docs/adr/0006-well-known-first.md).
