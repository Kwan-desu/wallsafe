using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WallSafeWinUI.Controls;
using WallSafeWinUI.Services;

namespace WallSafeWinUI.Views
{
    public sealed partial class HistoryPage : Page
    {
        private readonly ObservableCollection<PostItem> _items = new();

        public HistoryPage()
        {
            InitializeComponent();
            Grid.ItemsSource = _items;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            Reload();
        }

        private void Reload()
        {
            _items.Clear();
            foreach (var p in HistoryManager.Instance.GetAllHistory()) _items.Add(p);
            EmptyState.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            HistoryManager.Instance.ClearHistory();
            Reload();
        }

        private void Grid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.ItemContainer?.ContentTemplateRoot is WallpaperCard card)
            {
                card.PreviewRequested -= OnCardPreview;
                card.PreviewRequested += OnCardPreview;
            }
        }

        private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (Grid.ItemsPanelRoot is ItemsWrapGrid wrap)
                GridLayoutHelper.FitColumns(wrap, Grid.ActualWidth - Grid.Padding.Left - Grid.Padding.Right);
        }

        private void OnCardPreview(PostItem post) => _ = PreviewDialog.ShowAsync(XamlRoot, post);
    }
}
