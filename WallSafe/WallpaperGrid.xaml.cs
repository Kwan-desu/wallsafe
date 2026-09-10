using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WallSafe
{
    public partial class WallpaperGrid : System.Windows.Controls.UserControl
    {
        public static readonly DependencyProperty CardWidthProperty =
            DependencyProperty.Register(nameof(CardWidth), typeof(double), typeof(WallpaperGrid), new PropertyMetadata(260.0));

        public static readonly DependencyProperty CardHeightProperty =
            DependencyProperty.Register(nameof(CardHeight), typeof(double), typeof(WallpaperGrid), new PropertyMetadata(151.0));

        public double CardWidth
        {
            get => (double)GetValue(CardWidthProperty);
            set => SetValue(CardWidthProperty, value);
        }

        public double CardHeight
        {
            get => (double)GetValue(CardHeightProperty);
            set => SetValue(CardHeightProperty, value);
        }

        public event EventHandler<PostItem>? WallpaperApplied;
        public event EventHandler<PostItem>? FavoriteToggled;
        public event EventHandler<PostItem>? DownloadCompleted;
        public event EventHandler<PostItem>? PreviewRequested;
        public event EventHandler<PostItem>? MoveToCollectionRequested;
        public event EventHandler? LoadMoreRequested;

        private readonly ObservableCollection<PostItem> _posts = new();
        private readonly HashSet<string> _postKeys = new(StringComparer.OrdinalIgnoreCase);
        private DateTime _lastLoadMoreTime = DateTime.MinValue;
        private const double LoadMoreThrottleMs = 600; // Minimum ms between load-more triggers
        private bool _isLoadingMore;
        private bool _canLoadMore;
        private System.Windows.Media.Animation.DoubleAnimation? _spinnerAnim;

        public bool IsEmpty => _posts.Count == 0;
        public int PostCount => _posts.Count;

        public WallpaperGrid()
        {
            InitializeComponent();
            PostsPanel.ItemsSource = _posts;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateCardDimensions();
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateCardDimensions();
        }

        private void UpdateCardDimensions()
        {
            double availableWidth = MainScroll.ViewportWidth;
            if (availableWidth <= 0)
            {
                availableWidth = MainScroll.ActualWidth;
                if (availableWidth <= 0)
                {
                    availableWidth = ActualWidth;
                }
                if (availableWidth > 30)
                {
                    availableWidth -= SystemParameters.VerticalScrollBarWidth > 0
                        ? SystemParameters.VerticalScrollBarWidth
                        : 18;
                }
            }

            // Deduct StackPanel horizontal margin (4 on left + 4 on right = 8)
            availableWidth -= 8;

            if (availableWidth < 180)
            {
                availableWidth = 260;
            }

            // Target column width varies by user's density preference.
            double targetColWidth = Settings.Instance.CardDensity switch
            {
                "compact" => 190.0,
                "large" => 320.0,
                _ => 240.0 // comfortable
            };
            int cols = Math.Max(2, (int)Math.Floor(availableWidth / targetColWidth));
            double itemWidth = Math.Floor(availableWidth / cols);
            double itemHeight = Math.Round(itemWidth * 0.58); // 16:9 - 16:10 landscape desktop ratio

            if (itemWidth > 80 && (Math.Abs(CardWidth - itemWidth) > 0.5 || Math.Abs(CardHeight - itemHeight) > 0.5))
            {
                CardWidth = itemWidth;
                CardHeight = itemHeight;
            }
        }

        public void Clear()
        {
            _posts.Clear();
            _postKeys.Clear();
            _isLoadingMore = false;
            _canLoadMore = false;
            StopSpinnerAnimation();
            BottomLoadingSpinner.Visibility = Visibility.Collapsed;
            LoadMoreBtn.Visibility = Visibility.Collapsed;
            EndOfResultsText.Visibility = Visibility.Collapsed;
            MainScroll.ScrollToTop();
        }

        /// <summary>Re-run card layout after the density setting changes.</summary>
        public void RefreshDensity() => UpdateCardDimensions();

        public void SetLoadingMore(bool isLoading)
        {
            _isLoadingMore = isLoading;
            if (isLoading)
            {
                LoadMoreBtn.Visibility = Visibility.Collapsed;
                EndOfResultsText.Visibility = Visibility.Collapsed;
                BottomLoadingSpinner.Visibility = Visibility.Visible;
                StartSpinnerAnimation();
            }
            else
            {
                StopSpinnerAnimation();
                BottomLoadingSpinner.Visibility = Visibility.Collapsed;
                if (_canLoadMore && _posts.Count >= 10)
                {
                    LoadMoreBtn.Visibility = Visibility.Visible;
                    EndOfResultsText.Visibility = Visibility.Collapsed;
                }
                else if (_posts.Count > 0)
                {
                    LoadMoreBtn.Visibility = Visibility.Collapsed;
                    EndOfResultsText.Visibility = Visibility.Visible;
                }
                else
                {
                    LoadMoreBtn.Visibility = Visibility.Collapsed;
                    EndOfResultsText.Visibility = Visibility.Collapsed;
                }
            }
        }

        public void AddPosts(IEnumerable<PostItem> posts, bool canLoadMore = true)
        {
            _canLoadMore = canLoadMore;
            foreach (var p in posts)
            {
                var key = $"{p.Source}:{p.Id}";
                if (_postKeys.Add(key))
                {
                    _posts.Add(p);
                }
            }
            SetLoadingMore(false);
            PostsChanged?.Invoke(this, _posts.Count);
        }

        public void SetPosts(IEnumerable<PostItem> posts, bool canLoadMore = false)
        {
            _posts.Clear();
            _postKeys.Clear();
            _canLoadMore = canLoadMore;
            foreach (var p in posts)
            {
                var key = $"{p.Source}:{p.Id}";
                if (_postKeys.Add(key))
                {
                    _posts.Add(p);
                }
            }
            SetLoadingMore(false);
            PostsChanged?.Invoke(this, _posts.Count);
        }

        /// <summary>Raised whenever the number of displayed posts changes (for result-count UI).</summary>
        public event EventHandler<int>? PostsChanged;

        private void StartSpinnerAnimation()
        {
            if (_spinnerAnim == null)
            {
                _spinnerAnim = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = 0,
                    To = 360,
                    Duration = TimeSpan.FromSeconds(0.9),
                    RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
                };
            }
            BottomSpinnerRotation.BeginAnimation(RotateTransform.AngleProperty, _spinnerAnim);
        }

        private void StopSpinnerAnimation()
        {
            BottomSpinnerRotation.BeginAnimation(RotateTransform.AngleProperty, null);
        }

        private void Card_WallpaperApplied(object? sender, PostItem e) =>
            WallpaperApplied?.Invoke(this, e);

        private void Card_FavoriteToggled(object? sender, PostItem e) =>
            FavoriteToggled?.Invoke(this, e);

        private void Card_DownloadCompleted(object? sender, PostItem e) =>
            DownloadCompleted?.Invoke(this, e);

        private void Card_PreviewRequested(object? sender, PostItem e) =>
            PreviewRequested?.Invoke(this, e);

        private void Card_MoveToCollectionRequested(object? sender, PostItem e) =>
            MoveToCollectionRequested?.Invoke(this, e);

        private void LoadMore_Click(object sender, RoutedEventArgs e) =>
            TriggerLoadMore();

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.ViewportWidthChange != 0)
            {
                UpdateCardDimensions();
            }

            // Seamless infinite scroll trigger: when user scrolls near the bottom (within 250px)
            if (e.VerticalChange > 0 && e.VerticalOffset >= e.ExtentHeight - e.ViewportHeight - 250)
            {
                if (!_isLoadingMore && _canLoadMore && _posts.Count > 0)
                {
                    TriggerLoadMore();
                }
            }
        }

        private void TriggerLoadMore()
        {
            // Debounce: ignore rapid successive triggers within threshold
            var now = DateTime.UtcNow;
            if ((now - _lastLoadMoreTime).TotalMilliseconds < LoadMoreThrottleMs) return;
            _lastLoadMoreTime = now;
            SetLoadingMore(true);
            LoadMoreRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
