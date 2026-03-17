using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using MonadicSharp;

namespace MonadicSharp.Azure.Storage;

/// <summary>
/// Extension methods for <see cref="BlobContainerClient"/> that wrap blob operations
/// in <see cref="Result{T}"/> / <see cref="Option{T}"/> for Railway-Oriented Programming.
/// </summary>
public static class BlobContainerClientExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    // ── Find (404 → None) ────────────────────────────────────────────────────

    /// <summary>
    /// Downloads the blob content as <see cref="BinaryData"/>.
    /// Returns <c>None</c> if the blob does not exist (404). Other errors are thrown.
    /// </summary>
    public static async Task<Option<BinaryData>> FindAsync(
        this BlobContainerClient container,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client   = container.GetBlobClient(blobName);
            var response = await client.DownloadContentAsync(cancellationToken);
            return Option<BinaryData>.Some(response.Value.Content);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return Option<BinaryData>.None;
        }
    }

    // ── Download ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Downloads the blob as raw <see cref="BinaryData"/>.
    /// Returns <c>Result.Failure</c> on any SDK error.
    /// </summary>
    public static async Task<Result<BinaryData>> DownloadAsync(
        this BlobContainerClient container,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client   = container.GetBlobClient(blobName);
            var response = await client.DownloadContentAsync(cancellationToken);
            return Result<BinaryData>.Success(response.Value.Content);
        }
        catch (RequestFailedException ex)
        {
            return Result<BinaryData>.Failure(ex.ToMonadicError());
        }
    }

    /// <summary>
    /// Downloads the blob and deserializes its JSON content into <typeparamref name="T"/>.
    /// Returns <c>Result.Failure(Validation)</c> if deserialization fails.
    /// </summary>
    public static async Task<Result<T>> DownloadJsonAsync<T>(
        this BlobContainerClient container,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client   = container.GetBlobClient(blobName);
            var response = await client.DownloadContentAsync(cancellationToken);
            var value    = response.Value.Content.ToObjectFromJson<T>(JsonOptions)
                ?? throw new InvalidOperationException(
                    $"JSON deserialized to null for type {typeof(T).Name}.");
            return Result<T>.Success(value);
        }
        catch (RequestFailedException ex)
        {
            return Result<T>.Failure(ex.ToMonadicError());
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(
                Error.Create(ex.Message, "BLOB_DESERIALIZE_ERROR", ErrorType.Validation));
        }
    }

    // ── Upload ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Uploads raw <see cref="BinaryData"/> to a blob.
    /// Returns the blob <see cref="Uri"/> on success.
    /// </summary>
    public static async Task<Result<Uri>> UploadAsync(
        this BlobContainerClient container,
        string blobName,
        BinaryData content,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = container.GetBlobClient(blobName);
            await client.UploadAsync(content, overwrite: overwrite, cancellationToken: cancellationToken);
            return Result<Uri>.Success(client.Uri);
        }
        catch (RequestFailedException ex)
        {
            return Result<Uri>.Failure(ex.ToMonadicError());
        }
    }

    /// <summary>Uploads a UTF-8 text string to the blob.</summary>
    public static Task<Result<Uri>> UploadTextAsync(
        this BlobContainerClient container,
        string blobName,
        string text,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
        => container.UploadAsync(blobName, BinaryData.FromString(text), overwrite, cancellationToken);

    /// <summary>
    /// Serializes <paramref name="value"/> to JSON and uploads it to the blob.
    /// </summary>
    public static Task<Result<Uri>> UploadJsonAsync<T>(
        this BlobContainerClient container,
        string blobName,
        T value,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
        => container.UploadAsync(
            blobName,
            BinaryData.FromObjectAsJson(value, JsonOptions),
            overwrite,
            cancellationToken);

    // ── Delete ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Deletes the blob if it exists.
    /// Returns <c>true</c> if the blob was deleted, <c>false</c> if it was not found.
    /// Returns <c>Result.Failure</c> on any other error (e.g. access denied).
    /// </summary>
    public static async Task<Result<bool>> DeleteAsync(
        this BlobContainerClient container,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client   = container.GetBlobClient(blobName);
            var response = await client.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            return Result<bool>.Success(response.Value);
        }
        catch (RequestFailedException ex)
        {
            return Result<bool>.Failure(ex.ToMonadicError());
        }
    }

    // ── Exists ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Checks whether a blob exists in the container.
    /// Returns <c>Result.Failure</c> on access errors or service failures.
    /// </summary>
    public static async Task<Result<bool>> ExistsAsync(
        this BlobContainerClient container,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client   = container.GetBlobClient(blobName);
            var response = await client.ExistsAsync(cancellationToken);
            return Result<bool>.Success(response.Value);
        }
        catch (RequestFailedException ex)
        {
            return Result<bool>.Failure(ex.ToMonadicError());
        }
    }
}
