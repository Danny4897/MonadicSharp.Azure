using System.ClientModel;
using Azure;
using MonadicSharp;

namespace MonadicSharp.Azure.OpenAI;

/// <summary>
/// Maps exceptions thrown by the Azure OpenAI SDK into structured <see cref="Error"/>
/// values for Railway-Oriented Programming.
/// </summary>
public static class OpenAIExceptionMapping
{
    /// <summary>
    /// Converts a <see cref="ClientResultException"/> (from the OpenAI SDK) into a
    /// <see cref="Error"/> with an <see cref="ErrorType"/> derived from the HTTP status code.
    /// </summary>
    public static Error ToMonadicError(this ClientResultException ex)
    {
        var (code, errorType) = ex.Status switch
        {
            401    => ("OPENAI_UNAUTHORIZED",    ErrorType.Forbidden),
            403    => ("OPENAI_FORBIDDEN",       ErrorType.Forbidden),
            404    => ("OPENAI_NOT_FOUND",       ErrorType.NotFound),
            429    => ("OPENAI_RATE_LIMITED",     ErrorType.Failure),
            >= 500 => ("OPENAI_SERVICE_ERROR",   ErrorType.Exception),
            _      => ("OPENAI_REQUEST_FAILED",  ErrorType.Failure)
        };

        return Error.Create(ex.Message, code, errorType)
            .WithMetadata("Status", ex.Status);
    }

    /// <summary>
    /// Converts a <see cref="RequestFailedException"/> (Azure transport layer) into a
    /// <see cref="Error"/>.
    /// </summary>
    public static Error ToMonadicError(this RequestFailedException ex)
    {
        var (code, errorType) = ex.Status switch
        {
            401    => ("OPENAI_UNAUTHORIZED",    ErrorType.Forbidden),
            403    => ("OPENAI_FORBIDDEN",       ErrorType.Forbidden),
            404    => ("OPENAI_NOT_FOUND",       ErrorType.NotFound),
            429    => ("OPENAI_RATE_LIMITED",     ErrorType.Failure),
            >= 500 => ("OPENAI_SERVICE_ERROR",   ErrorType.Exception),
            _      => ("OPENAI_REQUEST_FAILED",  ErrorType.Failure)
        };

        return Error.Create(ex.Message, code, errorType)
            .WithMetadata("ErrorCode", ex.ErrorCode ?? string.Empty)
            .WithMetadata("Status",    ex.Status);
    }
}
