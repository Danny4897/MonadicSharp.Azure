using System.Net;
using FluentAssertions;
using MonadicSharp;
using MonadicSharp.Azure.Core;

namespace MonadicSharp.Azure.Core.Tests;

public class ErrorHttpMappingTests
{
    // ── ErrorType → HttpStatusCode ────────────────────────────────────────────

    [Theory]
    [InlineData(ErrorType.Validation, HttpStatusCode.UnprocessableEntity)]
    [InlineData(ErrorType.NotFound,   HttpStatusCode.NotFound)]
    [InlineData(ErrorType.Forbidden,  HttpStatusCode.Forbidden)]
    [InlineData(ErrorType.Conflict,   HttpStatusCode.Conflict)]
    [InlineData(ErrorType.Exception,  HttpStatusCode.InternalServerError)]
    [InlineData(ErrorType.Failure,    HttpStatusCode.BadRequest)]
    public void ErrorType_maps_to_correct_http_status(ErrorType type, HttpStatusCode expected)
    {
        type.ToHttpStatusCode().Should().Be(expected);
    }

    [Fact]
    public void Error_Validation_maps_to_422()
    {
        var error = Error.Validation("Name is required", field: "Name");
        error.ToHttpStatusCode().Should().Be(HttpStatusCode.UnprocessableEntity);
        error.ToHttpStatusInt().Should().Be(422);
    }

    [Fact]
    public void Error_NotFound_maps_to_404()
    {
        var error = Error.NotFound("Order", "42");
        error.ToHttpStatusCode().Should().Be(HttpStatusCode.NotFound);
        error.ToHttpStatusInt().Should().Be(404);
    }

    [Fact]
    public void Error_Forbidden_maps_to_403()
    {
        var error = Error.Forbidden();
        error.ToHttpStatusCode().Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public void Error_Conflict_maps_to_409()
    {
        var error = Error.Conflict("Order already exists", "Order");
        error.ToHttpStatusCode().Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public void Error_FromException_maps_to_500()
    {
        var error = Error.FromException(new InvalidOperationException("boom"));
        error.ToHttpStatusCode().Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public void Error_generic_Failure_maps_to_400()
    {
        var error = Error.Create("something went wrong");
        error.ToHttpStatusCode().Should().Be(HttpStatusCode.BadRequest);
    }

    // ── MonadicProblemDetails ─────────────────────────────────────────────────

    [Fact]
    public void MonadicProblemDetails_from_validation_error_has_correct_fields()
    {
        var error = Error.Validation("Email is invalid", field: "Email");
        var problem = new MonadicProblemDetails(error);

        problem.Status.Should().Be(422);
        problem.Title.Should().Be("Validation");
        problem.Detail.Should().Be("Email is invalid");
        problem.Code.Should().Be("VALIDATION_ERROR");
        problem.Type.Should().Contain("validation");
    }

    [Fact]
    public void MonadicProblemDetails_includes_metadata_as_extensions()
    {
        var error = Error.NotFound("Product", "sku-99");
        var problem = new MonadicProblemDetails(error);

        problem.Extensions.Should().ContainKey("Identifier");
        problem.Extensions!["Identifier"].Should().Be("sku-99");
    }

    [Fact]
    public void MonadicProblemDetails_extensions_is_null_when_no_metadata()
    {
        var error = Error.Create("generic error");
        var problem = new MonadicProblemDetails(error);

        problem.Extensions.Should().BeNull();
    }
}
