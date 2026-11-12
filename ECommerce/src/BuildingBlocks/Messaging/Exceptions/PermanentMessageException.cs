namespace Messaging.Exceptions;

/// <summary>Tekrar denemenin anlamsız olduğu hata (bozuk mesaj, sözleşme ihlali, tükenmiş sağlayıcı retry'ı). Mesaj doğrudan DLQ'ya gider.</summary>
public class PermanentMessageException(string message, Exception? inner = null) : Exception(message, inner);
