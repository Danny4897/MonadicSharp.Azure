using Azure;
using FluentAssertions;
using MonadicSharp;
using MonadicSharp.Azure.Storage;

namespace MonadicSharp.Azure.Storage.Tests;

public class BlobExceptionMappingTests
{
    private static RequestFailedException MakeException(int status, string message = "blob error") =>
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
    [InlineData(404, "BLOB_NOT_FOUND")]
    [InlineData(409, "BLOB_CONFLICT")]
    [InlineData(403, "BLOB_ACCESS_DENIED")]
    [InlineData(400, "BLOB_INVALID_REQUEST")]
    [InlineData(500, "BLOB_SERVICE_ERROR")]
    [InlineData(408, "BLOB_REQUEST_FAILED")]
    public void Maps_status_to_correct_error_code(int status, string expectedCode)
    {
        MakeException(status).ToMonadicError().Code.Should().Be(expectedCode);
    }

    [Fact]
    public void Preserves_original_message()
    {
        var error = MakeException(404, "container not found").ToMonadicError();
        error.Message.Should().Contain("container not found");
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
        var ex    = new RequestFailedException(404, "Not Found", "BlobNotFound", null);
        var error = ex.ToMonadicError();
        error.Metadata["ErrorCode"].Should().Be("BlobNotFound");
    }
}
