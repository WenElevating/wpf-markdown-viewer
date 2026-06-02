using System.IO;
using System.Text.Json;
using System.Threading;

namespace WpfMarkdownEditor.Sample;

public sealed record RecentFileEntry(string Path, DateTime OpenedAt);

public sealed class RecentFilesService
{
    private const string FileName = "recent-files.json";
    private const int MaxEntries = 20;
    private static readonly TimeSpan MutexTimeout = TimeSpan.FromSeconds(2);

    private readonly string _settingsDirectory;
    private readonly string _mutexName;

    public RecentFilesService(string settingsDirectory, string mutexName = "WpfMarkdownEditor.Sample.RecentFiles")
    {
        _settingsDirectory = settingsDirectory;
        _mutexName = $@"Local\{mutexName}";
    }

    public IReadOnlyList<RecentFileEntry> LoadFiles(bool removeMissingFiles = false)
    {
        return TryWithMutex(() =>
        {
            var entries = LoadRaw();
            if (removeMissingFiles)
            {
                entries = entries.Where(entry => File.Exists(entry.Path)).ToList();
                SaveRaw(entries);
            }

            return entries;
        }) ?? [];
    }

    public async Task<IReadOnlyList<RecentFileEntry>> LoadFilesAsync(
        bool removeMissingFiles = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var files = await Task.Run(() => LoadFiles(removeMissingFiles), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return files;
    }

    public void AddOrRefreshFile(string path)
    {
        if (!File.Exists(path))
            return;

        TryWithMutex(() =>
        {
            var fullPath = System.IO.Path.GetFullPath(path);
            var entries = LoadRaw()
                .Where(entry => !string.Equals(entry.Path, fullPath, StringComparison.OrdinalIgnoreCase))
                .ToList();

            entries.Insert(0, new RecentFileEntry(fullPath, DateTime.UtcNow));
            SaveRaw(entries.Take(MaxEntries).ToList());
        });
    }

    public void RemoveFile(string path)
    {
        TryWithMutex(() =>
        {
            var fullPath = System.IO.Path.GetFullPath(path);
            var entries = LoadRaw()
                .Where(entry => !string.Equals(entry.Path, fullPath, StringComparison.OrdinalIgnoreCase))
                .ToList();
            SaveRaw(entries);
        });
    }

    public void ClearFiles()
    {
        TryWithMutex(() => SaveRaw([]));
    }

    private T? TryWithMutex<T>(Func<T> action)
    {
        using var mutex = new Mutex(false, _mutexName);
        var hasHandle = false;

        try
        {
            hasHandle = mutex.WaitOne(MutexTimeout);
            if (!hasHandle)
                return default;

            return action();
        }
        catch (AbandonedMutexException)
        {
            hasHandle = true;
            return action();
        }
        catch (IOException)
        {
            return default;
        }
        catch (UnauthorizedAccessException)
        {
            return default;
        }
        finally
        {
            if (hasHandle)
                mutex.ReleaseMutex();
        }
    }

    private bool TryWithMutex(Action action) => TryWithMutex(() =>
    {
        action();
        return true;
    }) == true;

    private List<RecentFileEntry> LoadRaw()
    {
        var path = GetSettingsPath();
        if (!File.Exists(path))
            return [];

        try
        {
            var json = File.ReadAllText(path);
            var model = JsonSerializer.Deserialize<RecentFilesModel>(json);
            return model?.Files?
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Path))
                .OrderByDescending(entry => entry.OpenedAt)
                .Take(MaxEntries)
                .ToList() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }

    private void SaveRaw(IReadOnlyList<RecentFileEntry> entries)
    {
        Directory.CreateDirectory(_settingsDirectory);
        var json = JsonSerializer.Serialize(
            new RecentFilesModel(entries),
            new JsonSerializerOptions { WriteIndented = true });
        var path = GetSettingsPath();
        var tempPath = System.IO.Path.Combine(_settingsDirectory, $"{FileName}.{Guid.NewGuid():N}.tmp");

        RetryIo(() =>
        {
            File.WriteAllText(tempPath, json);
            if (File.Exists(path))
                File.Replace(tempPath, path, null);
            else
                File.Move(tempPath, path);
        });
    }

    private static void RetryIo(Action action)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (IOException) when (attempt < 3)
            {
                Thread.Sleep(25 * attempt);
            }
            catch (UnauthorizedAccessException) when (attempt < 3)
            {
                Thread.Sleep(25 * attempt);
            }
        }
    }

    private string GetSettingsPath() => System.IO.Path.Combine(_settingsDirectory, FileName);

    private sealed record RecentFilesModel(IReadOnlyList<RecentFileEntry>? Files);
}
