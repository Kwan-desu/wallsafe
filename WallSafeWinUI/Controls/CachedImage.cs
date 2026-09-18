using System;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using WallSafeWinUI.Services;

namespace WallSafeWinUI.Controls
{
    /// <summary>
    /// Attached properties that make a plain Image resolve a remote booru URL through
    /// the on-disk thumbnail cache and then decode it. (Image is sealed in WinUI, so
    /// this is an attached-property helper rather than a subclass.)
    /// </summary>
    public static class CachedImage
    {
        public static readonly DependencyProperty SourceUrlProperty =
            DependencyProperty.RegisterAttached("SourceUrl", typeof(string), typeof(CachedImage),
                new PropertyMetadata(null, OnSourceUrlChanged));

        public static string? GetSourceUrl(DependencyObject o) => (string?)o.GetValue(SourceUrlProperty);
        public static void SetSourceUrl(DependencyObject o, string? v) => o.SetValue(SourceUrlProperty, v);

        public static readonly DependencyProperty DecodeWidthProperty =
            DependencyProperty.RegisterAttached("DecodeWidth", typeof(int), typeof(CachedImage),
                new PropertyMetadata(0));

        public static int GetDecodeWidth(DependencyObject o) => (int)o.GetValue(DecodeWidthProperty);
        public static void SetDecodeWidth(DependencyObject o, int v) => o.SetValue(DecodeWidthProperty, v);

        public static readonly DependencyProperty IsBlurredProperty =
            DependencyProperty.RegisterAttached("IsBlurred", typeof(bool), typeof(CachedImage),
                new PropertyMetadata(false, OnSourceUrlChanged));

        public static bool GetIsBlurred(DependencyObject o) => (bool)o.GetValue(IsBlurredProperty);
        public static void SetIsBlurred(DependencyObject o, bool v) => o.SetValue(IsBlurredProperty, v);

        private static async void OnSourceUrlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Image image) return;
            string? url = GetSourceUrl(image);
            if (string.IsNullOrEmpty(url)) { image.Source = null; return; }

            try
            {
                bool isBlurred = GetIsBlurred(image);
                string path = await ImageCacheService.Instance.GetCachedImagePathAsync(url);
                if (GetSourceUrl(image) != url) return;

                if (isBlurred)
                {
                    if (File.Exists(path))
                    {
                        var blurSource = await ImageCacheService.Instance.GetBlurredBitmapSourceAsync(path);
                        if (GetSourceUrl(image) == url && blurSource != null)
                            image.Source = blurSource;
                    }
                    return;
                }

                var bmp = new BitmapImage();
                int decodeWidth = GetDecodeWidth(image);
                if (decodeWidth > 0) bmp.DecodePixelWidth = decodeWidth;

                if (File.Exists(path))
                {
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    await bmp.SetSourceAsync(stream.AsRandomAccessStream());
                }
                else
                {
                    bmp.UriSource = new Uri(url);
                }
                if (GetSourceUrl(image) == url) image.Source = bmp;
            }
            catch { }
        }
    }
}
