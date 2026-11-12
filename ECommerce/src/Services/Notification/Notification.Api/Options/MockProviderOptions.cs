namespace Notification.Api.Options;

public sealed class MockProviderOptions
{
    public const string SectionName = "Notification:Mock";

    /// <summary>Hata senaryosunu göstermek için ilgili kanalı hata verecek şekilde işaretler.</summary>
    public bool FailEmail { get; set; }
    public bool FailSms { get; set; }
}
