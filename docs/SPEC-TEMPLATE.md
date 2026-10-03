# SPEC-XXX — Nome do caso de uso

## Ator

## Objetivo

## Contexto

## Classificação e acesso aos dados

Informe `IReadUseCase` ou `IWriteUseCase`. Leituras de negócio usam, por padrão, o modelo original
e a mesma factory de contexto das escritas, com terminais EF e projeção direta no resultado.
Use `AsNoTracking()` (ou `AsNoTrackingWithIdentityResolution()`) imediatamente após cada origem da
consulta, antes de compor ou guardar em variável, inclusive para contagens e projeções só de valores.
Em `FromSql*`, aplique após essa chamada. CTFA009 acusa as omissões diretas e o uso de `AsTracking`;
helpers e fluxos de consultas fora dessa cobertura continuam sujeitos à convenção e ao review.
O analyzer também bloqueia chamadas diretas de persistência nos reads. Consultas incidentais usam
a superfície de leitura e o executor.

Para um modelo de leitura de negócio separado com executor, referencie a decisão inicial de CQRS
médio/forte do projeto ou um ADR posterior aprovado. Não deduza CQRS do frontend ou dos endpoints.

## Pré-condições

## Fluxo principal

1.

## Fluxos alternativos

## Regras de negócio

## Autorização e isolamento

## Request

## Result

## Controles e carregamento de dados

Para cada seletor, grid, tabela, lista, autocomplete ou histórico, informe o componente esperado.
Quando houver comportamento adaptativo, informe também o limite exato. O agente não deve trocar o
componente com base em uma estimativa própria de volume.

## Critérios de aceite

- [ ]

## Testes mínimos

## Dependências

Ao introduzir uma abstração privada relevante, informe qual limitação atual da API pública ela
resolve e qual semântica de produto, política ou fronteira concreta ela acrescenta. Não crie a
abstração apenas para renomear uma API conhecida ou preparar uma variação hipotética.

## Fora do escopo

## Data Specs relacionadas
