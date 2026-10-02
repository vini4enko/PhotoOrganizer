using System;
using System.Globalization;
using System.IO;
using ExifApp.Configuration;
using ImageMagick;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System.Threading.Tasks;

namespace ExifApp.Services;

public class ImageConversionService
{
    /// <summary>
    /// Конвертирует PNG в JPEG. Оригинал остаётся нетронутым.
    /// Возвращает путь к созданному файлу.
    /// </summary>
    public string ConvertPngToJpeg(string sourcePngPath, ImageConversionSettings settings)
    {
        if (!File.Exists(sourcePngPath))
            throw new FileNotFoundException("Исходный файл не найден", sourcePngPath);

        var ext = Path.GetExtension(sourcePngPath);
        if (!ext.Equals(".png", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Конвертация поддерживается только для PNG.");

        // Путь к целевому файлу: <имя><suffix>.jpg
        var dir = Path.GetDirectoryName(sourcePngPath) ?? ".";
        var baseName = Path.GetFileNameWithoutExtension(sourcePngPath);
        var targetPath = Path.Combine(dir, baseName + settings.OutputSuffix + ".jpg");

        // Если файл существует и перезапись запрещена — добавляем номер
        if (File.Exists(targetPath) && !settings.OverwriteExisting)
            targetPath = BuildUniquePath(dir, baseName + settings.OutputSuffix, ".jpg");

        using var image = new MagickImage(sourcePngPath);

        // 1. Сохраняем EXIF-профиль (если он есть и требуется)
        ExifProfile? exifProfile = null;
        if (settings.PreserveExif)
            exifProfile = (ExifProfile?)image.GetExifProfile();

        // 2. Заменяем прозрачность на заданный цвет
        var bgColor = ParseColor(settings.BackgroundColor);
        image.ColorAlpha(bgColor);

        // 3. Формат и качество
        image.Format = MagickFormat.Jpeg;
        image.Quality = (uint)Math.Clamp(settings.JpegQuality, 1, 100);

        // 4. Возвращаем EXIF-профиль в новое изображение
        if (exifProfile != null)
            image.SetProfile(exifProfile);

        // 5. Записываем в новый файл (оригинал не трогаем)
        image.Write(targetPath);

        return targetPath;
    }

    private static IMagickColor<ushort> ParseColor(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return MagickColors.White;

        // Именованные цвета ("White", "Black", ...)
        var named = typeof(MagickColors)
            .GetProperty(value,
                System.Reflection.BindingFlags.IgnoreCase
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Static);

        if (named?.GetValue(null) is IMagickColor<ushort> c)
            return c;

        // Цвет в формате #RRGGBB
        if (value.StartsWith("#"))
        {
            try
            {
                return new MagickColor(value);
            }
            catch
            {
                // Игнорируем — ниже вернём белый
            }
        }

        return MagickColors.White;
    }
    private static string BuildUniquePath(string directory, string baseName, string extension)
    {
        for (int i = 1; i < 10000; i++)
        {
            var candidate = Path.Combine(directory,
                string.Format(CultureInfo.InvariantCulture, "{0}_{1}{2}", baseName, i, extension));
            if (!File.Exists(candidate))
                return candidate;
        }
        throw new IOException("Не удалось подобрать уникальное имя файла.");
    }
}