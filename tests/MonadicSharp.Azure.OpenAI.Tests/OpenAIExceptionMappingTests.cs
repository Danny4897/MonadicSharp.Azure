using System.ClientModel;
using Azure;
using FluentAssertions;
using MonadicSharp;
using MonadicSharp.Azure.OpenAI;
using MonadicSharp.Azure.OpenAI.Tests.Helpers;

namespace MonadicSharp.Azure.OpenAI.Tests;

public class OpenAIExceptionMappingTests
{
    // ── ClientResultException mapping ─────────────────────────────────────────

    private static ClientResultException MakeClientException(int status, string message = "openai error")
    {
        var response = new MockPipelineResponse(status);
        return new ClientResultException(message, response);
    }

    [Theory]
    [InlineData(401, ErrorType.Forbidden)]
    [InlineData(403, ErrorType.Forbidden)]
    [InlineData(404, ErrorType.NotFound)]
    [InlineData(429, ErrorType.Failure)]
    [InlineData(500, ErrorType.Exception)]
    [InlineData(503, ErrorType.Exception)]
    [InlineData(400, ErrorType.Failure)]
    public void ClientResultException_maps_status_to_correct_error_type(int status, ErrorType expected)
    {
        MakeClientException(status).ToMonadicError().Type.Should().Be(expected);
    }

    [Theory]
    [InlineData(401, "OPENAI_UNAUTHORIZED")]
    [InlineData(403, "OPENAI_FORBIDDEN")]
    [InlineData(404, "OPENAI_NOT_FOUND")]
    [InlineData(429, "OPENAI_RATE_LIMITED")]
    [InlineData(500, "OPENAI_SERVICE_ERROR")]
    [InlineData(400, "OPENAI_REQUEST_FAILED")]
    public void ClientResultException_maps_status_to_correct_error_code(int status, string expectedCode)
    {
        MakeClientException(status).ToMonadicError().Code.Should().Be(expectedCode);
    }

    [Fact]
    public void ClientResultException_preserves_message()
    {
        var error = MakeClientException(429, "Rate limit exceeded").ToMonadicError();
        error.Message.Should().Contain("Rate limit exceeded");
    }

    [Fact]
    public void ClientResultException_includes_status_in_metadata()
    {
        var error = MakeClientException(429).ToMonadicError();
        error.Metadata["Status"].Should().Be(429);
    }

    // ── RequestFailedException mapping ────────────────────────────────────────

    private static RequestFailedException MakeAzureException(int status, string message = "azure error") =>
        new(status, message);

    [Theory]
    [InlineData(401, ErrorType.Forbidden)]
    [InlineData(403, ErrorType.Forbidden)]
    [InlineData(429, ErrorType.Failure)]
    [InlineData(500, ErrorType.Exception)]
    [InlineData(400, ErrorType.Failure)]
    public void RequestFailedException_maps_status_to_correct_error_type(int status, ErrorType expected)
    {
        MakeAzureException(status).ToMonadicError().Type.Should().Be(expected);
    }

    [Fact]
    public void RequestFailedException_includes_status_in_metadata()
    {
        var error = MakeAzureException(503).ToMonadicError();
        error.Metadata["Status"].Should().Be(503);
    }
}
