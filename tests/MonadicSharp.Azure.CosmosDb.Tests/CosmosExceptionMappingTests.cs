using System.Net;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using MonadicSharp;
using MonadicSharp.Azure.CosmosDb;

namespace MonadicSharp.Azure.CosmosDb.Tests;

public class CosmosExceptionMappingTests
{
    private static CosmosException MakeException(HttpStatusCode status, string message = "cosmos error") =>
        new(message, status, 0, "activity-123", 1.5);

    // ── ErrorType mapping ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(HttpStatusCode.NotFound,                ErrorType.NotFound)]
    [InlineData(HttpStatusCode.Conflict,                ErrorType.Conflict)]
    [InlineData(HttpStatusCode.Forbidden,               ErrorType.Forbidden)]
    [InlineData(HttpStatusCode.BadRequest,              ErrorType.Validation)]
    [InlineData(HttpStatusCode.TooManyRequests,         ErrorType.Failure)]
    [InlineData(HttpStatusCode.InternalServerError,     ErrorType.Exception)]
    [InlineData(HttpStatusCode.ServiceUnavailable,      ErrorType.Exception)]
    public void Maps_status_code_to_correct_error_type(HttpStatusCode status, ErrorType expected)
    {
        var ex = MakeException(status);
        var error = ex.ToMonadicError();
        error.Type.Should().Be(expected);
    }

    // ── Error code mapping ────────────────────────────────────────────────────

    [Theory]
    [InlineData(HttpStatusCode.NotFound,            "COSMOS_NOT_FOUND")]
    [InlineData(HttpStatusCode.Conflict,            "COSMOS_CONFLICT")]
    [InlineData(HttpStatusCode.Forbidden,           "COSMOS_FORBIDDEN")]
    [InlineData(HttpStatusCode.BadRequest,          "COSMOS_BAD_REQUEST")]
    [InlineData(HttpStatusCode.TooManyRequests,     "COSMOS_RATE_LIMITED")]
    [InlineData(HttpStatusCode.InternalServerError, "COSMOS_SERVER_ERROR")]
    public void Maps_status_code_to_correct_error_code(HttpStatusCode status, string expectedCode)
    {
        var ex = MakeException(status);
        var error = ex.ToMonadicError();
        error.Code.Should().Be(expectedCode);
    }

    // ── Metadata preservation ─────────────────────────────────────────────────

    [Fact]
    public void Preserves_status_code_in_metadata()
    {
        var ex = MakeException(HttpStatusCode.NotFound);
        var error = ex.ToMonadicError();
        error.Metadata["StatusCode"].Should().Be(404);
    }

    [Fact]
    public void Preserves_activity_id_in_metadata()
    {
        var ex = MakeException(HttpStatusCode.NotFound);
        var error = ex.ToMonadicError();
        error.Metadata["ActivityId"].Should().Be("activity-123");
    }

    [Fact]
    public void Preserves_request_charge_in_metadata()
    {
        var ex = MakeException(HttpStatusCode.NotFound);
        var error = ex.ToMonadicError();
        error.Metadata["RequestCharge"].Should().Be(1.5);
    }

    [Fact]
    public void Preserves_original_message()
    {
        var ex = MakeException(HttpStatusCode.BadRequest, "Invalid partition key");
        var error = ex.ToMonadicError();
        error.Message.Should().Be("Invalid partition key");
    }
}
