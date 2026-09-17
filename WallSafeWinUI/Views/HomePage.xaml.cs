using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using WallSafeWinUI.Services;

namespace WallSafeWinUI.Views
{
    public sealed partial class HomePage : Page
    {
        private readonly ApiClient _api = new();
        private readonly ObservableCollection<PostItem> _trending = new();
        private readonly ObservableCollection<PostItem> _shelf = new();
        private readonly ObservableCollection<SeriesCategoryItem> _franchises = new();
        private CancellationTokenSource? _cts;
        private bool _loaded;
        private bool _isSyncingScroll;

        public HomePage()
        {
            InitializeComponent();
            TrendingList.ItemsSource = _trending;
            ShelfList.ItemsSource = _shelf;
            FranchiseList.ItemsSource = _franchises;

            // Carousel scroll & size synchronization for Windows App style pagination
            TrendingScroller.ViewChanged += Scroller_ViewChanged;
            FranchiseScroller.ViewChanged += Scroller_ViewChanged;
            ShelfScroller.ViewChanged += Scroller_ViewChanged;

            TrendingScroller.SizeChanged += (_, _) => UpdateShelfPages(TrendingScroller, TrendingPager);
            FranchiseScroller.SizeChanged += (_, _) => UpdateShelfPages(FranchiseScroller, FranchisePager);
            ShelfScroller.SizeChanged += (_, _) => UpdateShelfPages(ShelfScroller, ShelfPager);

            TrendingList.SizeChanged += (_, _) => UpdateShelfPages(TrendingScroller, TrendingPager);
            FranchiseList.SizeChanged += (_, _) => UpdateShelfPages(FranchiseScroller, FranchisePager);
            ShelfList.SizeChanged += (_, _) => UpdateShelfPages(ShelfScroller, ShelfPager);

            Loaded += async (_, _) => { try { await MaybeShowFirstRunAsync(); } catch { } };
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            AppEvents.RatingModeChanged += OnRatingModeChanged;
            WallpaperManager.Instance.SafeWallpaperTriggered += OnPanicTriggered;
            RefreshStats();
            LoadShelf();
            if (!_loaded)
            {
                _loaded = true;
                ReloadAll();
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            AppEvents.RatingModeChanged -= OnRatingModeChanged;
            WallpaperManager.Instance.SafeWallpaperTriggered -= OnPanicTriggered;
            _cts?.Cancel();
        }

        private void OnPanicTriggered() => DispatcherQueue.TryEnqueue(RefreshStats);

        private void OnRatingModeChanged(ContentRatingMode mode) => DispatcherQueue.TryEnqueue(ReloadAll);

        private void ReloadAll()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            _ = LoadTrendingAsync(ct);
            _ = LoadFranchisesAsync(ct);
        }

        // ── data loading ──────────────────────────────────────────
        private async Task LoadTrendingAsync(CancellationToken ct)
        {
            TrendingLoader.IsActive = true;
            _trending.Clear();
            try
            {
                string source = Settings.Instance.RatingMode == ContentRatingMode.SfwOnly ? "konasfw" : "all";
                var list = await _api.FetchPostsAsync(source, "order:score", 1, 15, ct);
                if (ct.IsCancellationRequested) return;
                foreach (var p in list.Take(15)) _trending.Add(p);
                DispatcherQueue.TryEnqueue(() => UpdateShelfPages(TrendingScroller, TrendingPager));
            }
            catch { }
            finally { TrendingLoader.IsActive = false; }
        }

        private async Task LoadFranchisesAsync(CancellationToken ct)
        {
            FranchiseLoader.IsActive = true;
            _franchises.Clear();
            string sourceKey = Settings.Instance.RatingMode == ContentRatingMode.SfwOnly ? "konasfw" : "all";
            bool sfw = Settings.Instance.RatingMode == ContentRatingMode.SfwOnly;
            FranchiseSubtitle.Text = "Live ranking • " + (sfw ? "SFW sources" : "All sources");
            try
            {
                var rawTags = await _api.GetPopularSeriesFromSourceAsync(sourceKey, 15, ct);
                if (ct.IsCancellationRequested) return;
                var built = new List<SeriesCategoryItem>();
                foreach (var (tag, count) in rawTags)
                {
                    built.Add(new SeriesCategoryItem
                    {
                        Name = FormatSeriesTitle(tag),
                        Tag = tag,
                        Type = FormatSeriesType(tag),
                        PostCount = count,
                        PreviewImageUrl = ""
                    });
                }
                foreach (var item in built) _franchises.Add(item);
                DispatcherQueue.TryEnqueue(() => UpdateShelfPages(FranchiseScroller, FranchisePager));

                _ = Task.Run(async () =>
                {
                    foreach (var item in built)
                    {
                        if (ct.IsCancellationRequested) break;
                        try
                        {
                            var preview = await _api.GetSourceCategoryPreviewUrlAsync(sourceKey, item.Tag, ct);
                            if (!string.IsNullOrEmpty(preview))
                                DispatcherQueue.TryEnqueue(() => item.PreviewImageUrl = preview);
                        }
                        catch { }
                    }
                }, ct);
            }
            catch { }
            finally { FranchiseLoader.IsActive = false; }
        }

        private void LoadShelf()
        {
            _shelf.Clear();
            foreach (var p in FavoritesManager.Instance.GetAllFavorites().Take(20)) _shelf.Add(p);
            bool hasFavs = _shelf.Count > 0;
            ShelfEmpty.Visibility = hasFavs ? Visibility.Collapsed : Visibility.Visible;
            ShelfScroller.Visibility = hasFavs ? Visibility.Visible : Visibility.Collapsed;
            ShelfNavRow.Visibility = hasFavs ? Visibility.Visible : Visibility.Collapsed;
            if (hasFavs)
            {
                DispatcherQueue.TryEnqueue(() => UpdateShelfPages(ShelfScroller, ShelfPager));
            }
        }

        // ── stats & greeting ──────────────────────────────────────
        private void RefreshStats()
        {
            try
            {
                StatsRow.Visibility = Settings.Instance.ShowHomeStats ? Visibility.Visible : Visibility.Collapsed;
                var stats = HomeStats.Compute();
                StatFav.Text = stats.Favorites.ToString();
                StatDl.Text = stats.Downloads.ToString();
                StatApplied.Text = stats.Applied.ToString();
                StatTopTag.Text = stats.Favorites > 0 ? stats.TopTagDisplay : "—";
                StatPanic.Text = stats.PanicTriggers.ToString();
            }
            catch { }
            UpdateGreeting();
        }

        private void UpdateGreeting()
        {
            string part = DateTime.Now.Hour switch
            {
                < 12 => "Good morning",
                < 18 => "Good afternoon",
                _ => "Good evening"
            };
            string name = Settings.Instance.UserName?.Trim() ?? "";
            if (Settings.Instance.ShowGreeting && !string.IsNullOrEmpty(name))
            {
                GreetingText.Text = $"{part}, {name}";
                GreetingSub.Text = "Here's your wallpaper dashboard.";
            }
            else
            {
                GreetingText.Text = "Home";
                GreetingSub.Text = "Your wallpaper dashboard";
            }
        }

        private async Task MaybeShowFirstRunAsync()
        {
            if (Settings.Instance.FirstRunCompleted) return;
            Settings.Instance.FirstRunCompleted = true;
            Settings.Instance.Save();

            var input = new TextBox { PlaceholderText = "Your name (optional)", MaxLength = 40 };
            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(new TextBlock
            {
                Text = "Personalize your dashboard greeting. You can change or turn this off anytime in Settings.",
                TextWrapping = TextWrapping.Wrap
            });
            panel.Children.Add(input);

            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Welcome to WallSafe",
                Content = panel,
                PrimaryButtonText = "Save",
                CloseButtonText = "Skip",
                DefaultButton = ContentDialogButton.Primary
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(input.Text))
            {
                Settings.Instance.UserName = input.Text.Trim();
                Settings.Instance.Save();
                UpdateGreeting();
            }
        }

        private static string FormatSeriesTitle(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return tag;
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(tag.Replace('_', ' ').Trim());
        }

        private static string FormatSeriesType(string tag)
        {
            string t = tag.ToLowerInvariant();
            if (t.Contains("genshin") || t.Contains("honkai") || t.Contains("zenless") ||
                t.Contains("wuthering") || t.Contains("nikke") || t.Contains("azur_lane") ||
                t.Contains("arknights") || t.Contains("blue_archive") || t.Contains("fate"))
                return "Gaming";
            return "Anime";
        }

        // ── carousel scroll & pager synchronization ───────────────
        private void Scroller_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            if (sender is ScrollViewer sv)
            {
                PipsPager? pager = sv == TrendingScroller ? TrendingPager
                    : sv == FranchiseScroller ? FranchisePager
                    : sv == ShelfScroller ? ShelfPager : null;

                if (pager != null)
                    SyncPagerWithScroll(sv, pager);
            }
        }

        private void SyncPagerWithScroll(ScrollViewer sv, PipsPager pager)
        {
            if (_isSyncingScroll || sv.ViewportWidth <= 0) return;
            int curPage = (int)Math.Round(sv.HorizontalOffset / sv.ViewportWidth);
            int maxPage = Math.Max(0, pager.NumberOfPages - 1);
            int clamped = Math.Clamp(curPage, 0, maxPage);
            if (pager.SelectedPageIndex != clamped)
            {
                _isSyncingScroll = true;
                pager.SelectedPageIndex = clamped;
                _isSyncingScroll = false;
            }
        }

        private void UpdateShelfPages(ScrollViewer sv, PipsPager pager)
        {
            if (sv.ViewportWidth <= 0 || sv.ExtentWidth <= 0) return;
            int pages = (int)Math.Ceiling(sv.ExtentWidth / sv.ViewportWidth);
            if (pages < 1) pages = 1;
            pager.NumberOfPages = pages;
            pager.Visibility = pages > 1 ? Visibility.Visible : Visibility.Collapsed;
            SyncPagerWithScroll(sv, pager);
        }

        private void OnPagerChanged(ScrollViewer sv, int newIndex)
        {
            if (_isSyncingScroll || sv.ViewportWidth <= 0) return;
            try
            {
                _isSyncingScroll = true;
                double target = newIndex * sv.ViewportWidth;
                sv.ChangeView(Math.Clamp(target, 0, Math.Max(0, sv.ScrollableWidth)), null, null);
            }
            finally
            {
                _isSyncingScroll = false;
            }
        }

        private void ScrollShelf(ScrollViewer sv, double direction)
        {
            if (sv == null) return;
            double step = sv.ViewportWidth > 0 ? sv.ViewportWidth * 0.8 : 600;
            sv.ChangeView(Math.Clamp(sv.HorizontalOffset + (direction * step), 0, Math.Max(0, sv.ScrollableWidth)), null, null);
        }

        private void TrendingLeft_Click(object sender, RoutedEventArgs e) => ScrollShelf(TrendingScroller, -1);
        private void TrendingRight_Click(object sender, RoutedEventArgs e) => ScrollShelf(TrendingScroller, 1);
        private void FranchiseLeft_Click(object sender, RoutedEventArgs e) => ScrollShelf(FranchiseScroller, -1);
        private void FranchiseRight_Click(object sender, RoutedEventArgs e) => ScrollShelf(FranchiseScroller, 1);
        private void ShelfLeft_Click(object sender, RoutedEventArgs e) => ScrollShelf(ShelfScroller, -1);
        private void ShelfRight_Click(object sender, RoutedEventArgs e) => ScrollShelf(ShelfScroller, 1);

        private void TrendingPager_SelectedIndexChanged(PipsPager sender, PipsPagerSelectedIndexChangedEventArgs args)
            => OnPagerChanged(TrendingScroller, sender.SelectedPageIndex);

        private void FranchisePager_SelectedIndexChanged(PipsPager sender, PipsPagerSelectedIndexChangedEventArgs args)
            => OnPagerChanged(FranchiseScroller, sender.SelectedPageIndex);

        private void ShelfPager_SelectedIndexChanged(PipsPager sender, PipsPagerSelectedIndexChangedEventArgs args)
            => OnPagerChanged(ShelfScroller, sender.SelectedPageIndex);

        private void ShelfScroller_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            if (sender is ScrollViewer sv)
            {
                var props = e.GetCurrentPoint(sv).Properties;
                int delta = props.MouseWheelDelta;
                if (delta != 0)
                {
                    sv.ChangeView(Math.Clamp(sv.HorizontalOffset - (delta * 1.5), 0, Math.Max(0, sv.ScrollableWidth)), null, null);
                    e.Handled = true;
                }
            }
        }

        // ── franchise click ───────────────────────────────────────
        private void Franchise_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is string tag && !string.IsNullOrEmpty(tag))
                App.RootWindow?.NavigateToExploreWithTag(tag);
        }

        // ── navigation & actions ──────────────────────────────────
        private void Explore_Click(object sender, RoutedEventArgs e) => App.RootWindow?.NavigateFromHomeToExplore();
        private void ViewAllTrending_Click(object sender, RoutedEventArgs e) => App.RootWindow?.NavigateFromHomeToExplore();
        private void ViewAllFavorites_Click(object sender, RoutedEventArgs e) => App.RootWindow?.NavigateToFavorites();
        private void Panic_Click(object sender, RoutedEventArgs e) => WallpaperManager.Instance.ApplySafeWallpaper();
    }
}
