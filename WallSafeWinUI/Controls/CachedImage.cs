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
            if (string.IsNullOrWhiteSpace(url))
            {
                image.Source = null;
                return;
            }

            var dispatcher = image.DispatcherQueue;
            bool isBlurred = GetIsBlurred(image);
            int decodeWidth = GetDecodeWidth(image);

            try
            {
                string filePath;
                if (isBlurred)
                {
                    filePath = await ImageCacheService.Instance.GetCachedBlurredImagePathAsync(url);
                }
                else
                {
                    filePath = await ImageCacheService.Instance.GetCachedImagePathAsync(url);
                }

                if (string.IsNullOrEmpty(filePath)) return;

                void SetImageSource()
                {
                    try
                    {
                        if (GetSourceUrl(image) != url) return;

                        var bmp = new BitmapImage();
                        if (decodeWidth > 0 && !isBlurred)
                            bmp.DecodePixelWidth = decodeWidth;

                        if (File.Exists(filePath))
                        {
                            bmp.UriSource = new Uri(filePath);
                        }
                        else if (Uri.TryCreate(filePath, UriKind.Absolute, out var uri))
                        {
                            bmp.UriSource = uri;
                        }

                        if (GetSourceUrl(image) == url)
                        {
                            image.Source = bmp;
                        }
                    }
                    catch { }
                }

                if (dispatcher != null && !dispatcher.HasThreadAccess)
                {
                    dispatcher.TryEnqueue(SetImageSource);
                }
                else
                {
                    SetImageSource();
                }
            }
            catch { }
        }
    }
}
