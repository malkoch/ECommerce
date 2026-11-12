using Messaging.Exceptions;

namespace Notification.Api.Exceptions;

public sealed class NotificationDeliveryException(string message) : PermanentMessageException(message);
