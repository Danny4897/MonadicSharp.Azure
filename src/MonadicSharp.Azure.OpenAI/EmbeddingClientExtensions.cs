using System.ClientModel;
using Azure;
using OpenAI.Embeddings;
using MonadicSharp;

namespace MonadicSharp.Azure.OpenAI;

/// <summary>
/// Extension methods for <see cref="EmbeddingClient"/> that wrap embedding generation
/// in <see cref="Result{T}"/> for Railway-Oriented Programming.
/// </summary>
public static class EmbeddingClientExtensions
{
    // ── Single embedding ─────────────────────────────────────────────────────

    /// <summary>
    /// Generates an embedding vector for <paramref name="input"/> and returns it as
    /// a <see cref="Result{T}"/> of <see cref="ReadOnlyMemory{Single}"/>.
    /// </summary>
    public static async Task<Result<ReadOnlyMemory<float>>> EmbedAsync(
        this EmbeddingClient client,
        string input,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await client.GenerateEmbeddingAsync(input, options, cancellationToken);
            return Result<ReadOnlyMemory<float>>.Success(result.Value.ToFloats());
        }
        catch (ClientResultException ex)
        {
            return Result<ReadOnlyMemory<float>>.Failure(ex.ToMonadicError());
        }
        catch (RequestFailedException ex)
        {
            return Result<ReadOnlyMemory<float>>.Failure(ex.ToMonadicError());
        }
    }

    // ── Batch embeddings ──────────────────────────────────────────────────────

    /// <summary>
    /// Generates embedding vectors for a batch of <paramref name="inputs"/> and
    /// returns them in order as a <see cref="Result{T}"/> of a list of vectors.
    /// </summary>
    public static async Task<Result<IReadOnlyList<ReadOnlyMemory<float>>>> EmbedBatchAsync(
        this EmbeddingClient client,
        IEnumerable<string> inputs,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await client.GenerateEmbeddingsAsync(
                inputs, options, cancellationToken);

            var vectors = result.Value
                .OrderBy(e => e.Index)
                .Select(e => e.ToFloats())
                .ToList();

            return Result<IReadOnlyList<ReadOnlyMemory<float>>>.Success(vectors);
        }
        catch (ClientResultException ex)
        {
            return Result<IReadOnlyList<ReadOnlyMemory<float>>>.Failure(ex.ToMonadicError());
        }
        catch (RequestFailedException ex)
        {
            return Result<IReadOnlyList<ReadOnlyMemory<float>>>.Failure(ex.ToMonadicError());
        }
    }
}
