using System.Text;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using MonadicSharp;
using MonadicSharp.Extensions;

namespace MonadicSharp.Azure.Messaging;

/// <summary>
/// Extension methods for <see cref="ServiceBusReceivedMessage"/> and
/// <see cref="ServiceBusReceiver"/> that integrate with MonadicSharp patterns.
/// </summary>
public static class ServiceBusReceiverExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // ── Deserialization ───────────────────────────────────────────────────────

    /// <summary>
    /// Deserializes the message body from JSON into <typeparamref name="T"/>.
    /// Returns <c>Result.Failure(Validation)</c> if deserialization fails.
    /// </summary>
    public static Result<T> DeserializeBody<T>(this ServiceBusReceivedMessage message)
    {
        return Try.Execute(() =>
        {
            var json = Encoding.UTF8.GetString(message.Body);
            var value = JsonSerializer.Deserialize<T>(json, JsonOptions);
            return value ?? throw new InvalidOperationException(
                $"Message body deserialized to null for type {typeof(T).Name}.");
        });
    }

    /// <summary>
    /// Deserializes the message body and applies <paramref name="validate"/>
    /// to the result. Combines deserialization and domain validation in one step.
    /// </summary>
    public static Result<T> DeserializeAndValidate<T>(
        this ServiceBusReceivedMessage message,
        Func<T, Result<T>> validate)
    {
        return message.DeserializeBody<T>().Bind(validate);
    }

    // ── Complete / Dead-letter ────────────────────────────────────────────────

    /// <summary>
    /// Completes the message if <paramref name="result"/> is Success,
    /// otherwise dead-letters it with the error details.
    /// </summary>
    public static async Task CompleteOrDeadLetterAsync<T>(
        this Result<T> result,
        ServiceBusReceiver receiver,
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken = default)
    {
        if (result.IsSuccess)
        {
            await receiver.CompleteMessageAsync(message, cancellationToken);
        }
        else
        {
            await receiver.DeadLetterMessageAsync(
                message,
                deadLetterReason: result.Error.Code,
                deadLetterErrorDescription: result.Error.Message,
                cancellationToken: cancellationToken);
        }
    }

    /// <summary>
    /// Completes the message if <paramref name="result"/> is Success,
    /// otherwise dead-letters it with the error details.
    /// Task overload — awaits the result before deciding.
    /// </summary>
    public static async Task CompleteOrDeadLetterAsync<T>(
        this Task<Result<T>> resultTask,
        ServiceBusReceiver receiver,
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken = default)
    {
        var result = await resultTask;
        await result.CompleteOrDeadLetterAsync(receiver, message, cancellationToken);
    }

    // ── Batch processing ──────────────────────────────────────────────────────

    /// <summary>
    /// Receives up to <paramref name="maxMessages"/> messages and deserializes each
    /// one into <typeparamref name="T"/>. Returns a list of
    /// (message, Result&lt;T&gt;) pairs — preserving the original message for
    /// complete/dead-letter decisions downstream.
    /// </summary>
    public static async Task<IReadOnlyList<(ServiceBusReceivedMessage Message, Result<T> Result)>>
        ReceiveAndDeserializeAsync<T>(
            this ServiceBusReceiver receiver,
            int maxMessages = 10,
            TimeSpan? maxWaitTime = null,
            CancellationToken cancellationToken = default)
    {
        var messages = await receiver.ReceiveMessagesAsync(
            maxMessages,
            maxWaitTime ?? TimeSpan.FromSeconds(5),
            cancellationToken);

        return messages
            .Select(msg => (msg, msg.DeserializeBody<T>()))
            .ToList();
    }

    /// <summary>
    /// Processes a batch of received messages through <paramref name="process"/>,
    /// then completes successes and dead-letters failures atomically.
    /// Returns the partition of (successes, failures) for observability.
    /// </summary>
    public static async Task<(IEnumerable<T> Succeeded, IEnumerable<Error> Failed)>
        ProcessBatchAsync<T>(
            this ServiceBusReceiver receiver,
            IEnumerable<ServiceBusReceivedMessage> messages,
            Func<T, Task<Result<T>>> process,
            CancellationToken cancellationToken = default)
    {
        var results = new List<Result<T>>();

        foreach (var message in messages)
        {
            var deserialized = message.DeserializeBody<T>();

            if (deserialized.IsFailure)
            {
                await receiver.DeadLetterMessageAsync(
                    message,
                    deadLetterReason: deserialized.Error.Code,
                    deadLetterErrorDescription: deserialized.Error.Message,
                    cancellationToken: cancellationToken);
                results.Add(deserialized);
                continue;
            }

            var processed = await process(deserialized.Value);
            await processed.CompleteOrDeadLetterAsync(receiver, message, cancellationToken);
            results.Add(processed);
        }

        return (
            results.Where(r => r.IsSuccess).Select(r => r.Value),
            results.Where(r => r.IsFailure).Select(r => r.Error)
        );
    }
}
