using System;
using System.Collections.Generic;

namespace ExifApp.Configuration;

public class AppSettings
{
    public string[] AllowedTags { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Список внешних программ для просмотра/редактирования фото.
    /// </summary>
    public List<ExternalEditor> ExternalEditors { get; set; } = new();
}

public class ExternalEditor
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Шаблон аргументов. Плейсхолдер {file} заменяется на путь к файлу.
    /// </summary>
    public string Arguments { get; set; } = string.Empty;

    public override string ToString() => Name;
}