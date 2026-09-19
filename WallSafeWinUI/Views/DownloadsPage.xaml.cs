using System;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WallSafeWinUI.Controls;
using WallSafeWinUI.Services;

namespace WallSafeWinUI.Views
{
    public sealed partial class DownloadsPage : Page
    {
        private readonly ObservableCollection<PostItem> _items = new();
        private bool _layoutDone;

        public DownloadsPage()
        {
            InitializeComponent();
            Grid.ItemsSource = _items;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _items.Clear();
            foreach (var p in DownloadsManager.Instance.GetAllDownloads()) _items.Add(p);
            EmptyState.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var folder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(DownloadsManager.Instance.DownloadFolder);
                await Windows.System.Launcher.LaunchFolderAsync(folder);
            }
            catch { }
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

        private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            GridLayoutHelper.UpdateLayout(Grid);
        }

        private void OnCardPreview(PostItem post) => _ = PreviewDialog.ShowAsync(XamlRoot, post);
    }
}
