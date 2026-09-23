using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
using WallSafeWinUI.Controls;
using WallSafeWinUI.Services;

namespace WallSafeWinUI.Views
{
    public sealed partial class FavoritesPage : Page
    {
        private readonly ObservableCollection<PostItem> _items = new();
        private string _activeCollection = ""; // "" = all
        private readonly List<PostItem> _dragging = new();

        public FavoritesPage()
        {
            InitializeComponent();
            Grid.ItemsSource = _items;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            LoadFolders();
            LoadItems();
        }

        // ── Folders ───────────────────────────────────────────────
        private void LoadFolders()
        {
            var fm = FavoritesManager.Instance;
            var vms = new List<FolderCardVM>
            {
                new FolderCardVM
                {
                    Name = "",
                    DisplayName = "All favorites",
                    Cover = fm.GetAllFavorites().FirstOrDefault()?.PreviewUrl ?? "",
                    Count = fm.GetAllFavorites().Count
                }
            };
            foreach (var c in fm.GetCollections())
            {
                var items = fm.GetFavoritesByCollection(c);
                vms.Add(new FolderCardVM
                {
                    Name = c,
                    DisplayName = c,
                    Cover = items.FirstOrDefault()?.PreviewUrl ?? "",
                    Count = items.Count
                });
            }
            FolderList.ItemsSource = vms;
            CollectionsHeaderText.Text = $"Collections ({fm.GetCollections().Count})";
        }

        private void LoadItems()
        {
            _items.Clear();
            var list = string.IsNullOrEmpty(_activeCollection)
                ? FavoritesManager.Instance.GetAllFavorites()
                : FavoritesManager.Instance.GetFavoritesByCollection(_activeCollection);
            foreach (var p in list) _items.Add(p);

            ActiveCollectionText.Text = string.IsNullOrEmpty(_activeCollection) ? "All favorites" : _activeCollection;
            bool isNamed = !string.IsNullOrEmpty(_activeCollection);
            RenameBtn.Visibility = isNamed ? Visibility.Visible : Visibility.Collapsed;
            DeleteBtn.Visibility = isNamed ? Visibility.Visible : Visibility.Collapsed;
            PlaySlideshowText.Text = isNamed ? $"Play “{_activeCollection}” in Slideshow" : "Play in Slideshow";
            EmptyState.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void PlaySlideshow_Click(object sender, RoutedEventArgs e)
        {
            string targetSource = string.IsNullOrEmpty(_activeCollection) ? "favorites" : $"collection:{_activeCollection}";
            Settings.Instance.SlideshowSource = targetSource;
            Settings.Instance.SlideshowEnabled = true;
            Settings.Instance.Save();

            WallpaperManager.Instance.ResetSlideshowQueue();
            WallpaperManager.Instance.StartSlideshowFromSettings(advanceImmediately: true);
            App.RootWindow?.SyncSlideshowUi();
        }

        private void Folder_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is string name)
            {
                _activeCollection = name;
                LoadItems();
            }
        }

        // ── Drag & drop into a folder ─────────────────────────────
        private void Grid_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            _dragging.Clear();
            foreach (var o in e.Items)
                if (o is PostItem p) _dragging.Add(p);
            e.Data.RequestedOperation = DataPackageOperation.Move;
            e.Data.SetText("wallsafe-favorite"); // marker payload
        }

        private void Folder_DragEnter(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Move;
            if (e.DragUIOverride != null)
            {
                e.DragUIOverride.Caption = "Move to collection";
                e.DragUIOverride.IsGlyphVisible = true;
            }
            if (sender is FrameworkElement fe)
            {
                fe.Opacity = 0.75;
                if (fe is Control ctl) { /* subtle highlight via scale below */ }
                AnimateScale(fe, 1.05);
                if (fe is Panel || fe is Border || fe is Grid gg) SetHighlight(fe, true);
            }
        }

        private void Folder_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is FrameworkElement fe)
            {
                fe.Opacity = 1.0;
                AnimateScale(fe, 1.0);
                SetHighlight(fe, false);
            }
        }

        private void Folder_Drop(object sender, DragEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.Tag is not string target) return;
            fe.Opacity = 1.0;
            AnimateScale(fe, 1.0);
            SetHighlight(fe, false);

            foreach (var post in _dragging.ToList())
            {
                // Dropping on "All favorites" (empty name) un-categorizes it.
                FavoritesManager.Instance.AssignToCollection(post, target);
            }
            _dragging.Clear();

            LoadFolders();
            LoadItems();
        }

        private static void SetHighlight(FrameworkElement fe, bool on)
        {
            if (fe is Control c)
                c.BorderBrush = on
                    ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 129, 140, 248))
                    : (SolidColorBrush)Application.Current.Resources["CardStrokeColorDefaultBrush"];
            else if (fe is Border b)
                b.BorderBrush = on
                    ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 129, 140, 248))
                    : (SolidColorBrush)Application.Current.Resources["CardStrokeColorDefaultBrush"];
            else if (fe is Grid g)
                g.BorderBrush = on
                    ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 129, 140, 248))
                    : (SolidColorBrush)Application.Current.Resources["CardStrokeColorDefaultBrush"];
        }

        private static void AnimateScale(FrameworkElement fe, double scale)
        {
            fe.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
            var st = fe.RenderTransform as ScaleTransform ?? new ScaleTransform();
            var anim = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            var sx = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            { To = scale, Duration = TimeSpan.FromMilliseconds(140), EnableDependentAnimation = true };
            var sy = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            { To = scale, Duration = TimeSpan.FromMilliseconds(140), EnableDependentAnimation = true };
            fe.RenderTransform = st;
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(sx, st);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(sx, "ScaleX");
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(sy, st);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(sy, "ScaleY");
            anim.Children.Add(sx);
            anim.Children.Add(sy);
            anim.Begin();
        }

        // ── New / rename / delete ─────────────────────────────────
        private async void NewCollection_Click(object sender, RoutedEventArgs e)
        {
            var input = new TextBox { PlaceholderText = "Collection name" };
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "New collection",
                Content = input,
                PrimaryButtonText = "Create",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(input.Text))
            {
                FavoritesManager.Instance.CreateCollection(input.Text.Trim());
                LoadFolders();
            }
        }

        private async void Rename_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_activeCollection)) return;
            var input = new TextBox { Text = _activeCollection };
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Rename collection",
                Content = input,
                PrimaryButtonText = "Rename",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(input.Text))
            {
                if (FavoritesManager.Instance.RenameCollection(_activeCollection, input.Text.Trim()))
                {
                    _activeCollection = input.Text.Trim();
                    LoadFolders();
                    LoadItems();
                }
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_activeCollection)) return;
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = $"Delete “{_activeCollection}”?",
                Content = "The wallpapers stay in your favorites — only the collection folder is removed.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                FavoritesManager.Instance.DeleteCollection(_activeCollection);
                _activeCollection = "";
                LoadFolders();
                LoadItems();
            }
        }

        // ── Card wiring (preview + right-tap "move to") ───────────
        private void Grid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.ItemContainer?.ContentTemplateRoot is WallpaperCard card)
            {
                card.PreviewRequested -= OnCardPreview;
                card.PreviewRequested += OnCardPreview;
                card.RightTapped -= Card_RightTapped;
                card.RightTapped += Card_RightTapped;
            }
            if (!_layoutDone)
            {
                _layoutDone = true;
                GridLayoutHelper.UpdateLayout(Grid);
            }
        }

        private void Card_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (sender is not WallpaperCard card || card.Post == null) return;
            var post = card.Post;

            var menu = new MenuFlyout();
            var moveHeader = new MenuFlyoutItem { Text = "Move to…", IsEnabled = false };
            menu.Items.Add(moveHeader);

            var uncategorized = new MenuFlyoutItem { Text = "Unsorted (no collection)" };
            uncategorized.Click += (_, _) => { FavoritesManager.Instance.AssignToCollection(post, ""); LoadFolders(); LoadItems(); };
            menu.Items.Add(uncategorized);

            foreach (var c in FavoritesManager.Instance.GetCollections())
            {
                var mi = new MenuFlyoutItem { Text = c };
                string target = c;
                mi.Click += (_, _) => { FavoritesManager.Instance.AssignToCollection(post, target); LoadFolders(); LoadItems(); };
                menu.Items.Add(mi);
            }

            menu.Items.Add(new MenuFlyoutSeparator());
            var remove = new MenuFlyoutItem { Text = "Remove from favorites" };
            remove.Click += (_, _) =>
            {
                FavoritesManager.Instance.RemoveFavorite(post.Id, post.Source);
                LoadFolders(); LoadItems();
            };
            menu.Items.Add(remove);

            menu.ShowAt(card, e.GetPosition(card));
        }

        private void OnCardPreview(PostItem post) => _ = PreviewDialog.ShowAsync(XamlRoot, post);

        private bool _layoutDone;

        private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            GridLayoutHelper.UpdateLayout(Grid);
        }
    }
}
