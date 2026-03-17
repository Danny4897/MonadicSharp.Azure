using Azure.Messaging.ServiceBus;
using MonadicSharp;

namespace MonadicSharp.Azure.Messaging;

/// <summary>
/// Maps <see cref="ServiceBusException"/> to MonadicSharp <see cref="Error"/> values.
/// </summary>
public static class ServiceBusExceptionMapping
{
    /// <summary>
    /// Converts a <see cref="ServiceBusException"/> to a structured <see cref="Error"/>.
    /// </summary>
    public static Error ToMonadicError(this ServiceBusException ex)
    {
        var (errorType, code) = ex.Reason switch
        {
            ServiceBusFailureReason.MessagingEntityNotFound     => (ErrorType.NotFound,   "SB_ENTITY_NOT_FOUND"),
            ServiceBusFailureReason.MessageNotFound             => (ErrorType.NotFound,   "SB_MESSAGE_NOT_FOUND"),
            ServiceBusFailureReason.MessagingEntityDisabled     => (ErrorType.Forbidden,  "SB_ENTITY_DISABLED"),
            ServiceBusFailureReason.QuotaExceeded               => (ErrorType.Failure,    "SB_QUOTA_EXCEEDED"),
            ServiceBusFailureReason.MessageSizeExceeded         => (ErrorType.Validation, "SB_MESSAGE_TOO_LARGE"),
            ServiceBusFailureReason.ServiceCommunicationProblem => (ErrorType.Exception,  "SB_COMMUNICATION_ERROR"),
            ServiceBusFailureReason.ServiceTimeout              => (ErrorType.Exception,  "SB_TIMEOUT"),
            _                                                   => (ErrorType.Failure,    "SB_ERROR")
        };

        return Error.Create(ex.Message, code, errorType)
            .WithMetadata("Reason", ex.Reason.ToString())
            .WithMetadata("IsTransient", ex.IsTransient)
            .WithMetadata("EntityPath", ex.EntityPath ?? "");
    }
}
