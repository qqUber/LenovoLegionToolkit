using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.Utils;

/// <summary>
/// Service for exporting and importing all application settings as a single backup file.
/// </summary>
public class SettingsBackupService
{
    private static readonly string[] SettingsFiles =
    [
        "settings.json",
        "balance_mode_settings.json",
        "god_mode_settings.json",
        "gpu_overclock_settings.json",
        "integrations_settings.json",
        "package_downloader_settings.json",
        "rgb_keyboard_settings.json",
        "spectrum_keyboard_settings.json",
        "sunrise_sunset_settings.json",
        "update_check_settings.json",
        "automations.json",
        "macros.json"
    ];

    /// <summary>
    /// Exports all settings to a ZIP file at the specified path.
    /// </summary>
    /// <param name="exportPath">The full path including filename for the export ZIP.</param>
    /// <returns>The number of settings files exported.</returns>
    public async Task<int> ExportAllSettingsAsync(string exportPath)
    {
        var appDataPath = Folders.AppData;
        var exportedCount = 0;

        // Delete existing file if it exists
        if (File.Exists(exportPath))
            File.Delete(exportPath);

        using (var archive = ZipFile.Open(exportPath, ZipArchiveMode.Create))
        {
            foreach (var settingsFile in SettingsFiles)
            {
                var fullPath = Path.Combine(appDataPath, settingsFile);
                if (!File.Exists(fullPath))
                    continue;

                try
                {
                    archive.CreateEntryFromFile(fullPath, settingsFile, CompressionLevel.Optimal);
                    exportedCount++;
                }
                catch (Exception ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Failed to export {settingsFile}", ex);
                }
            }

            // Also export any custom profile files
            var jsonFiles = Directory.GetFiles(appDataPath, "*.json")
                .Where(f => !SettingsFiles.Contains(Path.GetFileName(f)))
                .ToList();

            foreach (var jsonFile in jsonFiles)
            {
                try
                {
                    var fileName = Path.GetFileName(jsonFile);
                    archive.CreateEntryFromFile(jsonFile, fileName, CompressionLevel.Optimal);
                    exportedCount++;
                }
                catch
                {
                    // Ignore individual file errors
                }
            }
        }

        await Task.CompletedTask;
        return exportedCount;
    }

    /// <summary>
    /// Imports settings from a ZIP backup file.
    /// </summary>
    /// <param name="importPath">The path to the backup ZIP file.</param>
    /// <returns>The number of settings files imported.</returns>
    public async Task<int> ImportAllSettingsAsync(string importPath)
    {
        if (!File.Exists(importPath))
            throw new FileNotFoundException("Backup file not found.", importPath);

        var appDataPath = Folders.AppData;
        var importedCount = 0;

        // Create a backup of current settings before importing
        var backupPath = Path.Combine(appDataPath, $"pre_import_backup_{DateTime.UtcNow:yyyyMMddHHmmss}.zip");
        try
        {
            await ExportAllSettingsAsync(backupPath);
        }
        catch
        {
            // Continue even if backup fails
        }

        using (var archive = ZipFile.OpenRead(importPath))
        {
            foreach (var entry in archive.Entries)
            {
                if (!entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    var destPath = Path.Combine(appDataPath, entry.FullName);
                    entry.ExtractToFile(destPath, overwrite: true);
                    importedCount++;
                }
                catch (Exception ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Failed to import {entry.FullName}", ex);
                }
            }
        }

        await Task.CompletedTask;
        return importedCount;
    }

    /// <summary>
    /// Gets the list of settings files that exist in the app data folder.
    /// </summary>
    public string[] GetExistingSettingsFiles()
    {
        var appDataPath = Folders.AppData;
        return SettingsFiles
            .Where(f => File.Exists(Path.Combine(appDataPath, f)))
            .ToArray();
    }
}
