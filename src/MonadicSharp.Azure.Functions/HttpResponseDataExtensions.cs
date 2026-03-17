using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Functions.Worker.Http;
using MonadicSharp;
using MonadicSharp.Azure.Core;

namespace MonadicSharp.Azure.Functions;

/// <summary>
/// Extension methods for converting <see cref="Result{T}"/> to Azure Functions
/// <see cref="HttpResponseData"/>, with automatic error-to-HTTP-status mapping.
/// </summary>
public static class HttpResponseDataExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static async Task WriteJsonBodyAsync<T>(HttpResponseData response, T value)
    {
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        var json = JsonSerializer.Serialize(value, JsonOptions);
        await response.Body.WriteAsync(Encoding.UTF8.GetBytes(json));
    }

    /// <summary>
    /// Awaits the result and writes an HTTP response.
    /// Success → <paramref name="successStatus"/> with the value serialized as JSON.
    /// Failure → mapped HTTP status with a <see cref="MonadicProblemDetails"/> body.
    /// </summary>
    public static async Task<HttpResponseData> ToHttpResponseAsync<T>(
        this Task<Result<T>> resultTask,
        HttpRequestData request,
        HttpStatusCode successStatus = HttpStatusCode.OK)
    {
        var result = await resultTask;
        return await result.ToHttpResponseAsync(request, successStatus);
    }

    /// <summary>
    /// Writes an HTTP response from a <see cref="Result{T}"/>.
    /// Success → <paramref name="successStatus"/> with the value serialized as JSON.
    /// Failure → mapped HTTP status with a <see cref="MonadicProblemDetails"/> body.
    /// </summary>
    public static async Task<HttpResponseData> ToHttpResponseAsync<T>(
        this Result<T> result,
        HttpRequestData request,
        HttpStatusCode successStatus = HttpStatusCode.OK)
    {
        if (result.IsSuccess)
        {
            var ok = request.CreateResponse(successStatus);
            await WriteJsonBodyAsync(ok, result.Value);
            return ok;
        }

        var error = request.CreateResponse(result.Error.ToHttpStatusCode());
        await WriteJsonBodyAsync(error, new MonadicProblemDetails(result.Error));
        return error;
    }

    /// <summary>
    /// Awaits and writes an HTTP response for <c>Result&lt;Unit&gt;</c> (command operations).
    /// Success → 204 No Content.
    /// Failure → mapped HTTP status with a <see cref="MonadicProblemDetails"/> body.
    /// </summary>
    public static async Task<HttpResponseData> ToHttpResponseAsync(
        this Task<Result<Unit>> resultTask,
        HttpRequestData request,
        HttpStatusCode successStatus = HttpStatusCode.NoContent)
    {
        var result = await resultTask;

        if (result.IsSuccess)
            return request.CreateResponse(successStatus);

        var error = request.CreateResponse(result.Error.ToHttpStatusCode());
        await WriteJsonBodyAsync(error, new MonadicProblemDetails(result.Error));
        return error;
    }

    /// <summary>
    /// Creates a 201 Created response with the value and an optional Location header.
    /// </summary>
    public static async Task<HttpResponseData> ToCreatedResponseAsync<T>(
        this Task<Result<T>> resultTask,
        HttpRequestData request,
        Func<T, string>? locationFactory = null)
    {
        var result = await resultTask;

        if (result.IsSuccess)
        {
            var created = request.CreateResponse(HttpStatusCode.Created);
            if (locationFactory != null)
                created.Headers.Add("Location", locationFactory(result.Value));
            await WriteJsonBodyAsync(created, result.Value);
            return created;
        }

        var error = request.CreateResponse(result.Error.ToHttpStatusCode());
        await WriteJsonBodyAsync(error, new MonadicProblemDetails(result.Error));
        return error;
    }
}
