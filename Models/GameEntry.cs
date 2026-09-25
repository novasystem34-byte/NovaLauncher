using System;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Windows;

namespace NovaLauncher.Models
{
    public class GameEntry : INotifyPropertyChanged
    {
        public string GameId { get; set; } = Guid.NewGuid().ToString("N");

        public string DisplayName { get; set; } = string.Empty;

        public string ExecutablePath { get; set; } = string.Empty;

        public string IconCachePath { get; set; } = string.Empty;

        public long TotalPlaytimeSeconds { get; set; }

        public DateTime? LastPlayedUtc { get; set; }

        public DateTime AddedOnUtc { get; set; } = DateTime.UtcNow;

        public bool IsFavorite { get; set; }

        public string LaunchArguments { get; set; } = string.Empty;

        [JsonIgnore]
        private bool _isRunning;

        [JsonIgnore]
        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                if (_isRunning != value)
                {
                    _isRunning = value;
                    OnPropertyChanged(nameof(IsRunning));
                    OnPropertyChanged(nameof(IsRunningVisibility));
                }
            }
        }

        [JsonIgnore]
        public Visibility IsRunningVisibility => IsRunning ? Visibility.Visible : Visibility.Collapsed;

        public string FavoriteGlyph => IsFavorite ? "★" : "☆";

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string DisplayPlaytime
        {
            get
            {
                var span = TimeSpan.FromSeconds(TotalPlaytimeSeconds);
                if (span.TotalHours >= 1)
                {
                    return $"{(int)span.TotalHours} ساعة و {span.Minutes} دقيقة";
                }
                if (span.TotalMinutes >= 1)
                {
                    return $"{span.Minutes} دقيقة";
                }
                return "أقل من دقيقة";
            }
        }

        public string DisplayLastPlayed => LastPlayedUtc.HasValue
            ? LastPlayedUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
            : "لم يتم التشغيل بعد";
    }
}
