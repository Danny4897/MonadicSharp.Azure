using System.Net;
using Microsoft.Azure.Cosmos;
using MonadicSharp;

namespace MonadicSharp.Azure.CosmosDb;

/// <summary>
/// Extension methods for <see cref="Container"/> that wrap Cosmos DB operations
/// in <see cref="Result{T}"/> and <see cref="Option{T}"/> for Railway-Oriented Programming.
/// </summary>
public static class ContainerExtensions
{
    // ── Point reads ──────────────────────────────────────────────────────────

    /// <summary>
    /// Reads an item by id. Returns <c>Option.None</c> when not found (404),
    /// or <c>Result.Failure</c> on any other error.
    /// </summary>
    public static async Task<Option<T>> FindAsync<T>(
        this Container container,
        string id,
        PartitionKey partitionKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await container.ReadItemAsync<T>(id, partitionKey,
                cancellationToken: cancellationToken);
            return Option<T>.From(response.Resource);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return Option<T>.None;
        }
    }

    /// <summary>
    /// Reads an item by id. Returns <c>Result.Failure(NotFound)</c> when not found.
    /// Use <see cref="FindAsync{T}"/> if you prefer the <see cref="Option{T}"/> path.
    /// </summary>
    public static async Task<Result<T>> ReadAsync<T>(
        this Container container,
        string id,
        PartitionKey partitionKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await container.ReadItemAsync<T>(id, partitionKey,
                cancellationToken: cancellationToken);
            return Result<T>.Success(response.Resource);
        }
        catch (CosmosException ex)
        {
            return Result<T>.Failure(ex.ToMonadicError());
        }
    }

    // ── Writes ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new item. Returns <c>Result.Failure(Conflict)</c> if the item already exists.
    /// </summary>
    public static async Task<Result<T>> CreateAsync<T>(
        this Container container,
        T item,
        PartitionKey? partitionKey = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await container.CreateItemAsync(item, partitionKey,
                cancellationToken: cancellationToken);
            return Result<T>.Success(response.Resource);
        }
        catch (CosmosException ex)
        {
            return Result<T>.Failure(ex.ToMonadicError());
        }
    }

    /// <summary>
    /// Creates or replaces an item (upsert semantics).
    /// </summary>
    public static async Task<Result<T>> UpsertAsync<T>(
        this Container container,
        T item,
        PartitionKey? partitionKey = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await container.UpsertItemAsync(item, partitionKey,
                cancellationToken: cancellationToken);
            return Result<T>.Success(response.Resource);
        }
        catch (CosmosException ex)
        {
            return Result<T>.Failure(ex.ToMonadicError());
        }
    }

    /// <summary>
    /// Replaces an existing item by id. Returns <c>Result.Failure(NotFound)</c> if missing.
    /// </summary>
    public static async Task<Result<T>> ReplaceAsync<T>(
        this Container container,
        T item,
        string id,
        PartitionKey? partitionKey = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await container.ReplaceItemAsync(item, id, partitionKey,
                cancellationToken: cancellationToken);
            return Result<T>.Success(response.Resource);
        }
        catch (CosmosException ex)
        {
            return Result<T>.Failure(ex.ToMonadicError());
        }
    }

    /// <summary>
    /// Deletes an item by id. Returns <c>Result.Failure(NotFound)</c> if missing.
    /// </summary>
    public static async Task<Result<Unit>> DeleteAsync(
        this Container container,
        string id,
        PartitionKey partitionKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await container.DeleteItemAsync<object>(id, partitionKey,
                cancellationToken: cancellationToken);
            return Result<Unit>.Success(Unit.Value);
        }
        catch (CosmosException ex)
        {
            return Result<Unit>.Failure(ex.ToMonadicError());
        }
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Executes a <see cref="QueryDefinition"/> and collects all pages into a list.
    /// </summary>
    public static async Task<Result<IReadOnlyList<T>>> QueryAsync<T>(
        this Container container,
        QueryDefinition query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var results = new List<T>();
            using var iterator = container.GetItemQueryIterator<T>(query);

            while (iterator.HasMoreResults)
            {
                var page = await iterator.ReadNextAsync(cancellationToken);
                results.AddRange(page);
            }

            return Result<IReadOnlyList<T>>.Success(results);
        }
        catch (CosmosException ex)
        {
            return Result<IReadOnlyList<T>>.Failure(ex.ToMonadicError());
        }
    }

    /// <summary>
    /// Executes a SQL query string and collects all pages into a list.
    /// </summary>
    public static Task<Result<IReadOnlyList<T>>> QueryAsync<T>(
        this Container container,
        string sql,
        CancellationToken cancellationToken = default) =>
        container.QueryAsync<T>(new QueryDefinition(sql), cancellationToken);

    /// <summary>
    /// Executes a <see cref="QueryDefinition"/> and returns the first result as an Option.
    /// Returns <c>Option.None</c> when the query returns no results.
    /// </summary>
    public static async Task<Option<T>> QueryFirstOrNoneAsync<T>(
        this Container container,
        QueryDefinition query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var iterator = container.GetItemQueryIterator<T>(query,
                requestOptions: new QueryRequestOptions { MaxItemCount = 1 });

            if (iterator.HasMoreResults)
            {
                var page = await iterator.ReadNextAsync(cancellationToken);
                var first = page.FirstOrDefault();
                return first is null ? Option<T>.None : Option<T>.Some(first);
            }

            return Option<T>.None;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return Option<T>.None;
        }
    }
}
