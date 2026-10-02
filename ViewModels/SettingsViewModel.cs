using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExifApp.Services;
using System;
using System.Net.Http.Json;
using System.Text.Json;

namespace ExifApp.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettingsService _settingsService;

    [ObservableProperty]
    private string _jsonContent = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>
    /// Событие: пользователь запросил закрытие окна (Сохранить или Отмена).
    /// </summary>
    public event Action? CloseRequested;

    public SettingsViewModel(AppSettingsService settingsService)
    {
        _settingsService = settingsService;
        JsonContent = settingsService.ReadRawJson();
    }

    [RelayCommand]
    private void Save()
    {
        ErrorMessage = string.Empty;

        try
        {
            _settingsService.SaveRawJson(JsonContent);
            CloseRequested?.Invoke();
        }
        catch (JsonException ex)
        {
            ErrorMessage = $"Ошибка JSON (строка {ex.LineNumber}, поз. {ex.BytePositionInLine}): {ex.Message}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ошибка сохранения: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Reload()
    {
        ErrorMessage = string.Empty;
        JsonContent = _settingsService.ReadRawJson();
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke();
    }
}