using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ExifApp.Configuration;
using Microsoft.Extensions.Configuration;

namespace ExifApp.Services;

public class AppSettingsService
{
    private readonly IConfigurationRoot _configuration;
    private AppSettings _current;

    /// <summary>
    /// Полный путь к appsettings.json рядом с .exe.
    /// </summary>
    public string SettingsFilePath { get; }

    public event Action<AppSettings>? SettingsChanged;

    public AppSettingsService()
    {
        SettingsFilePath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        _configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile(SettingsFilePath, optional: false, reloadOnChange: true)
            .Build();

        _current = LoadSettings();

        _configuration.GetReloadToken().RegisterChangeCallback(_ =>
        {
            _current = LoadSettings();
            SettingsChanged?.Invoke(_current);
        }, null);
    }

    public AppSettings Current => _current;

    /// <summary>
    /// Читает содержимое appsettings.json как текст.
    /// </summary>
    public string ReadRawJson() => File.ReadAllText(SettingsFilePath);

    /// <summary>
    /// Сохраняет содержимое appsettings.json. Бросает исключение при невалидном JSON.
    /// </summary>
    public void SaveRawJson(string json)
    {
        // Валидация: пробуем распарсить до записи в файл
        using (JsonDocument.Parse(json)) { }

        File.WriteAllText(SettingsFilePath, json);

        // Форсируем перезагрузку (reloadOnChange тоже сработает, но не сразу)
        _configuration.Reload();
        _current = LoadSettings();
        SettingsChanged?.Invoke(_current);
    }

    private AppSettings LoadSettings()
    {
        var settings = new AppSettings();
        _configuration.GetSection("AppSettings").Bind(settings);

        if (settings.AllowedTags is null || settings.AllowedTags.Length == 0)
        {
            settings.AllowedTags = new[] { "Model" };
        }

        settings.AllowedTags = settings.AllowedTags
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .ToArray();

        return settings;
    }
}