using Azure;
using FluentAssertions;
using MonadicSharp;
using MonadicSharp.Azure.KeyVault;

namespace MonadicSharp.Azure.KeyVault.Tests;

public class KeyVaultExceptionMappingTests
{
    private static RequestFailedException MakeException(int status, string message = "kv error") =>
        new(status, message);

    [Theory]
    [InlineData(404, ErrorType.NotFound)]
    [InlineData(409, ErrorType.Conflict)]
    [InlineData(403, ErrorType.Forbidden)]
    [InlineData(400, ErrorType.Validation)]
    [InlineData(500, ErrorType.Exception)]
    [InlineData(503, ErrorType.Exception)]
    [InlineData(408, ErrorType.Failure)]
    public void Maps_status_to_correct_error_type(int status, ErrorType expected)
    {
        MakeException(status).ToMonadicError().Type.Should().Be(expected);
    }

    [Theory]
    [InlineData(404, "KV_SECRET_NOT_FOUND")]
    [InlineData(409, "KV_SECRET_CONFLICT")]
    [InlineData(403, "KV_ACCESS_DENIED")]
    [InlineData(400, "KV_INVALID_REQUEST")]
    [InlineData(500, "KV_SERVICE_ERROR")]
    [InlineData(408, "KV_REQUEST_FAILED")]
    public void Maps_status_to_correct_error_code(int status, string expectedCode)
    {
        MakeException(status).ToMonadicError().Code.Should().Be(expectedCode);
    }

    [Fact]
    public void Preserves_original_message()
    {
        var error = MakeException(404, "secret not found").ToMonadicError();
        error.Message.Should().Contain("secret not found");
    }

    [Fact]
    public void Includes_status_in_metadata()
    {
        var error = MakeException(403).ToMonadicError();
        error.Metadata["Status"].Should().Be(403);
    }

    [Fact]
    public void Includes_error_code_in_metadata()
    {
        var ex    = new RequestFailedException(403, "Forbidden", "Forbidden", null);
        var error = ex.ToMonadicError();
        error.Metadata["ErrorCode"].Should().Be("Forbidden");
    }
}
