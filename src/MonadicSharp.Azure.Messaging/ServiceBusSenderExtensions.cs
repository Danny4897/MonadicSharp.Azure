using System.Text;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using MonadicSharp;

namespace MonadicSharp.Azure.Messaging;

/// <summary>
/// Extension methods for <see cref="ServiceBusSender"/> that wrap send operations
/// in <see cref="Result{T}"/> for Railway-Oriented Programming.
/// </summary>
public static class ServiceBusSenderExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Serializes <paramref name="payload"/> to JSON and sends it as a Service Bus message.
    /// Returns <c>Result.Failure</c> on any send error.
    /// </summary>
    public static async Task<Result<Unit>> SendAsync<T>(
        this ServiceBusSender sender,
        T payload,
        Action<ServiceBusMessage>? configure = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            var message = new ServiceBusMessage(Encoding.UTF8.GetBytes(json))
            {
                ContentType = "application/json"
            };
            configure?.Invoke(message);
            await sender.SendMessageAsync(message, cancellationToken);
            return Result<Unit>.Success(Unit.Value);
        }
        catch (ServiceBusException ex)
        {
            return Result<Unit>.Failure(ex.ToMonadicError());
        }
    }

    /// <summary>
    /// Sends a pre-built <see cref="ServiceBusMessage"/>.
    /// Returns <c>Result.Failure</c> on any send error.
    /// Use this overload when you have already constructed the message manually.
    /// </summary>
    public static async Task<Result<Unit>> SendRawAsync(
        this ServiceBusSender sender,
        ServiceBusMessage message,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await sender.SendMessageAsync(message, cancellationToken);
            return Result<Unit>.Success(Unit.Value);
        }
        catch (ServiceBusException ex)
        {
            return Result<Unit>.Failure(ex.ToMonadicError());
        }
    }

    /// <summary>
    /// Serializes and sends a batch of messages.
    /// Collects per-message results — does not short-circuit on partial failures.
    /// Use <c>results.Partition()</c> to separate successes from failures.
    /// </summary>
    public static async Task<IReadOnlyList<Result<Unit>>> SendBatchAsync<T>(
        this ServiceBusSender sender,
        IEnumerable<T> payloads,
        Action<T, ServiceBusMessage>? configure = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<Result<Unit>>();

        foreach (var payload in payloads)
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            var message = new ServiceBusMessage(Encoding.UTF8.GetBytes(json))
            {
                ContentType = "application/json"
            };
            configure?.Invoke(payload, message);

            try
            {
                await sender.SendMessageAsync(message, cancellationToken);
                results.Add(Result<Unit>.Success(Unit.Value));
            }
            catch (ServiceBusException ex)
            {
                results.Add(Result<Unit>.Failure(ex.ToMonadicError()));
            }
        }

        return results;
    }
}
