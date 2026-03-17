using System.Net;
using MonadicSharp;

namespace MonadicSharp.Azure.Core;

/// <summary>
/// Maps MonadicSharp <see cref="ErrorType"/> values to HTTP status codes,
/// following RFC 9110 semantics.
/// </summary>
public static class ErrorHttpMapping
{
    /// <summary>
    /// Returns the HTTP status code that best represents this <see cref="ErrorType"/>.
    /// </summary>
    public static HttpStatusCode ToHttpStatusCode(this ErrorType type) => type switch
    {
        ErrorType.Validation => HttpStatusCode.UnprocessableEntity,  // 422
        ErrorType.NotFound   => HttpStatusCode.NotFound,             // 404
        ErrorType.Forbidden  => HttpStatusCode.Forbidden,            // 403
        ErrorType.Conflict   => HttpStatusCode.Conflict,             // 409
        ErrorType.Exception  => HttpStatusCode.InternalServerError,  // 500
        _                    => HttpStatusCode.BadRequest,           // 400
    };

    /// <summary>
    /// Returns the HTTP status code that best represents this <see cref="Error"/>.
    /// </summary>
    public static HttpStatusCode ToHttpStatusCode(this Error error) =>
        error.Type.ToHttpStatusCode();

    /// <summary>
    /// Returns the integer HTTP status code for this <see cref="Error"/>.
    /// </summary>
    public static int ToHttpStatusInt(this Error error) =>
        (int)error.ToHttpStatusCode();
}
