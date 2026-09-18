using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using WallSafeWinUI.Services;

namespace WallSafeWinUI.Controls
{
    public sealed partial class WallpaperCard : UserControl
    {
        private PostItem? _post;

        public PostItem? Post => _post;

        public event Action<PostItem>? PreviewRequested;

        // When true, double-tapping opens the shared preview dialog directly (used by
        // ItemsControl-hosted shelves that don't wire PreviewRequested themselves).
        public bool PreviewHook { get; set; }

        public WallpaperCard()
        {
            InitializeComponent();
            DataContextChanged += (_, _) => Bind(DataContext as PostItem);
        }

        private bool _isDiscretionActive;

        private void Bind(PostItem? post)
        {
            _post = post;
            if (post == null) return;

            CachedImage.SetSourceUrl(Thumb, post.PreviewUrl);
            CachedImage.SetSourceUrl(BlurredThumb, post.PreviewUrl);
            ResText.Text = post.ResolutionText;
            RatingText.Text = post.RatingDisplay;
            RatingBadge.Background = new SolidColorBrush(ParseColor(post.RatingBadgeBackground));
            FavIcon.Glyph = post.IsFavorite ? "\uEB52" : "\uEB51";

            _isDiscretionActive = Settings.Instance.DiscretionBlur && post.Rating != "s";
            ApplyDiscretionState(revealed: false, animate: false);
        }

        private void ApplyDiscretionState(bool revealed, bool animate = false)
        {
            if (!_isDiscretionActive)
            {
                Thumb.Opacity = 1.0;
                BlurredThumb.Opacity = 0.0;
                BlurVeil.Opacity = 0.0;
                return;
            }

            double targetThumbOpacity = revealed ? 1.0 : 0.0;
            double targetBlurOpacity = revealed ? 0.0 : 1.0;

            if (animate)
            {
                double duration = revealed ? 280 : 220;
                AnimateOpacity(Thumb, targetThumbOpacity, duration);
                AnimateOpacity(BlurredThumb, targetBlurOpacity, duration);
                AnimateOpacity(BlurVeil, targetBlurOpacity, duration);
            }
            else
            {
                Thumb.Opacity = targetThumbOpacity;
                BlurredThumb.Opacity = targetBlurOpacity;
                BlurVeil.Opacity = targetBlurOpacity;
            }
        }

        private static void AnimateOpacity(UIElement element, double targetOpacity, double durationMs)
        {
            var anim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = targetOpacity,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EnableDependentAnimation = true,
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase
                {
                    EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseInOut
                }
            };
            var sb = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(anim, element);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(anim, "Opacity");
            sb.Children.Add(anim);
            sb.Begin();
        }

        private static Windows.UI.Color ParseColor(string hex)
        {
            try
            {
                hex = hex.TrimStart('#');
                byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                return Windows.UI.Color.FromArgb(255, r, g, b);
            }
            catch { return Colors.Gray; }
        }

        private void RootCard_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            RaisePreview();
        }

        private void View_Click(object sender, RoutedEventArgs e)
        {
            RaisePreview();
        }

        private void RaisePreview()
        {
            if (_post == null) return;
            PreviewRequested?.Invoke(_post);
            if (PreviewHook && XamlRoot != null)
                _ = WallSafeWinUI.Views.PreviewDialog.ShowAsync(XamlRoot, _post);
        }

        private void RootCard_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            ActionBar.Opacity = 1.0;
            if (_isDiscretionActive)
                ApplyDiscretionState(revealed: true, animate: true);
        }

        private void RootCard_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            ActionBar.Opacity = 0.85;
            if (_isDiscretionActive)
                ApplyDiscretionState(revealed: false, animate: true);
        }

        private void Fav_Click(object sender, RoutedEventArgs e)
        {
            if (_post == null) return;
            bool nowFav = FavoritesManager.Instance.ToggleFavorite(_post);
            FavIcon.Glyph = nowFav ? "\uEB52" : "\uEB51";
        }

        private async void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (_post == null) return;
            try { await WallpaperManager.Instance.ApplyWallpaperAsync(_post, Settings.Instance.TargetMonitor); }
            catch { }
        }

        private async void Download_Click(object sender, RoutedEventArgs e)
        {
            if (_post == null) return;
            DlProgress.Visibility = Visibility.Visible;
            var progress = new Progress<double>(p => DlProgress.Value = p);
            try
            {
                await DownloadsManager.Instance.DownloadPostAsync(_post, progress);
            }
            catch { }
            finally { DlProgress.Visibility = Visibility.Collapsed; }
        }

        private async void Source_Click(object sender, RoutedEventArgs e)
        {
            if (_post == null || string.IsNullOrEmpty(_post.SourceUrl)) return;
            try { await Windows.System.Launcher.LaunchUriAsync(new Uri(_post.SourceUrl)); }
            catch { }
        }
    }
}
