# Well-Known First, semantic transparency and regenerable boundaries

## The principle

> Prefer public, well-known constructs because they minimize the private context required for a
> human or coding agent to understand, generate, validate and migrate software.

The complementary rule is:

> Do not hide a suitable public abstraction behind a private abstraction unless the private
> abstraction adds concrete product meaning or owns a real boundary that exists now.

Well-Known First applies to APIs, protocols, types, conventions and established libraries. It is not
limited to methods whose language accessibility is `public`, and it does not mean that the most
popular dependency is automatically the right dependency.

## The private-language problem

Conventional architectures often introduce types such as:

```text
ICustomHttpClient
IGenericRepository
IQueryService
IGridAdapter
IAuthenticationGateway
IDatabaseReader
IAppLogger
IButton
IUiGrid
```

Sometimes those abstractions own real product or architecture semantics. Frequently, however, they
only rename a public API that already expresses the capability.

Every mechanical abstraction creates a private repository language. Before changing a feature, a
human or agent must then:

```text
find the abstraction
  → open its contract
  → locate its implementation
  → identify the underlying library
  → discover which capabilities were exposed
  → discover which capabilities were hidden
  → learn local conventions
  → only then change the feature
```

When the public API is already adequate, this cost is accidental.

For example, `HttpClient` has widely documented meaning. Replacing it without a current need with:

```csharp
IProjectRemoteRequestExecutor
```

does not add business knowledge. It adds private context. A client such as
`IStudentInformationSystemClient`, on the other hand, may be justified when it owns a concrete
external capability, authentication, normalization, versioning or protocol behavior.

This difference is the core of **Context Debt**: the amount of private knowledge that must be loaded
before the software can be modified correctly.

## Semantic transparency

**Semantic transparency** means keeping a regenerable boundary explicit enough that its technology
and behavior are immediately recognizable.

For regenerable code:

> Prefer explicit, well-known APIs to private abstractions that hide technology without adding
> product meaning.

This produces a useful separation:

```text
public mechanics
  + private product meaning
  + compiler/build validation
  = low-context architecture
```

Well-known public constructs provide a semantic surface already shared by documentation, IDEs,
compilers, analyzers, search tools, examples and coding models. They behave like preloaded semantic
context for an agent, but they are not literally an MCP and are not infallible memory. Official
documentation and the installed version remain authoritative.

## Read boundaries that survive Well-Known First

The provider-independent read stack deliberately combines public mechanics with narrow private
boundaries:

| Construct | Responsibility |
|---|---|
| `IQueryable<T>` and standard LINQ | Public query-composition language |
| `IReadDb` | Approved provider-independent read surface |
| `IReadDbFactory` | Operation-scoped creation, lifetime and provider boundary |
| `IReadQueryExecutor` | Provider-specific async terminals and materialization |

`IReadDb` and `IReadDbFactory` do not exist merely to rename an EF Core `DbContext` and its factory.
The same feature may receive an EF-backed read surface in Interactive Server and a remote/OData-backed
surface in WebAssembly. The private contracts keep provider creation and lifetime out of the
ViewModel while preserving `IQueryable<T>` and standard LINQ as the visible composition language.

Replacing them only in Server code with:

```csharp
IDbContextFactory<ReadOnlyDbContext>
```

would make the feature depend on an EF-only construction contract and would reintroduce a different
read path for Server, WebAssembly and Interactive Auto.

`IReadQueryExecutor` owns a different boundary. Query composition is portable, but EF Core and
browser OData do not expose one provider-neutral asynchronous terminal API. The executor owns only
that terminal/materialization difference.

Together:

```text
IReadDb / IReadDbFactory
  → approved read surface + operation lifetime + provider creation

IReadQueryExecutor
  → async terminal + materialization

IQueryable<T> / LINQ
  → public composition language retained directly
```

## UI example: public component to public component

Consider an application whose selected UI library is Radzen:

```razor
<RadzenDataGrid Data="@Students"
                AllowFiltering="true"
                AllowSorting="true"
                AllowPaging="true">
    ...
</RadzenDataGrid>
```

An agent can recognize immediately:

```text
Origin: RadzenDataGrid
Capabilities:
  - grid
  - filtering
  - sorting
  - paging
  - column templates
```

During a future migration it can investigate a public-to-public translation:

```text
RadzenDataGrid → Bit DataGrid
RadzenDataGrid → FluentDataGrid
RadzenDataGrid → a React grid such as TanStack or AG Grid
```

The exact target API still has to be verified against its official documentation and installed
version. The architectural advantage is that both sides of the translation are public and
discoverable.

Compare that with:

```razor
<ProjectUniversalGrid Definition="@GridDefinition"
                      Provider="@StudentsProvider"
                      Behaviors="@DefaultBehaviors" />
```

Before a migration can begin, the agent must answer what `ProjectUniversalGrid` means. It may need to
inspect:

```text
ProjectUniversalGrid
GridDefinition
IGridProvider
GridBehavior
RadzenGridAdapter
configuration
extension methods
  → finally reach RadzenDataGrid
```

A private language has been placed between two public languages. The wrapper may be legitimate, but
it now carries the burden of proving what semantic value justified that extra translation step.

The same distinction applies to selectors:

```razor
<RadzenDropDown ... />
```

is semantically transparent. A component named:

```razor
<ProjectSmartSelector ... />
```

is private and ambiguous if it only hides `RadzenDropDown`. A component such as:

```razor
<ProjectStudentEnrollmentSelector ... />
```

may be valuable because it expresses a real product capability. Its purpose is not vendor hiding;
its purpose is product meaning.

## Migration is a good AI task

A future UI migration can begin with an explicit capability matrix. For example:

```text
RadzenButton        → candidate target button
RadzenTextBox       → candidate target text field
RadzenDropDown      → candidate target dropdown
RadzenCheckBox      → candidate target checkbox
RadzenDataGrid      → candidate target data grid
RadzenDialog        → candidate target modal
RadzenNotification  → candidate target message/toast
```

This matrix is an analysis artifact, not an assertion that names or behavior are one-to-one. The
agent verifies the destination library before changing code.

The migration loop is mechanical and compiler-guided:

```text
1. locate uses of one public source component
2. identify the property and template combinations in use
3. map them to the verified destination API
4. convert one coherent slice
5. build
6. correct compiler, analyzer and test feedback
7. continue with the next slice
```

Compile-Time First closes the loop. Direct vendor usage does not prevent migration; it makes the
source language explicit enough to automate much of it.

## Complex components are behavior translations

A complex grid is rarely a textual one-to-one conversion. Suppose the source grid provides:

```text
server paging
filtering
sorting
edit templates
row selection
column templates
```

The agent first extracts a provider-neutral behavior model:

```text
Data source  → Students
Pagination   → server-side
Sort         → Name, Registration
Filter       → Name, Status
Editing      → inline
Selection    → multiple
Templates    → identified per column
```

It then implements those capabilities with the verified destination API. Semantic transparency
does not promise a trivial rename; it preserves enough visible meaning for a reliable de/para.

## Explicit and localized vendor coupling

There is no architectural shame in a frontend that is explicitly built with one selected component
library. The important boundary is localization:

```text
Domain
Application
Use cases
────────────────────────────────
do not know the UI vendor

Web / UI
  → RadzenLayout
  → RadzenDataGrid
  → RadzenDropDown
  → RadzenScheduler
  → RadzenDialog
  → RadzenChart
  → RadzenButton
```

An agent can inspect this boundary and conclude immediately: “This is a Blazor frontend built with
Radzen.” That is better than discovering the same fact only after traversing a large internal
component framework.

Therefore:

> Explicit vendor coupling, localized to a regenerable boundary, can be cheaper than artificial
> vendor independence.

Domain, Application, use cases, security rules and durable contracts remain protected. Razor,
component-library calls, visual state and screen-specific ViewModels may be regenerated or migrated
with the UI.

## The role of a design system

A `/design-system` remains valuable, but not as a vendor-hiding layer. It documents **how the product
uses the selected public library**.

For an application profile that selects Radzen and a theme such as Humanistic, it may document:

```text
Dropdown
  → RadzenDropDown
  → filtering behavior and any product-specified threshold
  → standard width
  → label placement
  → validation-message placement
  → product design tokens
```

The threshold must come from a product or feature specification; an agent does not invent it from an
estimated row count. The design system is an executable and visual specification of the desired
result. During migration it tells the agent what behavior and appearance must survive.

An application-specific UI profile may therefore be:

```text
Features
  ↓
selected public component library directly
  ↓
selected public theme
  ↓
product design tokens

/design-system
  → approved usage and visual reference

Application and Domain
  → no UI-vendor dependency
```

## Authentication example

ASP.NET Core Identity already exposes well-known concepts:

```text
PasswordSignInAsync
UserManager<TUser>
SignInManager<TUser>
IdentityRole
AuthorizeView
[Authorize]
AuthenticationStateProvider
```

Introducing all of the following merely to rename standard Identity mechanics adds a private
language:

```text
AuthenticateProjectUserUseCase
IAuthenticationGateway
IdentityAuthenticationAdapter
LoginResultFactory
```

Framework-owned sign-in, password, token, lockout, claims/roles primitives and authentication-state
mechanics may use the public Identity APIs directly at the trusted delivery/infrastructure boundary.
This does not bypass the rule that product and domain state changes use their authoritative
application boundary.

Product operations such as invitation, provisioning, tenant linkage, access
activation/deactivation or eligibility remain application use cases when they carry product
semantics. The product decision to grant or revoke a role may therefore belong to a use case even
though the implementation uses `UserManager<TUser>` and other public Identity APIs.

## Documentation ownership

To avoid turning Well-Known First itself into documentation Context Debt, each document has one
level of responsibility:

```text
README
  → short definition and discovery

Architecture.md
  → principle and architectural consequences

AGENTS.md
  → concise operational rule

ADR 0006
  → decision, rationale and trade-offs

this guide
  → canonical detailed explanation and examples
```

Future additions should extend this guide and link to it instead of copying long examples into every
document.

## Agent review questions

Before creating a private abstraction, ask:

1. What public construct already represents this capability?
2. What current limitation makes direct use insufficient?
3. What product meaning, policy or lifecycle boundary does the private type add?
4. Does it coordinate real provider variation or only hypothetical variation?
5. Does the change reduce or increase the number of concepts and files an agent must inspect?
6. Will a future migration see the real source technology or first have to reverse-engineer it?
7. Can the rule be enforced semantically, or must it remain an explicit review decision?

If the abstraction only forwards, renames or hides a suitable public API, prefer the public API.

## What this does not mean

Well-Known First does not mean:

- never create interfaces;
- use any popular library;
- place infrastructure inside Domain;
- accept vendor coupling in durable business layers;
- ignore testability, security, maintenance, licensing or versioning;
- assume remembered API knowledge is current;
- remove product components that genuinely express product meaning.

The concise rule remains:

> Public mechanics. Private business meaning.
