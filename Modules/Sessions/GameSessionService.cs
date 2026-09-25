using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using NovaLauncher.Core;
using NovaLauncher.Models;

namespace NovaLauncher.Modules.Sessions
{
    /// <summary>
    /// Starts a game process and reliably detects when it closes, then raises
    /// events the UI uses to hide/restore the launcher window and to persist
    /// accurate playtime.
    /// </summary>
    public class GameSessionService
    {
        public event Action<GameEntry>? SessionStarted;
        public event Action<GameEntry, TimeSpan>? SessionEnded;
        public event Action<GameEntry, string>? SessionFailed;

        public bool IsSessionActive { get; private set; }

        public async Task LaunchGameAsync(GameEntry game)
        {
            if (IsSessionActive)
            {
                SessionFailed?.Invoke(game, "توجد لعبة أخرى قيد التشغيل بالفعل من خلال اللانشر.");
                return;
            }

            if (!File.Exists(game.ExecutablePath))
            {
                SessionFailed?.Invoke(game, "الملف التنفيذي للعبة لم يعد موجوداً في مساره الأصلي.");
                return;
            }

            Process? process;

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = game.ExecutablePath,
                    Arguments = game.LaunchArguments ?? string.Empty,
                    WorkingDirectory = Path.GetDirectoryName(game.ExecutablePath) ?? string.Empty,
                    UseShellExecute = true
                };

                process = Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                CrashShieldLogger.LogError("فشل تشغيل اللعبة: " + game.DisplayName, ex);
                SessionFailed?.Invoke(game, "تعذر تشغيل اللعبة:\n" + ex.Message);
                return;
            }

            if (process == null)
            {
                SessionFailed?.Invoke(game, "تعذر بدء عملية اللعبة.");
                return;
            }

            IsSessionActive = true;
            var sessionStartTime = DateTime.UtcNow;
            CrashShieldLogger.LogInfo($"بدأت جلسة تشغيل: {game.DisplayName} (PID {process.Id})");

            SessionStarted?.Invoke(game);

            string processName;
            try
            {
                processName = process.ProcessName;
            }
            catch
            {
                processName = Path.GetFileNameWithoutExtension(game.ExecutablePath);
            }

            await Task.Run(() => WaitForGameToClose(process, processName));

            var sessionDuration = DateTime.UtcNow - sessionStartTime;
            IsSessionActive = false;

            CrashShieldLogger.LogInfo(
                $"انتهت جلسة تشغيل: {game.DisplayName}, المدة: {sessionDuration.TotalMinutes:F1} دقيقة");

            SessionEnded?.Invoke(game, sessionDuration);
        }

        /// <summary>
        /// Waits for the launched process to exit. Many games run through a
        /// wrapper/loader that exits immediately while the real game process
        /// keeps running under the same or a related name, so after the initial
        /// handle exits, this also polls by process name until no matching
        /// process remains - this is what makes exit detection reliable for
        /// most real-world game executables, not just simple single-process ones.
        /// </summary>
        private void WaitForGameToClose(Process launchedProcess, string processName)
        {
            try
            {
                launchedProcess.WaitForExit();
            }
            catch (Exception ex)
            {
                CrashShieldLogger.LogWarning("تعذرت مراقبة عملية اللعبة مباشرة: " + ex.Message);
            }

            while (true)
            {
                bool stillRunning;

                try
                {
                    stillRunning = Process.GetProcessesByName(processName).Length > 0;
                }
                catch
                {
                    stillRunning = false;
                }

                if (!stillRunning)
                {
                    return;
                }

                Task.Delay(1500).Wait();
            }
        }
    }
}
