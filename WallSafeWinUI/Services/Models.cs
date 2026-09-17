using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WallSafeWinUI.Services
{
    public class PostItem : INotifyPropertyChanged
    {
        private bool _isFavorite;
        private bool _isDownloaded;
        private bool _isDiscreet;
        private string? _localPath;

        public int Id { get; set; }
        public string Source { get; set; } = "yande";
        public string PreviewUrl { get; set; } = "";
        public string SampleUrl { get; set; } = "";
        public string FileUrl { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
        public string Rating { get; set; } = "s"; // s, q, e
        public int Score { get; set; }
        public string Tags { get; set; } = "";
        public string Author { get; set; } = "";
        public string SourceUrl { get; set; } = "";
        public DateTime? CreatedAt { get; set; }
        public DateTime? AppliedAt { get; set; }

        public string Collection { get; set; } = "";

        public bool IsFavorite
        {
            get => _isFavorite;
            set { if (_isFavorite != value) { _isFavorite = value; OnPropertyChanged(); OnPropertyChanged(nameof(FavoriteGlyph)); } }
        }

        public bool IsDownloaded
        {
            get => _isDownloaded;
            set { if (_isDownloaded != value) { _isDownloaded = value; OnPropertyChanged(); } }
        }

        public bool IsDiscreet
        {
            get => _isDiscreet;
            set { if (_isDiscreet != value) { _isDiscreet = value; OnPropertyChanged(); } }
        }

        public string? LocalPath
        {
            get => _localPath;
            set { if (_localPath != value) { _localPath = value; OnPropertyChanged(); } }
        }

        public string ResolutionText => Width > 0 && Height > 0 ? $"{Width} × {Height}" : "Original";

        public string AspectRatioText
        {
            get
            {
                if (Width <= 0 || Height <= 0) return "";
                double ratio = (double)Width / Height;
                if (ratio >= 2.2) return "Ultrawide";
                if (ratio >= 1.6) return "16:9";
                if (ratio >= 1.25) return "4:3";
                if (ratio >= 0.9) return "1:1";
                return "Portrait";
            }
        }

        public string RatingDisplay => Rating switch
        {
            "s" => "SFW",
            "q" => "16+",
            "e" => "NSFW",
            _ => Rating.ToUpperInvariant()
        };

        public string RatingBadgeBackground => Rating switch
        {
            "s" => "#10B981",
            "q" => "#F59E0B",
            "e" => "#EF4444",
            _ => "#6B7280"
        };

        // Segoe Fluent glyph for favorite state (used by card binding).
        public string FavoriteGlyph => IsFavorite ? "\uEB52" : "\uEB51"; // filled / outline heart

        public string BestImageUrl => !string.IsNullOrEmpty(SampleUrl) ? SampleUrl : (!string.IsNullOrEmpty(FileUrl) ? FileUrl : PreviewUrl);
        public string FullDownloadUrl => !string.IsNullOrEmpty(FileUrl) ? FileUrl : BestImageUrl;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class TagSuggestion
    {
        public string Name { get; set; } = "";
        public string? Title { get; set; }
        public int Count { get; set; }
        public int Type { get; set; }

        public string DisplayText => !string.IsNullOrEmpty(Title) ? $"{Name} • {Title}" : Name;
        public string CountDisplay => Count > 0 ? $"{Count:N0}" : "Recommended";

        public string CategoryName => Type switch
        {
            1 => "Artist",
            3 => "Series",
            4 => "Character",
            5 => "Circle",
            6 => "Faults",
            _ => "Tag"
        };

        public string CategoryColor => Type switch
        {
            1 => "#F43F5E",
            3 => "#A855F7",
            4 => "#10B981",
            5 => "#06B6D4",
            _ => "#94A3B8"
        };
    }

    public class DisplayMonitorInfo
    {
        public int Index { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsPrimary { get; set; }

        public string DisplayName => IsPrimary
            ? $"{DeviceName} ({Width}×{Height}) • Primary"
            : $"{DeviceName} ({Width}×{Height})";
    }

    public class CustomSourceConfig
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
        public bool IsSfw { get; set; } = true;
        public bool Enabled { get; set; } = true;
    }

    /// <summary>View-model for a favorites collection "folder" card.</summary>
    public class FolderCardVM
    {
        public string Name { get; set; } = "";       // "" => All favorites
        public string DisplayName { get; set; } = "";
        public string Cover { get; set; } = "";
        public int Count { get; set; }
        public string CountText => Count == 1 ? "1 item" : $"{Count} items";
    }

    public class SeriesCategoryItem : INotifyPropertyChanged
    {
        private string _previewImageUrl = string.Empty;
        private int _postCount = 0;

        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public string Type { get; set; } = "Anime";
        public bool IsCustom { get; set; } = false;

        public int PostCount
        {
            get => _postCount;
            set
            {
                if (_postCount != value)
                {
                    _postCount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PostCountDisplay));
                    OnPropertyChanged(nameof(HasPostCount));
                }
            }
        }

        public string PostCountDisplay => PostCount >= 1000
            ? $"{PostCount / 1000.0:0.#}k"
            : (PostCount > 0 ? $"{PostCount}" : "");

        public bool HasPostCount => PostCount > 0;

        public string PreviewImageUrl
        {
            get => _previewImageUrl;
            set { if (_previewImageUrl != value) { _previewImageUrl = value; OnPropertyChanged(); } }
        }

        public string TypeBadgeColor => Type switch
        {
            "Game" => "#06B6D4",
            "Custom" => "#EC4899",
            _ => "#A855F7"
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
