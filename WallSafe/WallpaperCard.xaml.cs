using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
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
        public event EventHandler<PostItem>? MoveToCollectionRequested;
        public event EventHandler<PostItem>? RemoveFromCollectionRequested;

        public static readonly DependencyProperty IsInCollectionFolderProperty =
            DependencyProperty.Register(nameof(IsInCollectionFolder), typeof(bool), typeof(WallpaperCard),
                new PropertyMetadata(false, OnIsInCollectionFolderChanged));

        public bool IsInCollectionFolder
        {
            get => (bool)GetValue(IsInCollectionFolderProperty);
            set => SetValue(IsInCollectionFolderProperty, value);
        }

        private static void OnIsInCollectionFolderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WallpaperCard card)
            {
                card.UpdateFavoriteStatus();
            }
        }

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
            // A single click focuses the card for keyboard interaction; double-click opens preview.
            Keyboard.Focus(CardBorder);
            _dragStart = e.GetPosition(null);
            if (e.ClickCount == 2)
            {
                if (Item != null)
                {
                    PreviewRequested?.Invoke(this, Item);
                }
                e.Handled = true;
            }
        }

        private System.Windows.Point _dragStart;

        private void Card_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || Item == null) return;
            var pos = e.GetPosition(null);
            if (System.Math.Abs(pos.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
                System.Math.Abs(pos.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            // Begin a drag carrying this wallpaper so it can be dropped onto a collection folder.
            try
            {
                var data = new System.Windows.DataObject("WallSafePostItem", Item);
                System.Windows.DragDrop.DoDragDrop(this, data, System.Windows.DragDropEffects.Copy);
            }
            catch { }
        }

        // ─────────────── Keyboard accessibility ───────────────

        private void Card_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (Item == null) return;
            switch (e.Key)
            {
                case Key.Enter:
                case Key.Space:
                    PreviewRequested?.Invoke(this, Item);
                    e.Handled = true;
                    break;
                case Key.A: // Apply as wallpaper
                    SetWallpaper_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.F: // Favorite toggle
                    Favorite_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.D: // Download
                    Download_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.O: // Open source in browser
                    OpenWeb_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
            }
        }

        private void Card_GotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            if (FocusRing != null) FocusRing.Visibility = Visibility.Visible;
            // Reveal a discreet (blurred) thumbnail while it has focus, so keyboard users
            // aren't locked out of the mouse-hover reveal behavior.
            if (Item?.IsDiscreet == true) ThumbImage.Effect = null;
        }

        private void Card_LostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            if (FocusRing != null) FocusRing.Visibility = Visibility.Collapsed;
            if (Item?.IsDiscreet == true)
            {
                ThumbImage.Effect = new System.Windows.Media.Effects.BlurEffect
                {
                    Radius = 24,
                    KernelType = System.Windows.Media.Effects.KernelType.Gaussian
                };
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
                if (IsInCollectionFolder)
                {
                    if (TryFindResource("IconFolderMinusGeo") is MediaGeometry minusGeo)
                        CardFavIcon.Data = minusGeo;
                    else if (TryFindResource("IconCloseGeo") is MediaGeometry closeGeo)
                        CardFavIcon.Data = closeGeo;

                    if (TryFindResource("AccentHoverBrush") is MediaBrush hoverBrush)
                        CardFavIcon.Fill = hoverBrush;
                    else if (new System.Windows.Media.BrushConverter().ConvertFromString("#F59E0B") is MediaBrush amber)
                        CardFavIcon.Fill = amber;

                    if (CardFavBtn != null)
                    {
                        string colName = !string.IsNullOrEmpty(Item?.Collection) ? Item.Collection : "collection";
                        CardFavBtn.ToolTip = $"Remove from \u201c{colName}\u201d (keeps in Favorites) (F)";
                        System.Windows.Automation.AutomationProperties.SetName(CardFavBtn, $"Remove from collection {colName}");
                        var amberAlpha = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#33F59E0B");
                        CardFavBtn.Background = new System.Windows.Media.SolidColorBrush(amberAlpha);
                    }
                    return;
                }

                // Normal favorite mode
                if (TryFindResource("IconHeartGeo") is MediaGeometry heartGeo)
                    CardFavIcon.Data = heartGeo;

                if (CardFavBtn != null)
                {
                    CardFavBtn.ToolTip = "Favorite (F)";
                    System.Windows.Automation.AutomationProperties.SetName(
                        CardFavBtn,
                        Item != null && Item.IsFavorite ? "Remove from favorites" : "Add to favorites");
                }

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
                if (CardDownloadBtn != null)
                {
                    System.Windows.Automation.AutomationProperties.SetName(
                        CardDownloadBtn,
                        Item != null && Item.IsDownloaded ? "Already downloaded" : "Download original full resolution wallpaper");
                }
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

            if (IsInCollectionFolder)
            {
                string col = Item.Collection;
                FavoritesManager.Instance.AssignToCollection(Item, "");
                ShowNotice("Removed from folder");
                RemoveFromCollectionRequested?.Invoke(this, Item);
                return;
            }

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
                ShowRetryNotice(FriendlyError("Couldn't set wallpaper", ex),
                    () => SetWallpaper_Click(sender, e));
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
                ShowRetryNotice(FriendlyError("Download failed", ex),
                    () => Download_Click(sender, e));
            }
        }

        /// <summary>Translate a raw exception into a short, human-readable message.</summary>
        private static string FriendlyError(string prefix, Exception ex)
        {
            string detail = ex switch
            {
                System.Net.Http.HttpRequestException => "network problem — check your connection",
                TaskCanceledException => "the request timed out",
                OperationCanceledException => "the request timed out",
                UnauthorizedAccessException => "permission denied writing the file",
                IOException io when io.Message.Contains("disk", StringComparison.OrdinalIgnoreCase)
                    => "not enough disk space",
                IOException => "a file error occurred",
                _ => "an unexpected error occurred"
            };
            return $"{prefix}: {detail}. Tap to retry.";
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

            _retryAction = null;
            NoticeOverlay.Cursor = System.Windows.Input.Cursors.Arrow;
            NoticeText.Text = text;
            NoticeOverlay.Visibility = Visibility.Visible;

            try
            {
                await Task.Delay(1400, ct);
                NoticeOverlay.Visibility = Visibility.Collapsed;
            }
            catch (OperationCanceledException) { /* replaced by newer notice — leave visible */ }
        }

        private Action? _retryAction;

        /// <summary>Show an error notice the user can click (or press Enter on) to retry.</summary>
        private async void ShowRetryNotice(string text, Action retry)
        {
            _noticeCts.Cancel();
            _noticeCts = new CancellationTokenSource();
            var ct = _noticeCts.Token;

            _retryAction = retry;
            NoticeText.Text = text;
            NoticeOverlay.Cursor = System.Windows.Input.Cursors.Hand;
            System.Windows.Automation.AutomationProperties.SetName(NoticeOverlay, text);
            NoticeOverlay.Visibility = Visibility.Visible;

            try
            {
                // Errors linger longer (5s) so the user has time to click retry.
                await Task.Delay(5000, ct);
                NoticeOverlay.Visibility = Visibility.Collapsed;
                _retryAction = null;
            }
            catch (OperationCanceledException) { }
        }

        private void NoticeOverlay_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var action = _retryAction;
            if (action != null)
            {
                _retryAction = null;
                NoticeOverlay.Visibility = Visibility.Collapsed;
                action();
            }
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
                ShowRetryNotice(FriendlyError("Couldn't set lock screen", ex),
                    () => ContextMenu_SetLockScreen(sender, e));
            }
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (MenuRemoveFromFolder == null || MenuToggleFavorite == null) return;

            bool inFolder = IsInCollectionFolder || !string.IsNullOrEmpty(Item?.Collection);
            if (inFolder)
            {
                MenuRemoveFromFolder.Visibility = Visibility.Visible;
                string colName = Item?.Collection ?? "";
                MenuRemoveFromFolder.Header = !string.IsNullOrEmpty(colName)
                    ? $"Remove from \u201c{colName}\u201d (keep in Favorites)"
                    : "Remove from this Collection";
                MenuToggleFavorite.Header = "Remove from Favorites Entirely";
            }
            else
            {
                MenuRemoveFromFolder.Visibility = Visibility.Collapsed;
                MenuToggleFavorite.Header = Item?.IsFavorite == true ? "Remove from Favorites" : "Add to Favorites";
            }
        }

        private void ContextMenu_RemoveFromFolder(object sender, RoutedEventArgs e)
        {
            if (Item == null) return;
            FavoritesManager.Instance.AssignToCollection(Item, "");
            ShowNotice("Removed from folder");
            RemoveFromCollectionRequested?.Invoke(this, Item);
        }

        private void ContextMenu_ToggleFavorite(object sender, RoutedEventArgs e)
        {
            if (Item == null) return;
            FavoritesManager.Instance.ToggleFavorite(Item);
            UpdateFavoriteStatus();
            ShowNotice(Item.IsFavorite ? "Favorited ♥" : "Removed from Favorites");
            FavoriteToggled?.Invoke(this, Item);
        }

        private void ContextMenu_MoveToCollection(object sender, RoutedEventArgs e)
        {
            if (Item != null)
                MoveToCollectionRequested?.Invoke(this, Item);
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
