using Azure;
using Azure.Security.KeyVault.Secrets;
using MonadicSharp;

namespace MonadicSharp.Azure.KeyVault;

/// <summary>
/// Extension methods for <see cref="SecretClient"/> that wrap secret operations
/// in <see cref="Result{T}"/> / <see cref="Option{T}"/> for Railway-Oriented Programming.
/// </summary>
public static class SecretClientExtensions
{
    // ── Find (404 → None) ────────────────────────────────────────────────────

    /// <summary>
    /// Retrieves the value of a secret.
    /// Returns <c>None</c> if the secret does not exist (404). Other errors are thrown.
    /// </summary>
    public static async Task<Option<string>> FindSecretAsync(
        this SecretClient client,
        string name,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await client.GetSecretAsync(name, version, cancellationToken);
            return Option<string>.Some(response.Value.Value);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return Option<string>.None;
        }
    }

    // ── Get ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Retrieves the plain-text value of a secret wrapped in <see cref="Result{T}"/>.
    /// Returns <c>Result.Failure(NotFound)</c> if the secret does not exist.
    /// </summary>
    public static async Task<Result<string>> GetSecretValueAsync(
        this SecretClient client,
        string name,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await client.GetSecretAsync(name, version, cancellationToken);
            return Result<string>.Success(response.Value.Value);
        }
        catch (RequestFailedException ex)
        {
            return Result<string>.Failure(ex.ToMonadicError());
        }
    }

    // ── Set ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates or updates a secret with the given <paramref name="value"/>.
    /// Returns <c>Result.Success(Unit)</c> on success.
    /// </summary>
    public static async Task<Result<Unit>> SetSecretValueAsync(
        this SecretClient client,
        string name,
        string value,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await client.SetSecretAsync(name, value, cancellationToken);
            return Result<Unit>.Success(Unit.Value);
        }
        catch (RequestFailedException ex)
        {
            return Result<Unit>.Failure(ex.ToMonadicError());
        }
    }

    // ── Delete ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Starts the deletion of a secret (Key Vault soft-delete).
    /// Returns <c>Result.Success(Unit)</c> when the operation is initiated successfully.
    /// Returns <c>Result.Failure(NotFound)</c> if the secret does not exist.
    /// </summary>
    public static async Task<Result<Unit>> DeleteSecretAsync(
        this SecretClient client,
        string name,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await client.StartDeleteSecretAsync(name, cancellationToken);
            return Result<Unit>.Success(Unit.Value);
        }
        catch (RequestFailedException ex)
        {
            return Result<Unit>.Failure(ex.ToMonadicError());
        }
    }
}
