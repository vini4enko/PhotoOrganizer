using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace ExifApp.Services;

public interface IDialogService
{
    Task ShowErrorAsync(string title, string message);
    Task ShowInfoAsync(string title, string message);
    Task<bool> ShowConfirmAsync(string title, string message);
}

public class DialogService : IDialogService
{
    public Task ShowErrorAsync(string title, string message) =>
        ShowAsync(title, message, Icon.Error);

    public Task ShowInfoAsync(string title, string message) =>
        ShowAsync(title, message, Icon.Info);

    public async Task<bool> ShowConfirmAsync(string title, string message)
    {
        var owner = GetMainWindow();
        if (owner is null) return false;

        var box = MessageBoxManager.GetMessageBoxStandard(
            title, message, ButtonEnum.YesNo, Icon.Warning);

        var result = await box.ShowWindowDialogAsync(owner);
        return result == ButtonResult.Yes;
    }

    private static async Task ShowAsync(string title, string message, Icon icon)
    {
        var owner = GetMainWindow();
        if (owner is null) return;

        var box = MessageBoxManager.GetMessageBoxStandard(
            title, message, ButtonEnum.Ok, icon);

        await box.ShowWindowDialogAsync(owner);
    }

    private static Window? GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is
            IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }
        return null;
    }
}