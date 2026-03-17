using System.ClientModel;
using Azure;
using FluentAssertions;
using Moq;
using OpenAI.Embeddings;
using MonadicSharp;
using MonadicSharp.Azure.OpenAI;
using MonadicSharp.Azure.OpenAI.Tests.Helpers;

namespace MonadicSharp.Azure.OpenAI.Tests;

public class EmbeddingClientExtensionsTests
{
    private readonly Mock<EmbeddingClient> _client = new();

    private static ClientResultException MakeClientException(int status, string msg = "error")
        => new(msg, new MockPipelineResponse(status));

    // ── EmbedAsync failure paths ──────────────────────────────────────────────

    [Fact]
    public async Task EmbedAsync_returns_failure_on_rate_limit()
    {
        _client.Setup(c => c.GenerateEmbeddingAsync(
                It.IsAny<string>(),
                It.IsAny<EmbeddingGenerationOptions?>(),
                It.IsAny<CancellationToken>()))
               .ThrowsAsync(MakeClientException(429, "Rate limit exceeded"));

        var result = await _client.Object.EmbedAsync("hello world");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OPENAI_RATE_LIMITED");
        result.Error.Type.Should().Be(ErrorType.Failure);
    }

    [Fact]
    public async Task EmbedAsync_returns_failure_on_unauthorized()
    {
        _client.Setup(c => c.GenerateEmbeddingAsync(
                It.IsAny<string>(),
                It.IsAny<EmbeddingGenerationOptions?>(),
                It.IsAny<CancellationToken>()))
               .ThrowsAsync(MakeClientException(401, "Unauthorized"));

        var result = await _client.Object.EmbedAsync("hello world");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task EmbedAsync_returns_failure_on_azure_transport_error()
    {
        _client.Setup(c => c.GenerateEmbeddingAsync(
                It.IsAny<string>(),
                It.IsAny<EmbeddingGenerationOptions?>(),
                It.IsAny<CancellationToken>()))
               .ThrowsAsync(new RequestFailedException(503, "ServiceUnavailable"));

        var result = await _client.Object.EmbedAsync("hello world");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Exception);
    }

    // ── EmbedBatchAsync failure paths ─────────────────────────────────────────

    [Fact]
    public async Task EmbedBatchAsync_returns_failure_on_rate_limit()
    {
        _client.Setup(c => c.GenerateEmbeddingsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<EmbeddingGenerationOptions?>(),
                It.IsAny<CancellationToken>()))
               .ThrowsAsync(MakeClientException(429, "Rate limit exceeded"));

        var result = await _client.Object.EmbedBatchAsync(["text1", "text2"]);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OPENAI_RATE_LIMITED");
    }

    [Fact]
    public async Task EmbedBatchAsync_returns_failure_on_service_error()
    {
        _client.Setup(c => c.GenerateEmbeddingsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<EmbeddingGenerationOptions?>(),
                It.IsAny<CancellationToken>()))
               .ThrowsAsync(MakeClientException(500, "Internal server error"));

        var result = await _client.Object.EmbedBatchAsync(["text1", "text2"]);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Exception);
    }
}
