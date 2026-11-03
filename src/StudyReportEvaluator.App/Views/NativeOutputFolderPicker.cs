using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace StudyReportEvaluator.App.Views;

public interface IOutputFolderPicker
{
    Task<string?> PickAsync(TopLevel topLevel);
}

/// <summary>Uses the owning window's native storage boundary; never creates a directory.</summary>
public sealed class NativeOutputFolderPicker : IOutputFolderPicker
{
    public async Task<string?> PickAsync(TopLevel topLevel)
    {
        ArgumentNullException.ThrowIfNull(topLevel);
        if (!topLevel.StorageProvider.CanPickFolder)
        {
            throw new NotSupportedException("この環境ではフォルダーピッカーを開けません。");
        }

        IReadOnlyList<IStorageFolder> folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = "次回の出力フォルダーを選択", AllowMultiple = false });
        if (folders.Count == 0)
        {
            return null;
        }

        using IStorageFolder folder = folders[0];
        string? path = folder.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            throw new InvalidOperationException("選択したフォルダーのローカル絶対パスを取得できません。");
        }

        return Path.GetFullPath(path);
    }
}
