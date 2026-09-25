using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.Json;
using NovaLauncher.Core;
using NovaLauncher.Models;

namespace NovaLauncher.Modules.Library
{
    /// <summary>
    /// Owns the persisted list of added games. Handles extracting each game's
    /// real Windows icon from its .exe (Icon.ExtractAssociatedIcon), caching it
    /// as a PNG on disk so the UI never needs to touch the original executable
    /// again, and saving/loading the whole library as local JSON.
    /// </summary>
    public class GameLibraryService
    {
        private List<GameEntry> _games = new();

        public IReadOnlyList<GameEntry> Games => _games.AsReadOnly();

        public GameLibraryService()
        {
            LoadLibrary();
        }

        public GameEntry AddGame(string executablePath)
        {
            if (!File.Exists(executablePath))
            {
                throw new FileNotFoundException("الملف التنفيذي غير موجود.", executablePath);
            }

            if (!string.Equals(Path.GetExtension(executablePath), ".exe", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("الرجاء اختيار ملف تشغيل (.exe) صالح.");
            }

            if (_games.Any(g => string.Equals(g.ExecutablePath, executablePath, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("هذه اللعبة مضافة بالفعل إلى المكتبة.");
            }

            var entry = new GameEntry
            {
                DisplayName = Path.GetFileNameWithoutExtension(executablePath),
                ExecutablePath = executablePath,
                AddedOnUtc = DateTime.UtcNow
            };

            entry.IconCachePath = ExtractAndCacheIcon(executablePath, entry.GameId);

            _games.Add(entry);
            SaveLibrary();
            return entry;
        }

        public void RemoveGame(GameEntry entry)
        {
            _games.RemoveAll(g => g.GameId == entry.GameId);

            try
            {
                if (!string.IsNullOrEmpty(entry.IconCachePath) && File.Exists(entry.IconCachePath))
                {
                    File.Delete(entry.IconCachePath);
                }
            }
            catch (Exception ex)
            {
                CrashShieldLogger.LogWarning("تعذر حذف أيقونة اللعبة المخزنة مؤقتاً: " + ex.Message);
            }

            SaveLibrary();
        }

        public void RenameGame(GameEntry entry, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                return;
            }

            entry.DisplayName = newName.Trim();
            SaveLibrary();
        }

        public void ToggleFavorite(GameEntry entry)
        {
            entry.IsFavorite = !entry.IsFavorite;
            SaveLibrary();
        }

        public void SetLaunchArguments(GameEntry entry, string arguments)
        {
            entry.LaunchArguments = arguments?.Trim() ?? string.Empty;
            SaveLibrary();
        }

        public void AddPlaytime(GameEntry entry, TimeSpan sessionDuration)
        {
            entry.TotalPlaytimeSeconds += (long)sessionDuration.TotalSeconds;
            entry.LastPlayedUtc = DateTime.UtcNow;
            SaveLibrary();
        }

        private string ExtractAndCacheIcon(string executablePath, string gameId)
        {
            try
            {
                using var extractedIcon = Icon.ExtractAssociatedIcon(executablePath);
                if (extractedIcon == null)
                {
                    return string.Empty;
                }

                string iconFilePath = Path.Combine(AppPaths.IconsFolder, gameId + ".png");

                using var bitmap = extractedIcon.ToBitmap();
                bitmap.Save(iconFilePath, ImageFormat.Png);

                return iconFilePath;
            }
            catch (Exception ex)
            {
                CrashShieldLogger.LogWarning("تعذر استخراج أيقونة اللعبة: " + ex.Message);
                return string.Empty;
            }
        }

        private void SaveLibrary()
        {
            try
            {
                string json = JsonSerializer.Serialize(_games, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(AppPaths.LibraryFile, json);
            }
            catch (Exception ex)
            {
                CrashShieldLogger.LogError("تعذر حفظ مكتبة الألعاب.", ex);
            }
        }

        private void LoadLibrary()
        {
            try
            {
                if (!File.Exists(AppPaths.LibraryFile))
                {
                    _games = new List<GameEntry>();
                    return;
                }

                string json = File.ReadAllText(AppPaths.LibraryFile);
                _games = JsonSerializer.Deserialize<List<GameEntry>>(json) ?? new List<GameEntry>();

                _games.RemoveAll(g => !File.Exists(g.ExecutablePath));
            }
            catch (Exception ex)
            {
                CrashShieldLogger.LogError("تعذر تحميل مكتبة الألعاب، سيتم البدء بمكتبة فارغة.", ex);
                _games = new List<GameEntry>();
            }
        }
    }
}
