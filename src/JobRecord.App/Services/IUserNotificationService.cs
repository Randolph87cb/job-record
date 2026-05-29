namespace JobRecord.App.Services;

public interface IUserNotificationService
{
    void ShowError(string title, string message);
}
