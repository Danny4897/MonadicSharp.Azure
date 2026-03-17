using System.Net;
using Microsoft.Azure.Cosmos;
using MonadicSharp;

namespace MonadicSharp.Azure.CosmosDb;

/// <summary>
/// Maps <see cref="CosmosException"/> to MonadicSharp <see cref="Error"/> values,
/// preserving semantic meaning (404 → NotFound, 409 → Conflict, etc.).
/// </summary>
public static class CosmosExceptionMapping
{
    /// <summary>
    /// Converts a <see cref="CosmosException"/> to a structured <see cref="Error"/>.
    /// Status codes are mapped to the most semantically appropriate <see cref="ErrorType"/>.
    /// </summary>
    public static Error ToMonadicError(this CosmosException ex)
    {
        var errorType = ex.StatusCode switch
        {
            HttpStatusCode.NotFound            => ErrorType.NotFound,
            HttpStatusCode.Conflict            => ErrorType.Conflict,
            HttpStatusCode.Forbidden           => ErrorType.Forbidden,
            HttpStatusCode.BadRequest          => ErrorType.Validation,
            HttpStatusCode.TooManyRequests     => ErrorType.Failure,   // 429 rate limit
            >= HttpStatusCode.InternalServerError => ErrorType.Exception,
            _                                  => ErrorType.Failure
        };

        var code = ex.StatusCode switch
        {
            HttpStatusCode.NotFound            => "COSMOS_NOT_FOUND",
            HttpStatusCode.Conflict            => "COSMOS_CONFLICT",
            HttpStatusCode.Forbidden           => "COSMOS_FORBIDDEN",
            HttpStatusCode.BadRequest          => "COSMOS_BAD_REQUEST",
            HttpStatusCode.TooManyRequests     => "COSMOS_RATE_LIMITED",
            >= HttpStatusCode.InternalServerError => "COSMOS_SERVER_ERROR",
            _                                  => "COSMOS_ERROR"
        };

        return Error.Create(ex.Message, code, errorType)
            .WithMetadata("StatusCode", (int)ex.StatusCode)
            .WithMetadata("ActivityId", ex.ActivityId ?? "")
            .WithMetadata("RequestCharge", ex.RequestCharge);
    }
}
