namespace FGate.Services
{
    public enum AlertLevel
    {
        Info,
        Warning,
        Critical
    }

    public interface INotificationService
    {
        Task TriggerAlertAsync(AlertLevel level, string title, object? details = null);
    }
}