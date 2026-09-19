using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WallSafeWinUI.Controls;
using WallSafeWinUI.Services;

namespace WallSafeWinUI.Views
{
    public sealed partial class ExplorePage : Page
    {
        private readonly ApiClient _api = new();
        private IncrementalPostCollection? _items;
        private string _tags = "";
        private bool _initialized;
        private CancellationTokenSource? _suggestCts;

        public ExplorePage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            AppEvents.RatingModeChanged += OnRatingModeChanged;
            AppEvents.ExploreTagRequested += OnExploreTagRequested;

            if (!_initialized)
            {
                _initialized = true;
                var profile = Settings.Instance.GetFilterProfileForMode(Settings.Instance.RatingMode);
                SelectByTag(SourceCombo, profile.Source);
            }

            // A tag may have been passed in via navigation (franchise click).
            if (e.Parameter is string tag && !string.IsNullOrWhiteSpace(tag))
            {
                _tags = tag;
                SearchBox.Text = tag;
            }
            Reload();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            AppEvents.RatingModeChanged -= OnRatingModeChanged;
            AppEvents.ExploreTagRequested -= OnExploreTagRequested;
        }

        private void OnRatingModeChanged(ContentRatingMode mode)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                // Restore this mode's remembered source, then reload.
                var profile = Settings.Instance.GetFilterProfileForMode(mode);
                SelectByTag(SourceCombo, profile.Source);
                Reload();
            });
        }

        private void OnExploreTagRequested(string tag)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _tags = tag;
                SearchBox.Text = tag;
                Reload();
            });
        }

        private void Reload()
        {
            string source = (SourceCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "all";
            string sort = (SortCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "order:score";
            int minWidth = int.TryParse((ResCombo.SelectedItem as ComboBoxItem)?.Tag as string, out var mw) ? mw : 0;

            // Persist filter memory for the active rating mode.
            var profile = Settings.Instance.GetFilterProfileForMode(Settings.Instance.RatingMode);
            profile.Source = source;
            profile.SortTag = sort;
            Settings.Instance.Save();

            string baseTags = _tags;
            if (!string.IsNullOrEmpty(sort)) baseTags = (baseTags + " " + sort).Trim();

            _items = new IncrementalPostCollection(async (page, ct) =>
            {
                var list = await _api.FetchPostsAsync(source, baseTags, page, 24, ct);
                if (minWidth > 0) list = list.Where(p => p.Width >= minWidth).ToList();
                if (WallpaperOnlyToggle.IsChecked == true)
                    list = list.Where(IsWallpaperFriendly).ToList();
                return list;
            });
            _items.LoadingStateChanged += OnLoadingStateChanged;
            Grid.ItemsSource = _items;
            EmptyState.Visibility = Visibility.Collapsed;
        }

        private void OnLoadingStateChanged(bool loading)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                Loader.IsActive = loading;
                if (!loading)
                {
                    EmptyState.Visibility = (_items != null && _items.Count == 0) ? Visibility.Visible : Visibility.Collapsed;
                    GridLayoutHelper.UpdateLayout(Grid);
                }
            });
        }

        private void SelectByTag(ComboBox combo, string tag)
        {
            foreach (var obj in combo.Items)
                if (obj is ComboBoxItem cbi && (cbi.Tag as string) == tag) { combo.SelectedItem = cbi; return; }
        }

        private async void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            string query = sender.Text?.Trim() ?? "";
            if (query.Length == 0) { sender.ItemsSource = null; return; }

            // Instant local matches (aliases + built-in popular tags) show immediately.
            var local = _api.GetInstantLocalSuggestions(query);
            sender.ItemsSource = local;

            // Debounced remote fetch for richer results.
            _suggestCts?.Cancel();
            _suggestCts = new CancellationTokenSource();
            var ct = _suggestCts.Token;
            try
            {
                await Task.Delay(220, ct);
                string source = (SourceCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "all";
                var results = await _api.FetchTagSuggestionsAsync(source, query, ct);
                if (!ct.IsCancellationRequested && sender.Text?.Trim() == query && results.Count > 0)
                    sender.ItemsSource = results;
            }
            catch { }
        }

        private void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is TagSuggestion s)
                sender.Text = s.Name; // put the actual tag in the box, not the pretty label
        }

        private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            string tag = args.ChosenSuggestion is TagSuggestion s
                ? s.Name
                : ApiClient.ResolveTag(args.QueryText ?? "");
            _tags = tag;
            sender.ItemsSource = null;
            Reload();
        }

        private void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_initialized) Reload();
        }

        private void WallpaperOnly_Click(object sender, RoutedEventArgs e)
        {
            if (_initialized) Reload();
        }

        // Landscape, wide-enough images that suit a desktop wallpaper (>= ~3:2 ratio,
        // at least HD-ish width). Portrait / square art is filtered out.
        private static bool IsWallpaperFriendly(PostItem p)
        {
            if (p.Width <= 0 || p.Height <= 0) return true; // unknown dimensions: keep
            double ratio = (double)p.Width / p.Height;
            return ratio >= 1.5 && p.Width >= 1280;
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) => Reload();

        private bool _layoutDone;

        private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            GridLayoutHelper.UpdateLayout(Grid);
        }

        private void Grid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.ItemContainer?.ContentTemplateRoot is WallpaperCard card)
            {
                card.PreviewRequested -= OnCardPreview;
                card.PreviewRequested += OnCardPreview;
            }
            if (!_layoutDone)
            {
                _layoutDone = true;
                GridLayoutHelper.UpdateLayout(Grid);
            }
        }

        // Wire the double-tap preview through each realized card.
        private void OnCardPreview(PostItem post)
        {
            _ = PreviewDialog.ShowAsync(XamlRoot, post);
        }
    }
}
