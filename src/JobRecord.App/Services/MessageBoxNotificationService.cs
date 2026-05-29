using System.Windows;

namespace JobRecord.App.Services;

public sealed class MessageBoxNotificationService : IUserNotificationService
{
    public void ShowError(string title, string message)
        => System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
}
