using System;
using System.IO;
using System.Linq;
using ExifApp.Configuration;
using Microsoft.Extensions.Configuration;

namespace ExifApp.Services;

public class AppSettingsService
{
    private readonly IConfigurationRoot _configuration;
    private AppSettings _current;

    /// <summary>
    /// Событие возникает при перезагрузке appsettings.json.
    /// </summary>
    public event Action<AppSettings>? SettingsChanged;

    public AppSettingsService()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        _configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile(path, optional: false, reloadOnChange: true)
            .Build();

        _current = LoadSettings();

        // Подписка на изменения файла
        _configuration.GetReloadToken().RegisterChangeCallback(_ =>
        {
            _current = LoadSettings();
            SettingsChanged?.Invoke(_current);
        }, null);
    }

    public AppSettings Current => _current;

    private AppSettings LoadSettings()
    {
        var settings = new AppSettings();
        _configuration.GetSection("AppSettings").Bind(settings);

        // Защита от пустого/отсутствующего списка
        if (settings.AllowedTags is null || settings.AllowedTags.Length == 0)
        {
            settings.AllowedTags = new[] { "Model" };
        }

        // Убираем пустые строки и пробелы по краям
        settings.AllowedTags = settings.AllowedTags
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .ToArray();

        return settings;
    }
}