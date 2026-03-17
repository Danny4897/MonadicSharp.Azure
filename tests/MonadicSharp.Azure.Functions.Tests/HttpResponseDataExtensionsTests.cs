using System.Net;
using System.Text.Json;
using FluentAssertions;
using Moq;
using Xunit;
using Microsoft.Azure.Functions.Worker;
using MonadicSharp;
using MonadicSharp.Azure.Core;
using MonadicSharp.Azure.Functions;
using MonadicSharp.Azure.Functions.Tests.Helpers;

namespace MonadicSharp.Azure.Functions.Tests;

public class HttpResponseDataExtensionsTests
{
    private static TestHttpRequestData CreateRequest(string body = "")
    {
        var context = new Mock<FunctionContext>().Object;
        return new TestHttpRequestData(context, body);
    }

    // ── ToHttpResponseAsync<T> ────────────────────────────────────────────────

    [Fact]
    public async Task Success_result_returns_200_with_json_body()
    {
        var request = CreateRequest();
        var result = Result<OrderDto>.Success(new OrderDto(42, "pending"));

        var response = await result.ToHttpResponseAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = ((TestHttpResponseData)response).ReadBody();
        body.Should().Contain("42");
        body.Should().Contain("pending");
    }

    [Fact]
    public async Task Success_result_uses_custom_status_code()
    {
        var request = CreateRequest();
        var result = Result<OrderDto>.Success(new OrderDto(1, "created"));

        var response = await result.ToHttpResponseAsync(request, HttpStatusCode.Created);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Failure_validation_returns_422_with_problem_details()
    {
        var request = CreateRequest();
        var result = Result<OrderDto>.Failure(Error.Validation("Name is required", "Name"));

        var response = await result.ToHttpResponseAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = ((TestHttpResponseData)response).ReadBody();
        body.Should().Contain("422");
        body.Should().Contain("Validation");
    }

    [Fact]
    public async Task Failure_not_found_returns_404()
    {
        var request = CreateRequest();
        var result = Result<OrderDto>.Failure(Error.NotFound("Order", "99"));

        var response = await result.ToHttpResponseAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Failure_forbidden_returns_403()
    {
        var request = CreateRequest();
        var result = Result<OrderDto>.Failure(Error.Forbidden("Not your order"));

        var response = await result.ToHttpResponseAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Failure_conflict_returns_409()
    {
        var request = CreateRequest();
        var result = Result<OrderDto>.Failure(Error.Conflict("Order already submitted"));

        var response = await result.ToHttpResponseAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Failure_exception_returns_500()
    {
        var request = CreateRequest();
        var result = Result<OrderDto>.Failure(Error.FromException(new Exception("db error")));

        var response = await result.ToHttpResponseAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    // ── Task<Result<T>> overload ──────────────────────────────────────────────

    [Fact]
    public async Task Task_success_returns_200()
    {
        var request = CreateRequest();
        var resultTask = Task.FromResult(Result<OrderDto>.Success(new OrderDto(5, "ok")));

        var response = await resultTask.ToHttpResponseAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Task_failure_returns_mapped_status()
    {
        var request = CreateRequest();
        var resultTask = Task.FromResult(Result<OrderDto>.Failure(Error.NotFound("Order", "1")));

        var response = await resultTask.ToHttpResponseAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Result<Unit> overload ─────────────────────────────────────────────────

    [Fact]
    public async Task Unit_success_returns_204_no_content()
    {
        var request = CreateRequest();
        var resultTask = Task.FromResult(Result<Unit>.Success(Unit.Value));

        var response = await resultTask.ToHttpResponseAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Unit_failure_returns_mapped_status()
    {
        var request = CreateRequest();
        var resultTask = Task.FromResult(Result<Unit>.Failure(Error.Forbidden()));

        var response = await resultTask.ToHttpResponseAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── ToCreatedResponseAsync ────────────────────────────────────────────────

    [Fact]
    public async Task Created_response_returns_201_with_location_header()
    {
        var request = CreateRequest();
        var resultTask = Task.FromResult(Result<OrderDto>.Success(new OrderDto(7, "new")));

        var response = await resultTask.ToCreatedResponseAsync(
            request,
            order => $"/api/orders/{order.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Should().ContainKey("Location");
    }

    // ── ReadFromJsonAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task ReadFromJsonAsync_deserializes_valid_body()
    {
        var request = CreateRequest("""{"id":10,"status":"pending"}""");

        var result = await request.ReadFromJsonAsync<OrderDto>();

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(10);
        result.Value.Status.Should().Be("pending");
    }

    [Fact]
    public async Task ReadFromJsonAsync_returns_failure_for_empty_body()
    {
        var request = CreateRequest("");

        var result = await request.ReadFromJsonAsync<OrderDto>();

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("empty");
    }

    [Fact]
    public async Task ReadAndValidateAsync_applies_validation()
    {
        var request = CreateRequest("""{"id":0,"status":"pending"}""");

        var result = await request.ReadAndValidateAsync<OrderDto>(
            order => order.Id > 0
                ? Result<OrderDto>.Success(order)
                : Result<OrderDto>.Failure(Error.Validation("Id must be > 0", "Id")));

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private record OrderDto(int Id, string Status);
}
