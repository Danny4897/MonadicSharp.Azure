# MonadicSharp.Azure

> Railway-Oriented Programming for the Azure ecosystem.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4.svg)](https://dotnet.microsoft.com/download)
[![Build](https://github.com/Danny4897/MonadicSharp.Azure/actions/workflows/release.yml/badge.svg)](https://github.com/Danny4897/MonadicSharp.Azure/actions/workflows/release.yml)

Seven focused NuGet packages that integrate [MonadicSharp](https://github.com/Danny4897/MonadicSharp) with Azure services. Every SDK call is wrapped in `Result<T>` or `Option<T>` — no scattered `try/catch`, no null checks, just composable pipelines.

---

## Packages

| Package | Version | Description |
|---------|---------|-------------|
| [MonadicSharp.Azure.Core](#monadicsharpazurecore) | [![GitHub release](https://img.shields.io/github/v/release/Danny4897/MonadicSharp.Azure?label=)](https://github.com/Danny4897/MonadicSharp.Azure/releases) | HTTP status mapping & RFC 9457 ProblemDetails |
| [MonadicSharp.Azure.Functions](#monadicsharpazurefunctions) | [![GitHub release](https://img.shields.io/github/v/release/Danny4897/MonadicSharp.Azure?label=)](https://github.com/Danny4897/MonadicSharp.Azure/releases) | Azure Functions v4 Isolated Worker integration |
| [MonadicSharp.Azure.CosmosDb](#monadicsharpazurecosmosdb) | [![GitHub release](https://img.shields.io/github/v/release/Danny4897/MonadicSharp.Azure?label=)](https://github.com/Danny4897/MonadicSharp.Azure/releases) | Cosmos DB container extensions |
| [MonadicSharp.Azure.Messaging](#monadicsharpazuremessaging) | [![GitHub release](https://img.shields.io/github/v/release/Danny4897/MonadicSharp.Azure?label=)](https://github.com/Danny4897/MonadicSharp.Azure/releases) | Service Bus sender & receiver extensions |
| [MonadicSharp.Azure.Storage](#monadicsharpazurestorage) | [![GitHub release](https://img.shields.io/github/v/release/Danny4897/MonadicSharp.Azure?label=)](https://github.com/Danny4897/MonadicSharp.Azure/releases) | Blob Storage upload/download extensions |
| [MonadicSharp.Azure.KeyVault](#monadicsharpazurekeyvault) | [![GitHub release](https://img.shields.io/github/v/release/Danny4897/MonadicSharp.Azure?label=)](https://github.com/Danny4897/MonadicSharp.Azure/releases) | Key Vault secret access |
| [MonadicSharp.Azure.OpenAI](#monadicsharpazureopenai) | [![GitHub release](https://img.shields.io/github/v/release/Danny4897/MonadicSharp.Azure?label=)](https://github.com/Danny4897/MonadicSharp.Azure/releases) | Azure OpenAI chat completions & embeddings |

---

## Requirements

- .NET 8.0 or later
- [MonadicSharp](https://github.com/Danny4897/MonadicSharp) v1.4.0+ (pulled in automatically)

---

## Installation

Install only the packages you need. Each package pulls in its Azure SDK dependency automatically.

```bash
# Core HTTP utilities (required by all other packages)
dotnet add package MonadicSharp.Azure.Core

# Azure Functions v4
dotnet add package MonadicSharp.Azure.Functions

# Cosmos DB
dotnet add package MonadicSharp.Azure.CosmosDb

# Service Bus
dotnet add package MonadicSharp.Azure.Messaging

# Blob Storage
dotnet add package MonadicSharp.Azure.Storage

# Key Vault
dotnet add package MonadicSharp.Azure.KeyVault

# Azure OpenAI
dotnet add package MonadicSharp.Azure.OpenAI
```

> All packages are available on [GitHub Packages](https://github.com/Danny4897/MonadicSharp.Azure/pkgs/nuget/MonadicSharp.Azure.Core).
> To restore from GitHub Packages, add the feed to your `nuget.config`:
>
> ```xml
> <packageSources>
>   <add key="github" value="https://nuget.pkg.github.com/Danny4897/index.json" />
> </packageSources>
> ```

---

## MonadicSharp.Azure.Core

Shared infrastructure. Provides the bridge between MonadicSharp `Error`/`ErrorType` and the HTTP world.

### ErrorHttpMapping

Maps `ErrorType` → `HttpStatusCode` following RFC 9110 semantics:

| ErrorType | HTTP Status |
|-----------|-------------|
| `Validation` | 422 Unprocessable Entity |
| `NotFound` | 404 Not Found |
| `Forbidden` | 403 Forbidden |
| `Conflict` | 409 Conflict |
| `Exception` | 500 Internal Server Error |
| `Failure` (default) | 400 Bad Request |

```csharp
using MonadicSharp.Azure.Core;

HttpStatusCode status = error.ToHttpStatusCode();
int statusInt         = error.ToHttpStatusInt();
```

### MonadicProblemDetails

RFC 9457-compliant problem details record, ready to serialize as JSON:

```csharp
var problem = new MonadicProblemDetails(error);
// {
//   "type": "https://httpstatuses.com/422",
//   "title": "Validation Error",
//   "status": 422,
//   "detail": "Email address is not valid.",
//   "code": "INVALID_EMAIL"
// }
```

---

## MonadicSharp.Azure.Functions

Extend Azure Functions v4 Isolated Worker with `Result<T>`-aware request/response helpers.

### Reading requests

```csharp
using MonadicSharp.Azure.Functions;

// Deserialize body → Result<T> (Failure on empty or malformed JSON)
Result<CreateOrderRequest> body = await req.ReadFromJsonAsync<CreateOrderRequest>();

// Deserialize + validate in one step
Result<CreateOrderRequest> validated = await req.ReadAndValidateAsync<CreateOrderRequest>(
    order => order.Amount > 0
        ? Result<CreateOrderRequest>.Success(order)
        : Result<CreateOrderRequest>.Failure(Error.Create("Amount must be positive", "INVALID_AMOUNT", ErrorType.Validation)));
```

### Writing responses

```csharp
// Success → 200 OK with JSON body
// Failure → mapped status (4xx/5xx) with RFC 9457 ProblemDetails body
return await ProcessOrder(order)
    .ToHttpResponseAsync(req);

// Custom success status
return await CreateOrder(order)
    .ToCreatedResponseAsync(req, o => $"/orders/{o.Id}");

// Command operations: Success → 204 No Content
return await DeleteOrder(id)
    .ToHttpResponseAsync(req);
```

### Full Azure Function example

```csharp
[Function("CreateOrder")]
public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
{
    return await req
        .ReadAndValidateAsync<CreateOrderRequest>(Validate)
        .BindAsync(CreateInCosmosAsync)
        .ToCreatedResponseAsync(req, o => $"/orders/{o.Id}");
}
```

---

## MonadicSharp.Azure.CosmosDb

Wrap Cosmos DB `Container` operations in `Result<T>` and `Option<T>`.

### Point reads

```csharp
using MonadicSharp.Azure.CosmosDb;

// 404 → None (not an error)
Option<Product> product = await container.FindAsync<Product>("prod-1", new PartitionKey("electronics"));

// 404 → Result.Failure(NotFound)
Result<Product> product = await container.ReadAsync<Product>("prod-1", new PartitionKey("electronics"));
```

### Writes

```csharp
// 409 Conflict → Result.Failure(Conflict)
Result<Product> created = await container.CreateAsync(newProduct);

// Upsert / Replace
Result<Product> saved    = await container.UpsertAsync(product);
Result<Product> replaced = await container.ReplaceAsync(product, product.Id);

// Delete
Result<Unit> deleted = await container.DeleteAsync("prod-1", new PartitionKey("electronics"));
```

### Queries

```csharp
// Returns all matching items
Result<IReadOnlyList<Product>> results = await container.QueryAsync<Product>(
    new QueryDefinition("SELECT * FROM c WHERE c.category = @cat")
        .WithParameter("@cat", "electronics"));

// String overload
Result<IReadOnlyList<Product>> results = await container.QueryAsync<Product>(
    "SELECT * FROM c WHERE c.inStock = true");

// First match or None
Option<Product> first = await container.QueryFirstOrNoneAsync<Product>(
    new QueryDefinition("SELECT * FROM c WHERE c.id = @id").WithParameter("@id", "prod-1"));
```

### Pipeline example

```csharp
Result<Order> result = await container.ReadAsync<Cart>(cartId, pk)
    .BindAsync(cart => ValidateCart(cart))
    .BindAsync(cart => container.CreateAsync(Order.From(cart)))
    .BindAsync(order => container.DeleteAsync(cartId, pk).MapAsync(_ => order));
```

---

## MonadicSharp.Azure.Messaging

Functional wrappers for Service Bus sender and receiver, with automatic dead-lettering.

### Exception mapping

| ServiceBusFailureReason | ErrorType | Code |
|-------------------------|-----------|------|
| `MessagingEntityNotFound` | NotFound | `SB_ENTITY_NOT_FOUND` |
| `MessageNotFound` | NotFound | `SB_MESSAGE_NOT_FOUND` |
| `MessagingEntityDisabled` | Forbidden | `SB_ENTITY_DISABLED` |
| `QuotaExceeded` | Failure | `SB_QUOTA_EXCEEDED` |
| `MessageSizeExceeded` | Validation | `SB_MESSAGE_TOO_LARGE` |
| `ServiceCommunicationProblem` / `ServiceTimeout` | Exception | `SB_COMMUNICATION_ERROR` / `SB_TIMEOUT` |

### Sending

```csharp
using MonadicSharp.Azure.Messaging;

// Serialize to JSON and send
Result<Unit> sent = await sender.SendAsync(new OrderCreatedEvent(orderId));

// With message customization
Result<Unit> sent = await sender.SendAsync(
    new OrderCreatedEvent(orderId),
    configure: msg =>
    {
        msg.Subject         = "order.created";
        msg.CorrelationId   = correlationId;
        msg.TimeToLive      = TimeSpan.FromHours(24);
    });

// Pre-built message
Result<Unit> sent = await sender.SendRawAsync(myMessage);

// Batch — collects all results, never short-circuits
IReadOnlyList<Result<Unit>> results = await sender.SendBatchAsync(events);
var (sent, failed) = results.Partition(); // MonadicSharp extension
```

### Receiving & processing

```csharp
// Deserialize body
Result<OrderCreatedEvent> evt = message.DeserializeBody<OrderCreatedEvent>();

// Deserialize + validate
Result<OrderCreatedEvent> validated = message.DeserializeAndValidate<OrderCreatedEvent>(
    e => e.OrderId != Guid.Empty
        ? Result<OrderCreatedEvent>.Success(e)
        : Result<OrderCreatedEvent>.Failure(Error.Create("Missing order ID", "MISSING_ID", ErrorType.Validation)));

// Auto complete or dead-letter based on result
await ProcessMessage(message)
    .CompleteOrDeadLetterAsync(receiver, message);
```

### Full batch pipeline

```csharp
var (succeeded, failed) = await receiver.ProcessBatchAsync<OrderCreatedEvent>(
    messages,
    async evt =>
    {
        await orderService.ProcessAsync(evt);
        return Result<OrderCreatedEvent>.Success(evt);
    });

logger.LogInformation("Processed: {ok}, Dead-lettered: {fail}", succeeded.Count(), failed.Count());
```

---

## MonadicSharp.Azure.Storage

Blob Storage operations on `BlobContainerClient`, wrapped in `Result<T>` and `Option<T>`.

### Exception mapping

| HTTP Status | ErrorType | Code |
|-------------|-----------|------|
| 404 | NotFound | `BLOB_NOT_FOUND` |
| 409 | Conflict | `BLOB_CONFLICT` |
| 403 | Forbidden | `BLOB_ACCESS_DENIED` |
| 400 | Validation | `BLOB_INVALID_REQUEST` |
| 5xx | Exception | `BLOB_SERVICE_ERROR` |

### Operations

```csharp
using MonadicSharp.Azure.Storage;

// 404 → None
Option<BinaryData> data = await container.FindAsync("images/logo.png");

// 404 → Failure(NotFound)
Result<BinaryData> data = await container.DownloadAsync("reports/q1.pdf");

// Deserialize JSON blob
Result<ReportConfig> config = await container.DownloadJsonAsync<ReportConfig>("config/report.json");

// Upload — returns blob URI on success
Result<Uri> uri = await container.UploadAsync("data/export.bin", binaryData, overwrite: true);
Result<Uri> uri = await container.UploadTextAsync("notes/readme.txt", "Hello, World!");
Result<Uri> uri = await container.UploadJsonAsync("config/settings.json", mySettings);

// Delete (returns true if deleted, false if not found)
Result<bool> deleted = await container.DeleteAsync("temp/old-file.tmp");

// Existence check
Result<bool> exists = await container.ExistsAsync("images/logo.png");
```

### Pipeline example

```csharp
Result<Uri> result = await container
    .DownloadJsonAsync<ProductCatalog>("catalog/v1.json")
    .BindAsync(catalog => Transform(catalog))
    .BindAsync(updated => container.UploadJsonAsync("catalog/v2.json", updated, overwrite: true));
```

---

## MonadicSharp.Azure.KeyVault

Access Key Vault secrets with `Result<T>` and `Option<T>`.

### Exception mapping

| HTTP Status | ErrorType | Code |
|-------------|-----------|------|
| 404 | NotFound | `KV_SECRET_NOT_FOUND` |
| 409 | Conflict | `KV_SECRET_CONFLICT` |
| 403 | Forbidden | `KV_ACCESS_DENIED` |
| 400 | Validation | `KV_INVALID_REQUEST` |
| 5xx | Exception | `KV_SERVICE_ERROR` |

### Operations

```csharp
using MonadicSharp.Azure.KeyVault;

// 404 → None (non-existence is not an error)
Option<string> connStr = await secretClient.FindSecretAsync("sql-connection-string");

// 404 → Failure(NotFound)
Result<string> apiKey = await secretClient.GetSecretValueAsync("openai-api-key");

// Specific version
Result<string> oldKey = await secretClient.GetSecretValueAsync("openai-api-key", version: "abc123");

// Create or update
Result<Unit> saved = await secretClient.SetSecretValueAsync("feature-flag", "true");

// Soft-delete (Key Vault two-phase deletion)
Result<Unit> deleted = await secretClient.DeleteSecretAsync("deprecated-key");
```

### Bootstrap pattern

```csharp
// Load all required secrets at startup and fail fast if any are missing
Result<AppSecrets> secrets = await LoadSecretsAsync(secretClient);

return secrets.Match(
    onSuccess: s => builder.Services.AddSingleton(s),
    onFailure: e => throw e.ToException());

static async Task<Result<AppSecrets>> LoadSecretsAsync(SecretClient client)
{
    var dbConn  = await client.GetSecretValueAsync("db-connection-string");
    var apiKey  = await client.GetSecretValueAsync("openai-api-key");
    var jwtKey  = await client.GetSecretValueAsync("jwt-signing-key");

    if (dbConn.IsFailure)  return Result<AppSecrets>.Failure(dbConn.Error);
    if (apiKey.IsFailure)  return Result<AppSecrets>.Failure(apiKey.Error);
    if (jwtKey.IsFailure)  return Result<AppSecrets>.Failure(jwtKey.Error);

    return Result<AppSecrets>.Success(new AppSecrets(dbConn.Value, apiKey.Value, jwtKey.Value));
}
```

---

## MonadicSharp.Azure.OpenAI

Wrap Azure OpenAI chat completions and embeddings in `Result<T>`.

### Exception mapping

| HTTP Status | ErrorType | Code |
|-------------|-----------|------|
| 401 | Forbidden | `OPENAI_UNAUTHORIZED` |
| 403 | Forbidden | `OPENAI_FORBIDDEN` |
| 404 | NotFound | `OPENAI_NOT_FOUND` |
| 429 | Failure | `OPENAI_RATE_LIMITED` |
| 5xx | Exception | `OPENAI_SERVICE_ERROR` |

> Both `ClientResultException` (OpenAI SDK) and `RequestFailedException` (Azure transport) are handled.

### Chat completions

```csharp
using MonadicSharp.Azure.OpenAI;

AzureOpenAIClient azure  = new(new Uri(endpoint), new AzureKeyCredential(key));
ChatClient        client = azure.GetChatClient("gpt-4o");

// Returns the first text content as Result<string>
Result<string> reply = await client.ChatAsync(
[
    ChatMessage.CreateSystemMessage("You are a helpful assistant."),
    ChatMessage.CreateUserMessage("Summarize the following order: " + orderJson)
]);

// Full completion object (finish reason, usage, tool calls, etc.)
Result<ChatCompletion> completion = await client.ChatWithDetailsAsync(
[
    ChatMessage.CreateUserMessage("What is 2+2?")
]);

completion.Match(
    onSuccess: c => Console.WriteLine($"Reply: {c.Content[0].Text} | Tokens: {c.Usage.TotalTokenCount}"),
    onFailure: e => Console.WriteLine($"Error [{e.Code}]: {e.Message}"));
```

### Embeddings

```csharp
EmbeddingClient embedder = azure.GetEmbeddingClient("text-embedding-3-small");

// Single vector
Result<ReadOnlyMemory<float>> vector = await embedder.EmbedAsync("MonadicSharp is awesome");

// Batch — results are order-preserving
Result<IReadOnlyList<ReadOnlyMemory<float>>> vectors = await embedder.EmbedBatchAsync(
[
    "MonadicSharp is awesome",
    "Railway-Oriented Programming in C#",
    "Functional error handling"
]);
```

### Pipeline: classify and store

```csharp
Result<string> result = await embedder
    .EmbedAsync(userQuery)
    .BindAsync(vector => vectorDb.SearchAsync(vector, topK: 5))
    .BindAsync(context => chatClient.ChatAsync(BuildPrompt(userQuery, context)));
```

---

## Design Principles

All packages follow the same conventions:

| Pattern | Detail |
|---------|--------|
| **Option vs Result** | `FindAsync` / `FindSecretAsync` return `Option<T>` for 404 — non-existence is not an error. All other operations return `Result<T>`. |
| **Error codes** | Machine-readable `SCREAMING_SNAKE_CASE` codes on every `Error` (e.g., `COSMOS_NOT_FOUND`, `SB_QUOTA_EXCEEDED`). |
| **Metadata** | Every mapped exception carries contextual metadata (`StatusCode`, `ActivityId`, `IsTransient`, etc.) for observability. |
| **CancellationToken** | All async methods accept an optional `CancellationToken`. |
| **No short-circuit in batches** | Batch operations (`SendBatchAsync`, `ProcessBatchAsync`) collect all results and never stop early. Use `Partition()` from MonadicSharp to split successes and failures. |
| **RFC 9457** | HTTP error responses always include a fully-formed `ProblemDetails` body. |

---

## Test Coverage

| Package | Tests |
|---------|-------|
| MonadicSharp.Azure.Core | 15 |
| MonadicSharp.Azure.Functions | 15 |
| MonadicSharp.Azure.CosmosDb | 36 |
| MonadicSharp.Azure.Messaging | 35 |
| MonadicSharp.Azure.Storage | 32 |
| MonadicSharp.Azure.KeyVault | 26 |
| MonadicSharp.Azure.OpenAI | 31 |
| **Total** | **190** |

---

## Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/my-feature`
3. Make your changes and add tests
4. Run the full test suite: `dotnet test --configuration Release`
5. Open a pull request

---

## License

[MIT](LICENSE) © [Danny4897](https://github.com/Danny4897)
