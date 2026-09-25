using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NovaLauncher.Core;
using NovaLauncher.Models;
using NovaLauncher.Modules.Library;
using NovaLauncher.Modules.Sessions;
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace NovaLauncher
{
    public partial class MainWindow : Window
    {
        private readonly GameLibraryService _libraryService = new();
        private readonly GameSessionService _sessionService = new();
        private WinForms.NotifyIcon? _trayIcon;
        private bool _isExitRequested;

        public MainWindow()
        {
            InitializeComponent();

            _sessionService.SessionStarted += OnSessionStarted;
            _sessionService.SessionEnded += OnSessionEnded;
            _sessionService.SessionFailed += OnSessionFailed;

            InitializeTrayIcon();
            RefreshGamesList();
        }

        // ============================================================
        // LIBRARY LIST
        // ============================================================

        private void RefreshGamesList()
        {
            if (GamesItemsControl == null || EmptyLibraryHint == null)
            {
                return;
            }

            IEnumerable<GameEntry> games = _libraryService.Games;

            string searchTerm = SearchTextBox?.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(searchTerm))
            {
                games = games.Where(g => g.DisplayName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
            }

            games = ApplySorting(games);

            var gamesList = games.ToList();
            GamesItemsControl.ItemsSource = null;
            GamesItemsControl.ItemsSource = gamesList;
            EmptyLibraryHint.Visibility = gamesList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            UpdateLibraryStats();
        }

        private IEnumerable<GameEntry> ApplySorting(IEnumerable<GameEntry> games)
        {
            IOrderedEnumerable<GameEntry> orderedGames = games.OrderByDescending(g => g.IsFavorite);

            int sortIndex = SortComboBox?.SelectedIndex ?? 0;

            return sortIndex switch
            {
                1 => orderedGames.ThenByDescending(g => g.TotalPlaytimeSeconds),
                2 => orderedGames.ThenBy(g => g.DisplayName, StringComparer.OrdinalIgnoreCase),
                3 => orderedGames.ThenByDescending(g => g.AddedOnUtc),
                _ => orderedGames.ThenByDescending(g => g.LastPlayedUtc ?? DateTime.MinValue)
            };
        }

        private void UpdateLibraryStats()
        {
            if (LibraryStatsText == null)
            {
                return;
            }

            int totalGames = _libraryService.Games.Count;
            long totalSeconds = _libraryService.Games.Sum(g => g.TotalPlaytimeSeconds);
            var totalSpan = TimeSpan.FromSeconds(totalSeconds);

            LibraryStatsText.Text = totalGames == 0
                ? string.Empty
                : $"📚 {totalGames} لعبة  •  ⏱ {(int)totalSpan.TotalHours} ساعة و {totalSpan.Minutes} دقيقة إجمالي وقت اللعب";
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            SearchPlaceholderText.Visibility = string.IsNullOrEmpty(SearchTextBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;

            RefreshGamesList();
        }

        private void OnSortChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshGamesList();
        }

        private void OnToggleFavoriteClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not GameEntry game)
            {
                return;
            }

            _libraryService.ToggleFavorite(game);
            RefreshGamesList();
        }

        private void OnEditLaunchArgumentsClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not GameEntry game)
            {
                return;
            }

            var inputDialog = new InputDialogWindow(
                $"خيارات تشغيل إضافية للعبة \"{game.DisplayName}\" (Launch Arguments) - اتركها فارغة إن لم تكن هناك حاجة:",
                game.LaunchArguments)
            {
                Owner = this
            };

            if (inputDialog.ShowDialog() == true)
            {
                _libraryService.SetLaunchArguments(game, inputDialog.ResultText);
            }
        }

        private void OnAddGameClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "اختر ملف تشغيل اللعبة (.exe)",
                Filter = "ملفات تنفيذية (*.exe)|*.exe"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                _libraryService.AddGame(dialog.FileName);
                RefreshGamesList();
                StatusBarText.Text = "تمت إضافة اللعبة بنجاح.";
            }
            catch (Exception ex)
            {
                CrashShieldLogger.LogError("فشل إضافة لعبة جديدة.", ex);
                MessageBox.Show(this, ex.Message, "Nova Launcher", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnRenameGameClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not GameEntry selectedGame)
            {
                return;
            }

            var inputDialog = new InputDialogWindow("أدخل الاسم الجديد للعبة:", selectedGame.DisplayName)
            {
                Owner = this
            };

            if (inputDialog.ShowDialog() == true)
            {
                _libraryService.RenameGame(selectedGame, inputDialog.ResultText);
                RefreshGamesList();
            }
        }

        private void OnRemoveGameClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not GameEntry selectedGame)
            {
                return;
            }

            if (selectedGame.IsRunning)
            {
                MessageBox.Show(this, "لا يمكن إزالة لعبة قيد التشغيل حالياً.", "Nova Launcher",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirmResult = MessageBox.Show(this,
                $"سيتم إزالة \"{selectedGame.DisplayName}\" من المكتبة (لن يتم حذف ملفات اللعبة نفسها). هل تريد المتابعة؟",
                "تأكيد الإزالة", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirmResult != MessageBoxResult.Yes)
            {
                return;
            }

            _libraryService.RemoveGame(selectedGame);
            RefreshGamesList();
        }

        private void OnOpenFolderClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not GameEntry selectedGame)
            {
                return;
            }

            try
            {
                string? folder = Path.GetDirectoryName(selectedGame.ExecutablePath);
                if (folder != null && Directory.Exists(folder))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{folder}\"",
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                CrashShieldLogger.LogWarning("تعذر فتح مجلد اللعبة: " + ex.Message);
            }
        }

        // ============================================================
        // LAUNCH FLOW
        // ============================================================

        private async void OnPlayGameClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not GameEntry gameToLaunch)
            {
                return;
            }

            if (_sessionService.IsSessionActive)
            {
                MessageBox.Show(this, "توجد لعبة أخرى قيد التشغيل بالفعل. أغلقها أولاً قبل تشغيل لعبة جديدة.",
                    "Nova Launcher", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            await _sessionService.LaunchGameAsync(gameToLaunch);
        }

        private void OnSessionStarted(GameEntry game)
        {
            Dispatcher.Invoke(() =>
            {
                game.IsRunning = true;
                StatusBarText.Text = $"جاري تشغيل: {game.DisplayName} ...";
                HideToTray();
            });
        }

        private void OnSessionEnded(GameEntry game, TimeSpan sessionDuration)
        {
            Dispatcher.Invoke(() =>
            {
                game.IsRunning = false;
                _libraryService.AddPlaytime(game, sessionDuration);
                RefreshGamesList();
                StatusBarText.Text =
                    $"انتهت جلسة {game.DisplayName} - مدة الجلسة: {(int)sessionDuration.TotalMinutes} دقيقة.";
                RestoreFromTray();
            });
        }

        private void OnSessionFailed(GameEntry game, string errorMessage)
        {
            Dispatcher.Invoke(() =>
            {
                game.IsRunning = false;
                MessageBox.Show(this, errorMessage, "Nova Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        // ============================================================
        // SYSTEM TRAY
        // ============================================================

        private void InitializeTrayIcon()
        {
            try
            {
                var iconResourceUri = new Uri("pack://application:,,,/AppIcon.ico", UriKind.Absolute);
                Drawing.Icon trayIconImage;

                try
                {
                    var resourceStream = System.Windows.Application.GetResourceStream(iconResourceUri);
                    trayIconImage = resourceStream != null
                        ? new Drawing.Icon(resourceStream.Stream)
                        : Drawing.SystemIcons.Application;
                }
                catch
                {
                    trayIconImage = Drawing.SystemIcons.Application;
                }

                _trayIcon = new WinForms.NotifyIcon
                {
                    Icon = trayIconImage,
                    Visible = false,
                    Text = "Nova System Launcher"
                };

                var contextMenu = new WinForms.ContextMenuStrip();
                contextMenu.Items.Add("إظهار اللانشر", null, (_, _) => RestoreFromTray());
                contextMenu.Items.Add("إغلاق نهائي", null, (_, _) => ExitApplication());
                _trayIcon.ContextMenuStrip = contextMenu;

                _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
            }
            catch (Exception ex)
            {
                CrashShieldLogger.LogError("تعذر تهيئة أيقونة شريط النظام.", ex);
            }
        }

        private void HideToTray()
        {
            try
            {
                Hide();
                ShowInTaskbar = false;

                if (_trayIcon != null)
                {
                    _trayIcon.Visible = true;
                    _trayIcon.ShowBalloonTip(2000, "Nova System Launcher",
                        "اللانشر يعمل الآن في الخلفية لإفساح كامل موارد الجهاز للعبة.", WinForms.ToolTipIcon.Info);
                }
            }
            catch (Exception ex)
            {
                CrashShieldLogger.LogWarning("تعذر إخفاء النافذة للـ Tray: " + ex.Message);
            }
        }

        private void RestoreFromTray()
        {
            try
            {
                Show();
                ShowInTaskbar = true;
                WindowState = WindowState.Normal;
                Activate();

                if (_trayIcon != null)
                {
                    _trayIcon.Visible = false;
                }
            }
            catch (Exception ex)
            {
                CrashShieldLogger.LogWarning("تعذرت إعادة إظهار النافذة: " + ex.Message);
            }
        }

        private void OnWindowStateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized && !_sessionService.IsSessionActive)
            {
                // Regular manual minimize (not a game session) keeps normal taskbar behavior.
            }
        }

        private void ExitApplication()
        {
            _isExitRequested = true;

            try
            {
                if (_trayIcon != null)
                {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                }
            }
            catch
            {
                // Best-effort cleanup on exit.
            }

            System.Windows.Application.Current.Shutdown();
        }

        private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isExitRequested)
            {
                return;
            }

            if (_sessionService.IsSessionActive)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }

            ExitApplication();
        }
    }
}
