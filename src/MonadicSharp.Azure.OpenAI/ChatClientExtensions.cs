using System.ClientModel;
using Azure;
using OpenAI.Chat;
using MonadicSharp;

namespace MonadicSharp.Azure.OpenAI;

/// <summary>
/// Extension methods for <see cref="ChatClient"/> that wrap chat completion calls
/// in <see cref="Result{T}"/> for Railway-Oriented Programming.
/// </summary>
public static class ChatClientExtensions
{
    // ── Text response ────────────────────────────────────────────────────────

    /// <summary>
    /// Sends a chat completion request and returns the first text content as a
    /// <see cref="Result{T}"/>. Use when you only need the plain-text reply.
    /// </summary>
    public static async Task<Result<string>> ChatAsync(
        this ChatClient client,
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await client.CompleteChatAsync(
                messages, options, cancellationToken);

            var text = result.Value.Content.FirstOrDefault()?.Text ?? string.Empty;
            return Result<string>.Success(text);
        }
        catch (ClientResultException ex)
        {
            return Result<string>.Failure(ex.ToMonadicError());
        }
        catch (RequestFailedException ex)
        {
            return Result<string>.Failure(ex.ToMonadicError());
        }
    }

    // ── Full ChatCompletion ───────────────────────────────────────────────────

    /// <summary>
    /// Sends a chat completion request and returns the full <see cref="ChatCompletion"/>
    /// object wrapped in a <see cref="Result{T}"/>.
    /// Use when you need finish reason, usage, tool calls, or multiple choices.
    /// </summary>
    public static async Task<Result<ChatCompletion>> ChatWithDetailsAsync(
        this ChatClient client,
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await client.CompleteChatAsync(
                messages, options, cancellationToken);

            return Result<ChatCompletion>.Success(result.Value);
        }
        catch (ClientResultException ex)
        {
            return Result<ChatCompletion>.Failure(ex.ToMonadicError());
        }
        catch (RequestFailedException ex)
        {
            return Result<ChatCompletion>.Failure(ex.ToMonadicError());
        }
    }
}
