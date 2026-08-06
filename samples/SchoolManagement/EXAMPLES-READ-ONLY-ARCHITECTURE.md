# Exemplos Práticos: Arquitetura Read-Only

Este arquivo mostra exemplos **CORRETOS** e **INCORRETOS** de código, com os erros que seriam gerados pelos analyzers.

## ✅ Exemplo 1: ViewModel Correto

```csharp
using CompileTimeFirst.Sample.Data;

namespace CompileTimeFirst.Sample.Web.Components.Pages.Products;

public class ProductsViewModel
{
    private readonly IReadSchoolDbFactory _readDbFactory;
    private readonly IReadQueryExecutor _queryExecutor;
    private readonly ICreateProductUseCase _createProductUseCase;

    // ✅ CORRETO: Injeta apenas abstrações de leitura e UseCases
    public ProductsViewModel(
        IReadSchoolDbFactory readDbFactory,
        IReadQueryExecutor queryExecutor,
        ICreateProductUseCase createProductUseCase)
    {
        _readDbFactory = readDbFactory;
        _queryExecutor = queryExecutor;
        _createProductUseCase = createProductUseCase;
    }

    public List<ProductReadItem> Items { get; private set; } = new();
    public string NewName { get; set; } = string.Empty;
    public bool IsBusy { get; private set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    // ✅ CORRETO: Usa IReadQueryExecutor.ToListAsync()
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            using var db = await _readDbFactory.CreateAsync();
            Items = await _queryExecutor.ToListAsync(
                db.Products.Where(p => p.IsActive).OrderBy(p => p.Name));
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ✅ CORRETO: Usa IReadQueryExecutor.FirstOrDefaultAsync()
    public async Task<ProductReadItem?> GetByIdAsync(Guid id)
    {
        using var db = await _readDbFactory.CreateAsync();
        return await _queryExecutor.FirstOrDefaultAsync(
            db.Products.Where(p => p.Id == id));
    }

    // ✅ CORRETO: Escrita via UseCase, leitura via IReadQueryExecutor
    public async Task CreateAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        SuccessMessage = null;

        try
        {
            var request = new CreateProductRequest(NewName);
            var result = await _createProductUseCase.ExecuteAsync(request);

            if (result.IsSuccess)
            {
                SuccessMessage = "Produto criado com sucesso!";
                NewName = string.Empty;
                await LoadAsync();
            }
            else
            {
                ErrorMessage = string.Join("; ", result.Errors);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erro: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
```

## ❌ Exemplo 2: ViewModel Incorreto (Com Erros)

```csharp
using CompileTimeFirst.Sample.Data;
using Microsoft.EntityFrameworkCore;

namespace CompileTimeFirst.Sample.Web.Components.Pages.BadExample;

public class BadProductsViewModel
{
    private readonly SchoolDbContext _dbContext;

    // ❌ ERRO CTFA001: Não pode injetar SchoolDbContext diretamente!
    // Deveria usar IReadSchoolDbFactory
    public BadProductsViewModel(SchoolDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public List<Product> Items { get; private set; } = new();

    // ❌ ERRO CTFA002: Não pode usar ToListAsync() do EF Core!
    // Deveria usar IReadQueryExecutor.ToListAsync()
    public async Task LoadAsync()
    {
        Items = await _dbContext.Products
            .Where(p => p.IsActive)
            .ToListAsync(); // ← ERRO AQUI
    }

    // ❌ ERRO CTFA003: Não pode usar FirstOrDefaultAsync() do EF Core!
    // Deveria usar IReadQueryExecutor.FirstOrDefaultAsync()
    public async Task<Product?> GetByIdAsync(Guid id)
    {
        return await _dbContext.Products
            .Where(p => p.Id == id)
            .FirstOrDefaultAsync(); // ← ERRO AQUI
    }

    // ❌ MÚLTIPLOS ERROS: Não pode salvar diretamente em ViewModel!
    // Deveria usar um UseCase
    public async Task CreateAsync(string name)
    {
        var product = new Product { Id = Guid.NewGuid(), Name = name };
        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync(); // ← VIOLAÇÃO DE ARQUITETURA
    }
}
```

**Erros que seriam gerados:**
```
CTFA001: 'BadProductsViewModel' não pode injetar 'SchoolDbContext' ou 'IDbContextFactory<SchoolDbContext>'.
Use 'IReadSchoolDbFactory' para leitura.

CTFA002: Não use 'ToListAsync()' do EF Core. Use 'IReadQueryExecutor.ToListAsync()' para manter a abstração de leitura.

CTFA003: Não use 'FirstOrDefaultAsync()' do EF Core. Use 'IReadQueryExecutor.FirstOrDefaultAsync()' para manter a abstração de leitura.
```

## ✅ Exemplo 3: Componente Blazor Correto

```razor
@page "/products"
@rendermode InteractiveServer

<!-- ✅ CORRETO: Injeta apenas ViewModel -->
@inject ProductsViewModel ViewModel
@inject NavigationManager Navigation

<h3>Produtos</h3>

@if (ViewModel.IsBusy)
{
    <p><em>Carregando...</em></p>
}

@if (ViewModel.SuccessMessage != null)
{
    <div class="alert alert-success">@ViewModel.SuccessMessage</div>
}

@if (ViewModel.ErrorMessage != null)
{
    <div class="alert alert-danger">@ViewModel.ErrorMessage</div>
}

<EditForm Model="ViewModel" OnValidSubmit="HandleCreateAsync">
    <div class="mb-3">
        <label class="form-label">Nome:</label>
        <InputText @bind-Value="ViewModel.NewName" class="form-control" />
    </div>
    <button type="submit" class="btn btn-primary" disabled="@ViewModel.IsBusy">
        Criar Produto
    </button>
</EditForm>

<table class="table mt-4">
    <thead>
        <tr>
            <th>Nome</th>
            <th>Status</th>
        </tr>
    </thead>
    <tbody>
        @foreach (var item in ViewModel.Items)
        {
            <tr>
                <td>@item.Name</td>
                <td>@(item.IsActive ? "Ativo" : "Inativo")</td>
            </tr>
        }
    </tbody>
</table>

@code {
    protected override async Task OnInitializedAsync()
    {
        await ViewModel.LoadAsync();
    }

    private async Task HandleCreateAsync()
    {
        await ViewModel.CreateAsync();
    }
}
```

## ❌ Exemplo 4: Componente Blazor Incorreto

```razor
@page "/bad-products"
@rendermode InteractiveServer

<!-- ❌ ERRO CTFA001: Não pode injetar DbContext ou Factory de escrita! -->
@inject IDbContextFactory<SchoolDbContext> DbFactory

<!-- ❌ ERRO: Não pode injetar EF Core diretamente! -->
@inject SchoolDbContext DbContext

<h3>Bad Products</h3>

@code {
    private List<Product> products = new();

    protected override async Task OnInitializedAsync()
    {
        // ❌ ERRO CTFA002: Não pode usar ToListAsync() diretamente
        using var db = await DbFactory.CreateDbContextAsync();
        products = await db.Products.ToListAsync(); // ← ERRO
    }
}
```

## ✅ Exemplo 5: UseCase (Camada de Escrita)

```csharp
using CompileTimeFirst.Sample.Data;
using CompileTimeFirst.Sample.Domain;
using Microsoft.EntityFrameworkCore;

namespace CompileTimeFirst.Sample.Application.Products;

public interface ICreateProductUseCase
{
    Task<CreateProductResult> ExecuteAsync(CreateProductRequest request);
}

public record CreateProductRequest(string Name);
public record CreateProductResult(Guid ProductId);

// ✅ UseCases PODEM injetar IDbContextFactory<SchoolDbContext> para escrita
public class CreateProductUseCase : UseCaseBase<CreateProductRequest, CreateProductResult>, ICreateProductUseCase
{
    private readonly IDbContextFactory<SchoolDbContext> _dbContextFactory;

    // ✅ CORRETO: UseCases são a ÚNICA camada que pode escrever no banco
    public CreateProductUseCase(IDbContextFactory<SchoolDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    protected override async Task<CreateProductResult> ExecuteCoreAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            IsActive = true
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        return new CreateProductResult(product.Id);
    }

    protected override async Task<IEnumerable<string>> Validate(
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors.Add("Nome é obrigatório");
        }

        if (request.Name.Length > 100)
        {
            errors.Add("Nome deve ter no máximo 100 caracteres");
        }

        // Validação de duplicidade
        using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var exists = await db.Products
            .AnyAsync(p => p.Name == request.Name && p.IsActive, cancellationToken);

        if (exists)
        {
            errors.Add($"Produto '{request.Name}' já existe");
        }

        return errors;
    }
}
```

## 📊 Resumo da Arquitetura

| Camada | Pode Injetar | Pode Usar | Exemplo |
|--------|--------------|-----------|---------|
| **ViewModels** | ✅ `IReadSchoolDbFactory`<br>✅ `IReadQueryExecutor`<br>✅ `IXxxUseCase` | ❌ `SchoolDbContext`<br>❌ `ToListAsync()` EF<br>❌ `SaveChangesAsync()` | `ProductsViewModel` |
| **Componentes Blazor** | ✅ `XxxViewModel` | ❌ `DbContext`<br>❌ `IDbContextFactory` | `Products.razor` |
| **UseCases** | ✅ `IDbContextFactory<SchoolDbContext>` | ✅ `SaveChangesAsync()`<br>✅ EF Core completo | `CreateProductUseCase` |
| **ReadStore** | ✅ `IDbContextFactory<ReadOnlySchoolDbContext>` | ✅ Queries read-only | `ReadSchoolDbFactory` |

## 🔧 Como Consertar Erros

### CTFA001: Injeção de DbContext de Escrita

**Antes:**
```csharp
public MyViewModel(SchoolDbContext dbContext) { }
```

**Depois:**
```csharp
public MyViewModel(IReadSchoolDbFactory readDbFactory, IReadQueryExecutor queryExecutor) { }
```

### CTFA002: ToListAsync() do EF Core

**Antes:**
```csharp
var items = await db.Products.ToListAsync();
```

**Depois:**
```csharp
var items = await _queryExecutor.ToListAsync(db.Products);
```

### CTFA003: FirstOrDefaultAsync() do EF Core

**Antes:**
```csharp
var item = await db.Products.FirstOrDefaultAsync(p => p.Id == id);
```

**Depois:**
```csharp
var item = await _queryExecutor.FirstOrDefaultAsync(db.Products.Where(p => p.Id == id));
```

## ✨ Benefícios

1. **Separação clara de responsabilidades**
   - ViewModels = Leitura + Orquestração
   - UseCases = Escrita + Regras de Negócio

2. **Impossível violar a arquitetura**
   - Analyzers bloqueiam no compile-time

3. **Facilita testes unitários**
   - ViewModels não dependem de DbContext real
   - Apenas de interfaces (IReadSchoolDbFactory, IReadQueryExecutor)

4. **Performance e escalabilidade**
   - Leitura pode usar read replicas
   - Escrita isolada em UseCases

5. **Onboarding rápido**
   - Novos desenvolvedores recebem feedback automático do IDE
