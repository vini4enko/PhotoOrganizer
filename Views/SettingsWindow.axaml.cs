using Avalonia.Controls;
using ExifApp.Services;
using ExifApp.ViewModels;

namespace ExifApp.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(AppSettingsService settingsService)
    {
        InitializeComponent();

        var vm = new SettingsViewModel(settingsService);
        vm.CloseRequested += () => Close();
        DataContext = vm;
    }

    // Параметрless-конструктор для дизайнера XAML
    public SettingsWindow() : this(new AppSettingsService()) { }
}