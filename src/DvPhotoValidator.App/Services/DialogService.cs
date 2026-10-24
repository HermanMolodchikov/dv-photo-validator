using System.Windows;
using Microsoft.Win32;

namespace DvPhotoValidator.App.Services;

public interface IDialogService
{
    string? PickImageFile();

    string? PickSavePath(string? initialDirectory, string fileName);

    void ShowError(string message);

    void CopyToClipboard(string text);
}

public sealed class DialogService : IDialogService
{
    public string? PickImageFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Выберите фотографию",
            Filter = "Изображения|*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.tif;*.tiff;*.heic;*.heif|Все файлы|*.*",
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickSavePath(string? initialDirectory, string fileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Сохранить фотографию для DV",
            Filter = "JPEG|*.jpg",
            DefaultExt = ".jpg",
            AddExtension = true,
            FileName = fileName,
            InitialDirectory = initialDirectory ?? "",
            OverwritePrompt = true,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public void ShowError(string message) =>
        MessageBox.Show(message, "DV Photo Validator", MessageBoxButton.OK, MessageBoxImage.Warning);

    public void CopyToClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            ShowError("Не удалось скопировать отчёт: буфер обмена занят другим приложением.");
        }
    }
}
