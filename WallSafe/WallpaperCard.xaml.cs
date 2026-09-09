using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaGeometry = System.Windows.Media.Geometry;

namespace WallSafe
{
    public partial class WallpaperCard : System.Windows.Controls.UserControl
    {
        public event EventHandler<PostItem>? WallpaperApplied;
        public event EventHandler<PostItem>? FavoriteToggled;
        public event EventHandler<PostItem>? DownloadCompleted;
        public event EventHandler<PostItem>? PreviewRequested;

        public PostItem? Item => DataContext as PostItem;

        private CancellationTokenSource _noticeCts = new();

        public WallpaperCard()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            Loaded += (_, _) =>
            {
                ThemeManager.ThemeChanged += OnThemeChanged;
                UpdateCardStates();
            };
            Unloaded += (_, _) =>
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
            };
        }

        private void OnThemeChanged(bool isDark)
        {
            Dispatcher.Invoke(UpdateCardStates);
        }

        private void CardRootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width > 0 && e.NewSize.Height > 0)
            {
                CardRootGrid.Clip = new System.Windows.Media.RectangleGeometry(
                    new Rect(0, 0, e.NewSize.Width, e.NewSize.Height), 8, 8);
            }
        }

        private void Card_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                if (Item != null)
                {
                    PreviewRequested?.Invoke(this, Item);
                }
                e.Handled = true;
            }
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateCardStates();
            // Reset placeholders on item change
            if (ErrorPlaceholder != null) ErrorPlaceholder.Visibility = Visibility.Collapsed;
            if (LoadingPlaceholder != null) LoadingPlaceholder.Visibility = Visibility.Visible;

            // Manually load BitmapImage so we can subscribe to events and show/hide placeholders
            if (Item != null && !string.IsNullOrEmpty(Item.PreviewUrl))
            {
                LoadThumbWithPlaceholder(Item.PreviewUrl);
            }
            else
            {
                ThumbImage.Source = null;
                if (LoadingPlaceholder != null) LoadingPlaceholder.Visibility = Visibility.Collapsed;
            }
        }

        private async void LoadThumbWithPlaceholder(string url)
        {
            try
            {
                // 1. Instant check in memory cache (0ms)
                var memBmp = ImageCacheService.Instance.GetFromMemoryCache(url);
                if (memBmp != null)
                {
                    ThumbImage.Source = memBmp;
                    if (LoadingPlaceholder != null) LoadingPlaceholder.Visibility = Visibility.Collapsed;
                    if (ErrorPlaceholder != null) ErrorPlaceholder.Visibility = Visibility.Collapsed;
                    return;
                }

                // 2. Fetch from fast content-addressable disk cache
                string filePath = await ImageCacheService.Instance.GetCachedImagePathAsync(url);
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    filePath = url;
                }

                // If DataContext changed while loading, cancel applying
                if (Item?.PreviewUrl != url) return;

                // If local cached file exists, decode off the UI thread and freeze for zero UI hitching
                if (File.Exists(filePath))
                {
                    var cachedBi = await Task.Run(() =>
                    {
                        var img = new System.Windows.Media.Imaging.BitmapImage();
                        img.BeginInit();
                        img.UriSource = new Uri(filePath, UriKind.Absolute);
                        img.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        img.DecodePixelWidth = 360; // Decode at crisp thumbnail resolution
                        img.EndInit();
                        img.Freeze(); // Allows cross-thread access and lightning fast GPU render
                        return img;
                    });

                    if (Item?.PreviewUrl != url) return;
                    ImageCacheService.Instance.AddToMemoryCache(url, cachedBi);
                    ThumbImage.Source = cachedBi;
                    if (LoadingPlaceholder != null) LoadingPlaceholder.Visibility = Visibility.Collapsed;
                    if (ErrorPlaceholder != null) ErrorPlaceholder.Visibility = Visibility.Collapsed;
                    return;
                }

                var bi = new System.Windows.Media.Imaging.BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(filePath, UriKind.RelativeOrAbsolute);
                bi.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bi.DecodePixelWidth = 360;
                bi.EndInit();

                if (bi.IsDownloading)
                {
                    bi.DownloadCompleted += (_, _) =>
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            ImageCacheService.Instance.AddToMemoryCache(url, bi);
                            if (LoadingPlaceholder != null) LoadingPlaceholder.Visibility = Visibility.Collapsed;
                            if (ErrorPlaceholder != null) ErrorPlaceholder.Visibility = Visibility.Collapsed;
                        }));
                    };
                    bi.DownloadFailed += (_, _) =>
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (LoadingPlaceholder != null) LoadingPlaceholder.Visibility = Visibility.Collapsed;
                            if (ErrorPlaceholder != null) ErrorPlaceholder.Visibility = Visibility.Visible;
                        }));
                    };
                }
                else
                {
                    ImageCacheService.Instance.AddToMemoryCache(url, bi);
                    if (LoadingPlaceholder != null) LoadingPlaceholder.Visibility = Visibility.Collapsed;
                    if (ErrorPlaceholder != null) ErrorPlaceholder.Visibility = Visibility.Collapsed;
                }

                ThumbImage.Source = bi;
            }
            catch
            {
                ThumbImage.Source = null;
                if (LoadingPlaceholder != null) LoadingPlaceholder.Visibility = Visibility.Collapsed;
                if (ErrorPlaceholder != null) ErrorPlaceholder.Visibility = Visibility.Visible;
            }
        }

        private void UpdateCardStates()
        {
            UpdateDownloadStatus();
            UpdateFavoriteStatus();
        }

        private void UpdateFavoriteStatus()
        {
            if (CardFavIcon == null) return;
            try
            {
                if (Item != null && Item.IsFavorite)
                {
                    if (TryFindResource("PinkHeartBrush") is MediaBrush pink)
                        CardFavIcon.Fill = pink;
                    else if (new System.Windows.Media.BrushConverter().ConvertFromString("#F43F5E") is MediaBrush fallbackPink)
                        CardFavIcon.Fill = fallbackPink;

                    if (CardFavBtn != null)
                    {
                        var pinkAlpha = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#33F43F5E");
                        CardFavBtn.Background = new System.Windows.Media.SolidColorBrush(pinkAlpha);
                    }
                }
                else
                {
                    if (TryFindResource("CardActionBtnFgBrush") is MediaBrush fg)
                        CardFavIcon.Fill = fg;
                    else if (TryFindResource("SubtextBrush") is MediaBrush subtext)
                        CardFavIcon.Fill = subtext;

                    if (CardFavBtn != null && TryFindResource("CardActionBtnBgBrush") is MediaBrush bg)
                        CardFavBtn.Background = bg;
                }
            }
            catch { }
        }

        private void UpdateDownloadStatus()
        {
            if (DownloadIconPath == null) return;

            try
            {
                if (Item != null && Item.IsDownloaded)
                {
                    if (TryFindResource("IconCheckGeo") is MediaGeometry checkGeo)
                        DownloadIconPath.Data = checkGeo;
                    if (TryFindResource("EmeraldBrush") is MediaBrush green)
                        DownloadIconPath.Fill = green;
                }
                else
                {
                    if (TryFindResource("IconDownloadGeo") is MediaGeometry dlGeo)
                        DownloadIconPath.Data = dlGeo;
                    if (TryFindResource("CardActionBtnFgBrush") is MediaBrush fg)
                        DownloadIconPath.Fill = fg;
                    else if (new System.Windows.Media.BrushConverter().ConvertFromString("#0EA5E9") is MediaBrush blue)
                        DownloadIconPath.Fill = blue;
                }
            }
            catch { }
        }

        private void Card_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (TryFindResource("AccentBrush") is MediaBrush accent)
                CardBorder.BorderBrush = accent;

            // Hover to reveal discreet/blurred anime artwork
            if (Item?.IsDiscreet == true)
            {
                ThumbImage.Effect = null;
            }
        }

        private void Card_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (TryFindResource("BorderBrush") is MediaBrush border)
                CardBorder.BorderBrush = border;

            // Restore discretion blur
            if (Item?.IsDiscreet == true)
            {
                ThumbImage.Effect = new System.Windows.Media.Effects.BlurEffect
                {
                    Radius = 24,
                    KernelType = System.Windows.Media.Effects.KernelType.Gaussian
                };
            }
        }

        private void Favorite_Click(object sender, RoutedEventArgs e)
        {
            if (Item == null) return;
            FavoritesManager.Instance.ToggleFavorite(Item);
            UpdateFavoriteStatus();
            ShowNotice(Item.IsFavorite ? "Favorited ♥" : "Removed");
            FavoriteToggled?.Invoke(this, Item);
        }

        private async void SetWallpaper_Click(object sender, RoutedEventArgs e)
        {
            if (Item == null) return;

            try
            {
                ShowNotice("Applying…");
                await WallpaperManager.Instance.ApplyWallpaperAsync(Item);
                ShowNotice("Wallpaper Set!");
                WallpaperApplied?.Invoke(this, Item);
            }
            catch (Exception ex)
            {
                ShowNotice($"Error: {ex.Message}");
            }
        }

        private async void Download_Click(object sender, RoutedEventArgs e)
        {
            if (Item == null) return;

            try
            {
                DownloadBar.Visibility = Visibility.Visible;
                var progress = new Progress<double>(p =>
                {
                    DownloadBar.Value = p * 100;
                });

                ShowNotice("Downloading…");
                string localPath = await DownloadsManager.Instance.DownloadPostAsync(Item, progress, CancellationToken.None);
                DownloadBar.Visibility = Visibility.Collapsed;
                UpdateDownloadStatus();
                ShowNotice("Downloaded!");
                DownloadCompleted?.Invoke(this, Item);
            }
            catch (Exception ex)
            {
                DownloadBar.Visibility = Visibility.Collapsed;
                ShowNotice($"Failed: {ex.Message}");
            }
        }

        private void OpenWeb_Click(object sender, RoutedEventArgs e)
        {
            if (Item == null || string.IsNullOrEmpty(Item.SourceUrl)) return;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Item.SourceUrl,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private async void ShowNotice(string text)
        {
            // Cancel any previous notice that is still showing
            _noticeCts.Cancel();
            _noticeCts = new CancellationTokenSource();
            var ct = _noticeCts.Token;

            NoticeText.Text = text;
            NoticeOverlay.Visibility = Visibility.Visible;

            try
            {
                await Task.Delay(1400, ct);
                NoticeOverlay.Visibility = Visibility.Collapsed;
            }
            catch (OperationCanceledException) { /* replaced by newer notice — leave visible */ }
        }

        private void ContextMenu_SetWallpaper(object sender, RoutedEventArgs e)
        {
            SetWallpaper_Click(sender, e);
        }

        private async void ContextMenu_SetLockScreen(object sender, RoutedEventArgs e)
        {
            if (Item == null) return;
            try
            {
                ShowNotice("Applying…");
                bool ok = await WallpaperManager.Instance.SetLockScreenAsync(Item.BestImageUrl);
                if (ok)
                {
                    ShowNotice("Lock Screen Set!");
                }
                else
                {
                    ShowNotice("Failed to set");
                }
            }
            catch (Exception ex)
            {
                ShowNotice($"Error: {ex.Message}");
            }
        }

        private void ContextMenu_ToggleFavorite(object sender, RoutedEventArgs e)
        {
            Favorite_Click(sender, e);
        }

        private void ContextMenu_Download(object sender, RoutedEventArgs e)
        {
            Download_Click(sender, e);
        }

        private void ContextMenu_CopyUrl(object sender, RoutedEventArgs e)
        {
            if (Item == null) return;
            try
            {
                System.Windows.Clipboard.SetText(Item.FullDownloadUrl);
                ShowNotice("URL Copied!");
            }
            catch { }
        }

        private void ContextMenu_OpenWeb(object sender, RoutedEventArgs e)
        {
            OpenWeb_Click(sender, e);
        }
    }
}
