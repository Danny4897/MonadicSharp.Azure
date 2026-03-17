using Azure;
using MonadicSharp;

namespace MonadicSharp.Azure.Storage;

/// <summary>
/// Maps <see cref="RequestFailedException"/> thrown by the Azure Blob Storage SDK
/// into structured <see cref="Error"/> values for Railway-Oriented Programming.
/// </summary>
public static class BlobExceptionMapping
{
    /// <summary>
    /// Converts a <see cref="RequestFailedException"/> into a <see cref="Error"/>
    /// with an <see cref="ErrorType"/> derived from the HTTP status code.
    /// </summary>
    public static Error ToMonadicError(this RequestFailedException ex)
    {
        var (code, errorType) = ex.Status switch
        {
            404    => ("BLOB_NOT_FOUND",       ErrorType.NotFound),
            409    => ("BLOB_CONFLICT",        ErrorType.Conflict),
            403    => ("BLOB_ACCESS_DENIED",   ErrorType.Forbidden),
            400    => ("BLOB_INVALID_REQUEST", ErrorType.Validation),
            >= 500 => ("BLOB_SERVICE_ERROR",   ErrorType.Exception),
            _      => ("BLOB_REQUEST_FAILED",  ErrorType.Failure)
        };

        return Error.Create(ex.Message, code, errorType)
            .WithMetadata("ErrorCode", ex.ErrorCode ?? string.Empty)
            .WithMetadata("Status", ex.Status);
    }
}
