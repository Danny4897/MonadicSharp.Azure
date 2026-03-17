using Azure.Messaging.ServiceBus;
using FluentAssertions;
using MonadicSharp;
using MonadicSharp.Azure.Messaging;

namespace MonadicSharp.Azure.Messaging.Tests;

public class ServiceBusExceptionMappingTests
{
    private static ServiceBusException MakeException(
        ServiceBusFailureReason reason,
        string message = "sb error") =>
        new(message, reason);

    [Theory]
    [InlineData(ServiceBusFailureReason.MessagingEntityNotFound,     ErrorType.NotFound)]
    [InlineData(ServiceBusFailureReason.MessageNotFound,             ErrorType.NotFound)]
    [InlineData(ServiceBusFailureReason.MessagingEntityDisabled,     ErrorType.Forbidden)]
    [InlineData(ServiceBusFailureReason.QuotaExceeded,               ErrorType.Failure)]
    [InlineData(ServiceBusFailureReason.MessageSizeExceeded,         ErrorType.Validation)]
    [InlineData(ServiceBusFailureReason.ServiceCommunicationProblem, ErrorType.Exception)]
    [InlineData(ServiceBusFailureReason.ServiceTimeout,              ErrorType.Exception)]
    public void Maps_reason_to_correct_error_type(ServiceBusFailureReason reason, ErrorType expected)
    {
        MakeException(reason).ToMonadicError().Type.Should().Be(expected);
    }

    [Theory]
    [InlineData(ServiceBusFailureReason.MessagingEntityNotFound,     "SB_ENTITY_NOT_FOUND")]
    [InlineData(ServiceBusFailureReason.MessageNotFound,             "SB_MESSAGE_NOT_FOUND")]
    [InlineData(ServiceBusFailureReason.MessagingEntityDisabled,     "SB_ENTITY_DISABLED")]
    [InlineData(ServiceBusFailureReason.QuotaExceeded,               "SB_QUOTA_EXCEEDED")]
    [InlineData(ServiceBusFailureReason.MessageSizeExceeded,         "SB_MESSAGE_TOO_LARGE")]
    [InlineData(ServiceBusFailureReason.ServiceCommunicationProblem, "SB_COMMUNICATION_ERROR")]
    [InlineData(ServiceBusFailureReason.ServiceTimeout,              "SB_TIMEOUT")]
    public void Maps_reason_to_correct_error_code(ServiceBusFailureReason reason, string expectedCode)
    {
        MakeException(reason).ToMonadicError().Code.Should().Be(expectedCode);
    }

    [Fact]
    public void Preserves_original_message()
    {
        // The SDK appends diagnostic text to the message; verify it starts with our string.
        var error = MakeException(ServiceBusFailureReason.ServiceTimeout, "connection refused")
            .ToMonadicError();
        error.Message.Should().StartWith("connection refused");
    }

    [Fact]
    public void Includes_reason_in_metadata()
    {
        var error = MakeException(ServiceBusFailureReason.QuotaExceeded).ToMonadicError();
        error.Metadata["Reason"].Should().Be("QuotaExceeded");
    }

    [Fact]
    public void Includes_is_transient_in_metadata()
    {
        // ServiceBusException.IsTransient is set internally by the SDK based on Reason;
        // we verify the metadata key exists and is a bool.
        var error = MakeException(ServiceBusFailureReason.ServiceTimeout).ToMonadicError();
        error.Metadata.Should().ContainKey("IsTransient");
        error.Metadata["IsTransient"].Should().BeOfType<bool>();
    }
}
