using Azure;
using MonadicSharp;

namespace MonadicSharp.Azure.KeyVault;

/// <summary>
/// Maps <see cref="RequestFailedException"/> thrown by the Azure Key Vault SDK
/// into structured <see cref="Error"/> values for Railway-Oriented Programming.
/// </summary>
public static class KeyVaultExceptionMapping
{
    /// <summary>
    /// Converts a <see cref="RequestFailedException"/> into a <see cref="Error"/>
    /// with an <see cref="ErrorType"/> derived from the HTTP status code.
    /// </summary>
    public static Error ToMonadicError(this RequestFailedException ex)
    {
        var (code, errorType) = ex.Status switch
        {
            404    => ("KV_SECRET_NOT_FOUND",   ErrorType.NotFound),
            409    => ("KV_SECRET_CONFLICT",    ErrorType.Conflict),
            403    => ("KV_ACCESS_DENIED",      ErrorType.Forbidden),
            400    => ("KV_INVALID_REQUEST",    ErrorType.Validation),
            >= 500 => ("KV_SERVICE_ERROR",      ErrorType.Exception),
            _      => ("KV_REQUEST_FAILED",     ErrorType.Failure)
        };

        return Error.Create(ex.Message, code, errorType)
            .WithMetadata("ErrorCode", ex.ErrorCode ?? string.Empty)
            .WithMetadata("Status",    ex.Status);
    }
}
