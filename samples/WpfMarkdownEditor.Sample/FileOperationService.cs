using System.Diagnostics;
using System.IO;

namespace WpfMarkdownEditor.Sample;

public sealed record FileProperties(string Path, long SizeBytes, DateTime CreatedUtc, DateTime LastModifiedUtc);

public sealed class FileOperationService
{
    public void MoveFile(string sourcePath, string destinationPath, bool overwrite)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Source file was not found.", sourcePath);

        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(destinationDirectory))
            Directory.CreateDirectory(destinationDirectory);

        File.Move(sourcePath, destinationPath, overwrite);
    }

    public void DeleteFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("File was not found.", path);

        File.Delete(path);
    }

    public FileProperties GetProperties(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
            throw new FileNotFoundException("File was not found.", path);

        return new FileProperties(info.FullName, info.Length, info.CreationTimeUtc, info.LastWriteTimeUtc);
    }

    public void OpenFileLocation(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("File was not found.", path);

        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
        {
            UseShellExecute = true
        });
    }
}
