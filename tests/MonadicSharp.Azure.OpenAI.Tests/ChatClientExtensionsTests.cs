using System.ClientModel;
using Azure;
using FluentAssertions;
using Moq;
using OpenAI.Chat;
using MonadicSharp;
using MonadicSharp.Azure.OpenAI;
using MonadicSharp.Azure.OpenAI.Tests.Helpers;

namespace MonadicSharp.Azure.OpenAI.Tests;

public class ChatClientExtensionsTests
{
    private readonly Mock<ChatClient> _client = new();

    private static ClientResultException MakeClientException(int status, string msg = "error")
        => new(msg, new MockPipelineResponse(status));

    // ── ChatAsync failure paths ──────────────────────────────────────────────

    [Fact]
    public async Task ChatAsync_returns_failure_on_rate_limit()
    {
        _client.Setup(c => c.CompleteChatAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatCompletionOptions?>(),
                It.IsAny<CancellationToken>()))
               .ThrowsAsync(MakeClientException(429, "Rate limit exceeded"));

        var result = await _client.Object.ChatAsync(
            [ChatMessage.CreateUserMessage("hello")]);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OPENAI_RATE_LIMITED");
        result.Error.Type.Should().Be(ErrorType.Failure);
    }

    [Fact]
    public async Task ChatAsync_returns_failure_on_unauthorized()
    {
        _client.Setup(c => c.CompleteChatAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatCompletionOptions?>(),
                It.IsAny<CancellationToken>()))
               .ThrowsAsync(MakeClientException(401, "Unauthorized"));

        var result = await _client.Object.ChatAsync(
            [ChatMessage.CreateUserMessage("hello")]);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task ChatAsync_returns_failure_on_service_error()
    {
        _client.Setup(c => c.CompleteChatAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatCompletionOptions?>(),
                It.IsAny<CancellationToken>()))
               .ThrowsAsync(MakeClientException(500, "Internal server error"));

        var result = await _client.Object.ChatAsync(
            [ChatMessage.CreateUserMessage("hello")]);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Exception);
        result.Error.Code.Should().Be("OPENAI_SERVICE_ERROR");
    }

    [Fact]
    public async Task ChatAsync_returns_failure_on_azure_transport_error()
    {
        _client.Setup(c => c.CompleteChatAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatCompletionOptions?>(),
                It.IsAny<CancellationToken>()))
               .ThrowsAsync(new RequestFailedException(503, "ServiceUnavailable"));

        var result = await _client.Object.ChatAsync(
            [ChatMessage.CreateUserMessage("hello")]);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Exception);
    }

    // ── ChatWithDetailsAsync failure paths ────────────────────────────────────

    [Fact]
    public async Task ChatWithDetailsAsync_returns_failure_on_forbidden()
    {
        _client.Setup(c => c.CompleteChatAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatCompletionOptions?>(),
                It.IsAny<CancellationToken>()))
               .ThrowsAsync(MakeClientException(403, "Forbidden"));

        var result = await _client.Object.ChatWithDetailsAsync(
            [ChatMessage.CreateSystemMessage("You are helpful")]);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OPENAI_FORBIDDEN");
    }
}
