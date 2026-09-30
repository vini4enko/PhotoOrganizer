using System;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ExifApp.Models;

/// <summary>
/// Запись о фотографии в каталоге: путь, имя, дата съёмки и (лениво) миниатюра.
/// </summary>
public partial class PhotoEntry : ObservableObject
{
    public string FilePath { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public DateTime DateTaken { get; init; }

    /// <summary>
    /// Миниатюра фотографии. Загружается асинхронно после сканирования каталога.
    /// </summary>
    [ObservableProperty]
    private Bitmap? _thumbnail;

    public override string ToString() => FileName;
}