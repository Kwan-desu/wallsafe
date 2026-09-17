using System;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using WallSafeWinUI.Services;

namespace WallSafeWinUI.Views
{
    /// <summary>
    /// Progressive high-res preview: shows the cached thumbnail immediately, then swaps
    /// in the full-res sample. Always shows the WHOLE image (Uniform, never cropped) in a
    /// wide landscape dialog, with favorite / apply / download / open-source actions.
    /// </summary>
    public static class PreviewDialog
    {
        public static async System.Threading.Tasks.Task ShowAsync(XamlRoot xamlRoot, PostItem post)
        {
            // A wide viewer box; the image uses Uniform stretch so the entire picture is
            // always visible (letterboxed if needed), never cropped.
            var image = new Image
            {
                Stretch = Stretch.Uniform,
                MaxWidth = 1000,
                MaxHeight = 540,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            try
            {
                string thumbPath = await ImageCacheService.Instance.GetCachedImagePathAsync(post.PreviewUrl);
                var thumb = new BitmapImage();
                if (File.Exists(thumbPath))
                {
                    using var s = File.OpenRead(thumbPath);
                    await thumb.SetSourceAsync(s.AsRandomAccessStream());
                }
                else thumb.UriSource = new Uri(post.PreviewUrl);
                image.Source = thumb;
            }
            catch { }

            var imageHost = new Border
            {
                Width = 1000,
                Height = 520,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x22, 0, 0, 0)),
                Child = image
            };

            // Always-visible header row: title + info + favorite/source buttons.
            var favBtn = new Button { Content = MakeIcon(post.IsFavorite ? "\uEB52" : "\uEB51", "Favorite") };
            favBtn.Click += (_, _) =>
            {
                bool now = FavoritesManager.Instance.ToggleFavorite(post);
                favBtn.Content = MakeIcon(now ? "\uEB52" : "\uEB51", "Favorite");
            };

            var sourceBtn = new Button { Content = MakeIcon("\uE774", "Open source in browser") };
            sourceBtn.Click += async (_, _) =>
            {
                if (!string.IsNullOrEmpty(post.SourceUrl))
                    try { await Windows.System.Launcher.LaunchUriAsync(new Uri(post.SourceUrl)); } catch { }
            };

            var info = new TextBlock
            {
                Text = $"{post.ResolutionText}   ·   {post.RatingDisplay}   ·   {post.AspectRatioText}",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            };

            var header = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(info, 0);
            var headerActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            headerActions.Children.Add(favBtn);
            headerActions.Children.Add(sourceBtn);
            Grid.SetColumn(headerActions, 1);
            header.Children.Add(info);
            header.Children.Add(headerActions);

            var body = new StackPanel { Spacing = 4 };
            body.Children.Add(header);
            body.Children.Add(imageHost);

            var dialog = new ContentDialog
            {
                XamlRoot = xamlRoot,
                Title = "Preview",
                Content = body,
                PrimaryButtonText = "Apply wallpaper",
                SecondaryButtonText = "Download",
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Primary,
                Resources =
                {
                    ["ContentDialogMaxWidth"] = 1080.0,
                    ["ContentDialogMinWidth"] = 1040.0
                }
            };

            _ = LoadFullResAsync(image, post);

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                try { await WallpaperManager.Instance.ApplyWallpaperAsync(post, Settings.Instance.TargetMonitor); } catch { }
            }
            else if (result == ContentDialogResult.Secondary)
            {
                try { await DownloadsManager.Instance.DownloadPostAsync(post); } catch { }
            }
        }

        private static StackPanel MakeIcon(string glyph, string tooltip)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            sp.Children.Add(new FontIcon { Glyph = glyph, FontSize = 16 });
            ToolTipService.SetToolTip(sp, tooltip);
            return sp;
        }

        private static async System.Threading.Tasks.Task LoadFullResAsync(Image image, PostItem post)
        {
            try
            {
                string url = post.BestImageUrl;
                string path = await ImageCacheService.Instance.GetCachedImagePathAsync(url);
                var full = new BitmapImage();
                if (File.Exists(path))
                {
                    using var s = File.OpenRead(path);
                    await full.SetSourceAsync(s.AsRandomAccessStream());
                }
                else full.UriSource = new Uri(url);
                image.Source = full;
            }
            catch { }
        }
    }
}
