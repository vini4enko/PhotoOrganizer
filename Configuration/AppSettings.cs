using System;
using System.Collections.Generic;

namespace ExifApp.Configuration;

public class AppSettings
{
    public string[] AllowedTags { get; set; } = Array.Empty<string>();
    public List<ExternalEditor> ExternalEditors { get; set; } = new();
    public ImageConversionSettings ImageConversion { get; set; } = new();
}

public class ExternalEditor
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;

    public override string ToString() => Name;
}

public class ImageConversionSettings
{
    /// <summary>Качество JPEG (1–100).</summary>
    public int JpegQuality { get; set; } = 95;

    /// <summary>Цвет фона для замены прозрачности: "White", "Black" или "#RRGGBB".</summary>
    public string BackgroundColor { get; set; } = "White";

    /// <summary>Суффикс, добавляемый к имени файла перед .jpg.</summary>
    public string OutputSuffix { get; set; } = "_converted";

    /// <summary>Перезаписывать существующий .jpg или создавать с номером.</summary>
    public bool OverwriteExisting { get; set; }

    /// <summary>Переносить EXIF-профиль из исходного PNG.</summary>
    public bool PreserveExif { get; set; } = true;
}