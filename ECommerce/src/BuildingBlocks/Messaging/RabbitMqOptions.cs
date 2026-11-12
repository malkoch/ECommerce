namespace Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string User { get; set; } = "guest";
    public string Password { get; set; } = "guest";

    /// <summary>Consumer'ın bir mesajı DLQ'ya göndermeden önceki toplam deneme sayısı.</summary>
    public int ConsumerMaxAttempts { get; set; } = 3;
    public int ConsumerRetryBaseDelayMs { get; set; } = 500;
}
