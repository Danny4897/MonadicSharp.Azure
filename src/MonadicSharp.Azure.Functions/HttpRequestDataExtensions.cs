using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;
using MonadicSharp;

namespace MonadicSharp.Azure.Functions;

/// <summary>
/// Extension methods for deserializing Azure Functions HTTP request bodies
/// into <see cref="Result{T}"/> values.
/// </summary>
public static class HttpRequestDataExtensions
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Reads and deserializes the request body as <typeparamref name="T"/>.
    /// Returns <c>Result.Failure</c> if the body is empty or cannot be deserialized.
    /// </summary>
    public static async Task<Result<T>> ReadFromJsonAsync<T>(
        this HttpRequestData request,
        JsonSerializerOptions? options = null)
    {
        return await Try.ExecuteAsync(async () =>
        {
            using var reader = new StreamReader(request.Body);
            var body = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(body))
                throw new InvalidOperationException("Request body is empty.");

            var value = JsonSerializer.Deserialize<T>(body, options ?? DefaultOptions);
            return value ?? throw new InvalidOperationException(
                $"Could not deserialize request body to {typeof(T).Name}.");
        });
    }

    /// <summary>
    /// Reads and deserializes the request body, then applies <paramref name="validate"/>
    /// to the deserialized value. Combines deserialization and domain validation in one step.
    /// </summary>
    public static async Task<Result<T>> ReadAndValidateAsync<T>(
        this HttpRequestData request,
        Func<T, Result<T>> validate,
        JsonSerializerOptions? options = null)
    {
        var result = await request.ReadFromJsonAsync<T>(options);
        return result.Bind(validate);
    }
}
