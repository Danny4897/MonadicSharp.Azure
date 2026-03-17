using System.Text;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using FluentAssertions;
using Moq;
using MonadicSharp;
using MonadicSharp.Azure.Messaging;

namespace MonadicSharp.Azure.Messaging.Tests;

public class ServiceBusReceiverExtensionsTests
{
    private readonly Mock<ServiceBusReceiver> _receiver = new();

    private static ServiceBusReceivedMessage MakeMessage<T>(T payload)
    {
        var json = JsonSerializer.Serialize(payload,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var bytes = Encoding.UTF8.GetBytes(json);

        // ServiceBusReceivedMessage is sealed — use the internal factory via reflection
        // or test via the public DeserializeBody extension directly.
        return ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: new BinaryData(bytes),
            contentType: "application/json");
    }

    // ── DeserializeBody ───────────────────────────────────────────────────────

    [Fact]
    public void DeserializeBody_returns_success_for_valid_json()
    {
        var order = new OrderEvent("ord-1", "created", 50.0m);
        var message = MakeMessage(order);

        var result = message.DeserializeBody<OrderEvent>();

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be("ord-1");
        result.Value.Status.Should().Be("created");
    }

    [Fact]
    public void DeserializeBody_returns_failure_for_invalid_json()
    {
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: new BinaryData("{ not valid json }"u8.ToArray()));

        var result = message.DeserializeBody<OrderEvent>();

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void DeserializeBody_is_case_insensitive()
    {
        // JSON uses PascalCase, model uses camelCase — should still work
        var json = """{"Id":"ord-2","Status":"shipped","Amount":99.9}""";
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: new BinaryData(Encoding.UTF8.GetBytes(json)));

        var result = message.DeserializeBody<OrderEvent>();

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be("ord-2");
    }

    // ── DeserializeAndValidate ────────────────────────────────────────────────

    [Fact]
    public void DeserializeAndValidate_applies_validation_on_success()
    {
        var message = MakeMessage(new OrderEvent("ord-3", "unknown", 0m));

        var result = message.DeserializeAndValidate<OrderEvent>(e =>
            e.Amount > 0
                ? Result<OrderEvent>.Success(e)
                : Result<OrderEvent>.Failure(Error.Validation("Amount must be > 0", "Amount")));

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void DeserializeAndValidate_returns_success_when_valid()
    {
        var message = MakeMessage(new OrderEvent("ord-4", "created", 10m));

        var result = message.DeserializeAndValidate<OrderEvent>(e =>
            Result<OrderEvent>.Success(e));

        result.IsSuccess.Should().BeTrue();
    }

    // ── CompleteOrDeadLetterAsync ─────────────────────────────────────────────

    [Fact]
    public async Task CompleteOrDeadLetterAsync_completes_on_success()
    {
        var message = MakeMessage(new OrderEvent("ord-5", "ok", 1m));
        _receiver.Setup(r => r.CompleteMessageAsync(message, default))
                 .Returns(Task.CompletedTask);

        await Result<string>.Success("done")
            .CompleteOrDeadLetterAsync(_receiver.Object, message);

        _receiver.Verify(r => r.CompleteMessageAsync(message, default), Times.Once);
        _receiver.Verify(r => r.DeadLetterMessageAsync(
            It.IsAny<ServiceBusReceivedMessage>(),
            It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task CompleteOrDeadLetterAsync_dead_letters_on_failure()
    {
        var message = MakeMessage(new OrderEvent("ord-6", "bad", 0m));
        _receiver.Setup(r => r.DeadLetterMessageAsync(
                message, It.IsAny<string>(), It.IsAny<string>(), default))
            .Returns(Task.CompletedTask);

        await Result<string>.Failure(Error.Validation("bad message", "field"))
            .CompleteOrDeadLetterAsync(_receiver.Object, message);

        _receiver.Verify(r => r.CompleteMessageAsync(
            It.IsAny<ServiceBusReceivedMessage>(), default), Times.Never);
        _receiver.Verify(r => r.DeadLetterMessageAsync(
            message, "VALIDATION_ERROR", "bad message", default), Times.Once);
    }

    [Fact]
    public async Task CompleteOrDeadLetterAsync_task_overload_works()
    {
        var message = MakeMessage(new OrderEvent("ord-7", "ok", 5m));
        _receiver.Setup(r => r.CompleteMessageAsync(message, default))
                 .Returns(Task.CompletedTask);

        await Task.FromResult(Result<string>.Success("ok"))
            .CompleteOrDeadLetterAsync(_receiver.Object, message);

        _receiver.Verify(r => r.CompleteMessageAsync(message, default), Times.Once);
    }

    // ── ProcessBatchAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessBatchAsync_completes_successes_and_dead_letters_failures()
    {
        var goodMsg = MakeMessage(new OrderEvent("ord-8", "created", 10m));
        var badMsg  = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: new BinaryData("garbage"u8.ToArray()));

        _receiver.Setup(r => r.CompleteMessageAsync(goodMsg, default))
                 .Returns(Task.CompletedTask);
        _receiver.Setup(r => r.DeadLetterMessageAsync(
                badMsg, It.IsAny<string>(), It.IsAny<string>(), default))
            .Returns(Task.CompletedTask);

        var (successes, failures) = await _receiver.Object.ProcessBatchAsync<OrderEvent>(
            [goodMsg, badMsg],
            process: e => Task.FromResult(Result<OrderEvent>.Success(e)));

        successes.Should().ContainSingle();
        failures.Should().ContainSingle();
        _receiver.Verify(r => r.CompleteMessageAsync(goodMsg, default), Times.Once);
        _receiver.Verify(r => r.DeadLetterMessageAsync(
            badMsg, It.IsAny<string>(), It.IsAny<string>(), default), Times.Once);
    }

    [Fact]
    public async Task ProcessBatchAsync_dead_letters_when_process_returns_failure()
    {
        var message = MakeMessage(new OrderEvent("ord-9", "created", 10m));

        _receiver.Setup(r => r.DeadLetterMessageAsync(
                message, It.IsAny<string>(), It.IsAny<string>(), default))
            .Returns(Task.CompletedTask);

        var (successes, failures) = await _receiver.Object.ProcessBatchAsync<OrderEvent>(
            [message],
            process: _ => Task.FromResult(
                Result<OrderEvent>.Failure(Error.Create("downstream failed"))));

        successes.Should().BeEmpty();
        failures.Should().ContainSingle();
    }

    private record OrderEvent(string Id, string Status, decimal Amount);
}
