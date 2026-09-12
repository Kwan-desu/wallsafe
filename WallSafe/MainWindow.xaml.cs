using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RadioButton = System.Windows.Controls.RadioButton;

namespace WallSafe
{
    public partial class MainWindow : Window
    {
        private readonly TextBlock? StatusText = null;
        private readonly System.Windows.Shapes.Ellipse? StatusDot = null;
        private readonly TextBlock? PanicButtonLabel = null;

        private enum ActiveSection { Home, Explore, Favorites, Downloaded, History }

        private ActiveSection _currentSection = ActiveSection.Home;
        private readonly ApiClient _api = new();
        private CancellationTokenSource _searchCts = new();
        private CancellationTokenSource _autocompleteCts = new();

        private string _currentSource = "yande";
        private int _currentPage = 1;
        private int _isLoading = 0; // 0=false, 1=true — use Interlocked for thread safety
        private int _pendingSearch = 0; // 0=none, 1=replace/new, 2=append
        private List<PostItem> _cachedExplorePosts = new();
        private CancellationTokenSource _toastCts = new();
        private string[]? _cachedBlacklist;
        private string _cachedBlacklistSource = string.Empty;
        private PostItem? _previewItem;
        private PostItem? _heroPost;
        private List<PostItem> _trendingPosts = new();
        private bool _showingFavoriteInHero;
        private int _favoriteHeroIndex;
        private string _currentCategoryFilter = "All";

        private readonly List<SeriesCategoryItem> _curatedCategories = new()
        {
            // Anime
            new SeriesCategoryItem { Name = "Frieren", Tag = "sousou_no_frieren", Type = "Anime", PreviewImageUrl = "https://konachan.net/sample/7aae7fc502f39f432ef4b4747554cb75/Konachan.com%20-%20378150%20sample%20ass%20barefoot%20bunnygirl%20flat_chest%20frieren%20green_eyes%20leotard%20long_hair%20pointed_ears%20sydus%20tail%20thighhighs%20twintails%20white%20white_hair.jpg" },
            new SeriesCategoryItem { Name = "Bocchi the Rock!", Tag = "bocchi_the_rock!", Type = "Anime", PreviewImageUrl = "https://konachan.net/sample/dc22b14a1a7105077fec088558b114e7/Konachan.com%20-%20351971%20sample%20bed%20blush%20bocchi_the_rock%21%20braids%20dress%20drink%20hiroi_kikuri%20hong_%28white_spider%29%20long_hair%20no_bra%20ponytail%20purple_eyes%20purple_hair%20sake.jpg" },
            new SeriesCategoryItem { Name = "Chainsaw Man", Tag = "chainsaw_man", Type = "Anime", PreviewImageUrl = "https://konachan.net/sample/fae2a5580646c25404c1eabe13417267/Konachan.com%20-%20312859%20sample%20act-age%20barefoot%20bikini%20breasts%20cleavage%20dr._stone%20genderswap%20haze_rena%20izumo_fuuko%20mashle%20nami%20nanase_umi%20one_piece%20roboco%20swimsuit%20yonagi_kei.jpg" },
            new SeriesCategoryItem { Name = "Fate / Stay Night", Tag = "fate/stay_night", Type = "Anime", PreviewImageUrl = "https://konachan.net/sample/ce16f10042f537e6c2fd2ffbfbd64476/Konachan.com%20-%20134733%20sample%20all_male%20armor%20fate_%28series%29%20fate_stay_night%20fate_zero%20lancelot_%28fate%29%20male%20maningusu%20moon%20sword%20weapon.jpg" },
            new SeriesCategoryItem { Name = "Demon Slayer", Tag = "kimetsu_no_yaiba", Type = "Anime", PreviewImageUrl = "https://konachan.net/sample/7ddf38fca9b93c517aae5cdb48e2f679/Konachan.com%20-%20294270%20sample%20bed%20black_hair%20blush%20bra%20braids%20cropped%20fang%20green_eyes%20group%20long_hair%20navel%20panties%20ponytail%20purple_eyes%20purple_hair%20thighhighs%20twintails%20underwear.jpg" },
            new SeriesCategoryItem { Name = "Evangelion", Tag = "neon_genesis_evangelion", Type = "Anime", PreviewImageUrl = "https://konachan.net/image/50cc6ca30e3b16cf7e5de024e8ddf170/Konachan.com%20-%2043209%20air%20bleach%20cc%20chii%20chobits%20clannad%20eclair%20inuyasha%20kanon%20lafiel%20lumiere%20maburaho%20mai-hime%20red_eyes%20red_hair%20saber%20shana%20suzuka%20triela%20vandread%20yin.jpg" },
            new SeriesCategoryItem { Name = "Sword Art Online", Tag = "sword_art_online", Type = "Anime", PreviewImageUrl = "https://konachan.net/jpeg/8b53b89076d5bca8ab217354f7b465c7/Konachan.com%20-%20189932%20animal_ears%20aqua_eyes%20aqua_hair%20catgirl%20gun_gale_online%20haribote_%28tarao%29%20shinon_%28sao%29%20short_hair%20shorts%20sword_art_online%20tail%20thighhighs.jpg" },
            new SeriesCategoryItem { Name = "Cyberpunk: Edgerunners", Tag = "cyberpunk:_edgerunners", Type = "Anime", PreviewImageUrl = "https://konachan.net/sample/d56c5960dfc5b4846d7507e912f57a88/Konachan.com%20-%20347721%20sample%20bed%20cyberpunk_2077%20cyberpunk%3A_edgerunners%20david_martinez%20drink%20echosdoodle%20lucy_%28cyberpunk%29%20scenic%20sky%20space%20stars.jpg" },
            new SeriesCategoryItem { Name = "Spy × Family", Tag = "spy_x_family", Type = "Anime", PreviewImageUrl = "https://konachan.net/sample/fcc14b26ad8b0d6ce2eba1c66befe9ae/Konachan.com%20-%20343631%20sample%20animal_ears%20bed%20black_hair%20blood%20bunny_ears%20bunnygirl%20flowers%20gloves%20headband%20leotard%20red_eyes%20rose%20spy_x_family%20thighhighs%20watermark%20weapon%20yor_briar.jpg" },
            new SeriesCategoryItem { Name = "Oshi no Ko", Tag = "oshi_no_ko", Type = "Anime", PreviewImageUrl = "https://konachan.net/sample/82e258119839cc879a0027ef0cf0a0c2/Konachan.com%20-%20362375%20sample%20blonde_hair%20blush%20close%20hoshino_ruby%20long_hair%20oshi_no_ko%20pink_eyes%20unnunal_%28unneonal%29.jpg" },

            // Games
            new SeriesCategoryItem { Name = "Genshin Impact", Tag = "genshin_impact", Type = "Game", PreviewImageUrl = "https://konachan.net/sample/9a25050c8424becfe96ca5e41ff0c011/Konachan.com%20-%20350272%20sample%20bed%20braids%20breasts%20cleavage%20elbow_gloves%20genshin_impact%20gloves%20long_hair%20no_bra%20ponytail%20purple_eyes%20purple_hair%20raiden_shogun%20thighhighs.jpg" },
            new SeriesCategoryItem { Name = "Honkai: Star Rail", Tag = "honkai:_star_rail", Type = "Game", PreviewImageUrl = "https://konachan.net/sample/357da1a0223398929c77a90aa0c2969c/Konachan.com%20-%20392134%20sample%20animal%20barefoot%20beach%20bikini%20blush%20bow%20breasts%20cake%20cherry%20cleavage%20crab%20drink%20food%20fruit%20garter%20headband%20ice_cream%20long_hair%20navel%20shade%20swimsuit.jpg" },
            new SeriesCategoryItem { Name = "Blue Archive", Tag = "blue_archive", Type = "Game", PreviewImageUrl = "https://konachan.net/sample/20e33fdd7b39b2a0f865ba9961320e05/Konachan.com%20-%20345572%20sample%202girls%20bikini%20black_hair%20blue_eyes%20breasts%20choney%20cleavage%20clouds%20dark_skin%20flowers%20halo%20navel%20ponytail%20sky%20swimsuit%20water%20watermark%20wet%20wristwear.jpg" },
            new SeriesCategoryItem { Name = "NieR", Tag = "nier", Type = "Game", PreviewImageUrl = "https://konachan.net/sample/e2213d976de343bd9f8daf381fdac4f9/Konachan.com%20-%20280312%20sample%20aqua_eyes%20long_hair%20nier%20nier%3A_automata%20sword%20weapon%20white_hair%20wlop%20yorha_unit_no._2_type_a.jpg" },
            new SeriesCategoryItem { Name = "Arknights", Tag = "arknights", Type = "Game", PreviewImageUrl = "https://konachan.net/sample/0edc3567801aee61a46fdb09874c5da7/Konachan.com%20-%20339228%20sample%20angelina_%28arknights%29%20animal_ears%20arknights%20bed%20brown_hair%20cameltoe%20foxgirl%20long_hair%20red_eyes%20snm_%28sunimi%29%20swimsuit%20tail%20wristwear.jpg" },
            new SeriesCategoryItem { Name = "Fate / Grand Order", Tag = "fate/grand_order", Type = "Game", PreviewImageUrl = "https://konachan.net/sample/940a16ce3036096f520d5f720138ab24/Konachan.com%20-%20292156%20sample%202girls%20ass%20barefoot%20beach%20bikini%20blonde_hair%20fate_grand_order%20fate_%28series%29%20purple_hair%20red_eyes%20ribbons%20sunglasses%20swimsuit%20yang-do%20yellow_eyes.jpg" },
            new SeriesCategoryItem { Name = "Elden Ring", Tag = "elden_ring", Type = "Game", PreviewImageUrl = "https://konachan.net/sample/8aeea62208846a994f15a495d412bbdf/Konachan.com%20-%20343821%20sample%20doll%20dress%20elden_ring%20hat%20liu_liaoliao%20ranni_the_witch%20witch%20witch_hat.jpg" },
            new SeriesCategoryItem { Name = "Touhou Project", Tag = "touhou", Type = "Game", PreviewImageUrl = "https://konachan.net/sample/f96fe01d6cd8d35b9f6c87b0829bb13f/Konachan.com%20-%20204334%20sample%20ass%20beach%20bikini%20blush%20breast_hold%20clouds%20ke-ta%20pink_hair%20saigyouji_yuyuko%20scan%20short_hair%20sky%20swimsuit%20touhou%20water.jpg" },
            new SeriesCategoryItem { Name = "Hololive", Tag = "hololive", Type = "Game", PreviewImageUrl = "https://konachan.net/sample/8b8710694380612cf5653a861930f7d7/Konachan.com%20-%20299750%20sample%20bed%20breast_hold%20breasts%20cleavage%20hololive%20long_hair%20minato_aqua%20panties%20purple_eyes%20purple_hair%20scan%20thighhighs%20third-party_edit%20twintails%20underwear.jpg" }
        };

        public MainWindow()
        {
            InitializeComponent();
            PositionNearTray();
            UpdatePanicButtonLabel();
            UpdateSlideshowQuickToggleState();
            UpdateHotkeyRegistrationStatus(HotkeyManager.Instance.IsRegistered);

            FavoritesManager.Instance.FavoritesChanged += OnFavoritesChanged;
            DownloadsManager.Instance.DownloadsChanged += OnDownloadsChanged;
            HistoryManager.Instance.HistoryChanged += OnHistoryChanged;
            WallpaperManager.Instance.SafeWallpaperTriggered += OnSafeWallpaperTriggered;
            WallpaperManager.Instance.SafeWallpaperRestored += OnSafeWallpaperRestored;
            HotkeyManager.Instance.RegistrationChanged += OnHotkeyRegistrationChanged;
            ThemeManager.ThemeChanged += _ => Dispatcher.Invoke(() =>
            {
                UpdateThemeIcon();
                // Keep the DWM title-bar + window backdrop in sync with the new theme.
                MicaHelper.SyncCaptionTheme(this);
                ApplyWindowBackdrop();
                // Force chrome surfaces to follow the theme (guards against any stale brush).
                if (TopBarBorder != null)
                    TopBarBorder.Background = (System.Windows.Media.Brush)FindResource("SurfaceBrush");
            });
            UpdateThemeIcon();
            UpdateSfwToggleVisuals();
            ApplySfwModeToFilters();
            ApplyHomeSectionsVisibility();

            Loaded += async (_, _) =>
            {
                InitializeCategories();
                _ = RefreshCategoriesForCurrentSourceAsync();
                await LoadHomeDataAsync();
                _ = DoExploreSearch(append: false);
            };

            KeyDown += (s, e) =>
            {
                if (PreviewModal.Visibility == Visibility.Visible)
                {
                    if (e.Key == Key.Escape)
                    {
                        ClosePreview_Click(s, e);
                        e.Handled = true;
                        return;
                    }
                    else if (e.Key == Key.Enter)
                    {
                        PreviewApply_Click(s, e);
                        e.Handled = true;
                        return;
                    }
                }

                if (e.Key == Key.F5)
                {
                    Refresh_Click(s, e);
                    e.Handled = true;
                }
                else if (e.Key == Key.F && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    TagSearchBox.Focus();
                    TagSearchBox.SelectAll();
                    e.Handled = true;
                }
            };
        }

        private void OnHotkeyRegistrationChanged(bool isRegistered)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateHotkeyRegistrationStatus(isRegistered);
            });
        }

        private void UpdateHotkeyRegistrationStatus(bool registered)
        {
            if (StatusDot == null) return;
            if (registered)
            {
                StatusDot.Fill = (System.Windows.Media.Brush)FindResource("EmeraldBrush");
                StatusDot.ToolTip = "WallSafe Engine Ready • Hotkey Active";
            }
            else
            {
                StatusDot.Fill = (System.Windows.Media.Brush)FindResource("DangerBrush");
                StatusDot.ToolTip = "Hotkey Conflict! The configured shortcut is already in use by another app.";
            }
        }

        public void UpdatePanicButtonLabel()
        {
            string hotkey = Settings.Instance.GetHotkeyDisplayName();
            if (PanicButtonLabel != null) PanicButtonLabel.Text = $"SAFE  {hotkey}";
            if (HomePanicKeyText != null) HomePanicKeyText.Text = hotkey;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var source = System.Windows.Interop.HwndSource.FromHwnd(handle);
            source?.AddHook(WndProc);

            // Apply the opt-in Windows 11 Mica backdrop (no-op on older Windows).
            ApplyWindowBackdrop();
            // Rounded corners via DWM (Windows 11).
            MicaHelper.EnableRoundedCorners(this);
        }

        private const int WM_GETMINMAXINFO = 0x0024;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flag);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // Constrain a maximized WindowStyle=None window to the monitor work area so it
            // fills the screen exactly (without covering the taskbar or overflowing edges).
            if (msg == WM_GETMINMAXINFO)
            {
                const int MONITOR_DEFAULTTONEAREST = 0x00000002;
                IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                if (monitor != IntPtr.Zero)
                {
                    var mi = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(MONITORINFO)) };
                    if (GetMonitorInfo(monitor, ref mi))
                    {
                        var mmi = System.Runtime.InteropServices.Marshal.PtrToStructure<MINMAXINFO>(lParam);
                        mmi.ptMaxPosition.X = mi.rcWork.Left - mi.rcMonitor.Left;
                        mmi.ptMaxPosition.Y = mi.rcWork.Top - mi.rcMonitor.Top;
                        mmi.ptMaxSize.X = mi.rcWork.Right - mi.rcWork.Left;
                        mmi.ptMaxSize.Y = mi.rcWork.Bottom - mi.rcWork.Top;
                        mmi.ptMinTrackSize.X = (int)MinWidth;
                        mmi.ptMinTrackSize.Y = (int)MinHeight;
                        System.Runtime.InteropServices.Marshal.StructureToPtr(mmi, lParam, true);
                        handled = true;
                    }
                }
            }
            return IntPtr.Zero;
        }

        /// <summary>Apply or remove the Mica backdrop based on the user setting. When Mica is
        /// active the window background is made transparent so the DWM backdrop shows; the
        /// wallpaper grid area itself remains opaque for thumbnail legibility.</summary>
        public void ApplyWindowBackdrop()
        {
            bool want = Settings.Instance.WindowMicaEnabled && MicaHelper.IsMicaSupported;
            bool applied = MicaHelper.Apply(this, want);
            if (applied)
            {
                // Let the Mica backdrop show through the window + root border.
                Background = System.Windows.Media.Brushes.Transparent;
                if (RootBorder != null)
                    RootBorder.Background = System.Windows.Media.Brushes.Transparent;
            }
            else
            {
                // Re-bind to the themed brush via a resource reference so it keeps
                // following Dark/Light switches (a static snapshot would freeze the color).
                SetResourceReference(BackgroundProperty, "BgBrush");
                if (RootBorder != null)
                    RootBorder.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "BgBrush");
            }
        }

        /// <summary>Re-apply grid card density (called from Settings when the density changes).</summary>
        public void RefreshGridDensity()
        {
            try { WallpaperGridControl?.RefreshDensity(); } catch { }
        }

        private void RootBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // WindowChrome + DWM round the window corners; no manual corner clip required.
        }

        private void SlideshowQuickToggle_Click(object sender, RoutedEventArgs e)
        {
            Settings.Instance.SlideshowEnabled = !Settings.Instance.SlideshowEnabled;
            Settings.Instance.Save();
            if (Settings.Instance.SlideshowEnabled)
            {
                WallpaperManager.Instance.StartSlideshow(Settings.Instance.SlideshowIntervalMinutes, advanceImmediately: true);
                ShowToast("▶", "Slideshow Started");
            }
            else
            {
                WallpaperManager.Instance.StopSlideshow();
                ShowToast("⏸", "Slideshow Paused");
            }
            UpdateSlideshowQuickToggleState();
        }

        private void ThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.ToggleTheme();
            UpdateThemeIcon();
            ShowToast("✓", ThemeManager.IsDark ? "Dark Mode Enabled" : "Light Mode Enabled");
        }

        private void UpdateThemeIcon()
        {
            if (ThemeIconPath == null || ThemeToggleBtn == null) return;
            bool isDark = ThemeManager.IsDark;
            if (isDark)
            {
                ThemeIconPath.Data = (System.Windows.Media.Geometry)FindResource("IconSunGeo");
                ThemeToggleBtn.ToolTip = "Switch to Light Mode";
            }
            else
            {
                ThemeIconPath.Data = (System.Windows.Media.Geometry)FindResource("IconMoonGeo");
                ThemeToggleBtn.ToolTip = "Switch to Dark Mode";
            }
        }

        private void UpdateSlideshowQuickToggleState()
        {
            if (SlideshowQuickToggle == null || SlideshowIconPath == null) return;
            bool isEnabled = Settings.Instance.SlideshowEnabled;
            if (isEnabled)
            {
                SlideshowIconPath.Data = (System.Windows.Media.Geometry)FindResource("IconPauseGeo");
                SlideshowIconPath.Fill = (System.Windows.Media.Brush)FindResource("AccentHoverBrush");
                SlideshowQuickToggle.ToolTip = "Automated Slideshow Active (Click to Pause)";
                if (SlideshowActiveDot != null)
                    SlideshowActiveDot.Visibility = Visibility.Visible;
                if (HomeSlideshowStatusText != null)
                    HomeSlideshowStatusText.Text = $"Active ({Settings.Instance.SlideshowIntervalMinutes}m interval)";
                if (HomeSlideshowBtnText != null)
                    HomeSlideshowBtnText.Text = "Pause";
                if (HomeSlideshowBtnIcon != null)
                    HomeSlideshowBtnIcon.Data = (System.Windows.Media.Geometry)FindResource("IconPauseGeo");
            }
            else
            {
                SlideshowIconPath.Data = (System.Windows.Media.Geometry)FindResource("IconPlayGeo");
                SlideshowIconPath.Fill = (System.Windows.Media.Brush)FindResource("SubtextBrush");
                SlideshowQuickToggle.ToolTip = "Automated Slideshow Paused (Click to Start)";
                if (SlideshowActiveDot != null)
                    SlideshowActiveDot.Visibility = Visibility.Collapsed;
                if (HomeSlideshowStatusText != null)
                    HomeSlideshowStatusText.Text = $"Paused ({Settings.Instance.SlideshowIntervalMinutes}m interval)";
                if (HomeSlideshowBtnText != null)
                    HomeSlideshowBtnText.Text = "Start";
                if (HomeSlideshowBtnIcon != null)
                    HomeSlideshowBtnIcon.Data = (System.Windows.Media.Geometry)FindResource("IconPlayGeo");
            }
        }

        private void OnSafeWallpaperRestored()
        {
            ShowToast("✓", "Previous Wallpaper Restored");
        }

        private void PositionNearTray()
        {
            var screen = SystemParameters.WorkArea;
            Left = Math.Max(10, (screen.Width - Width) / 2);
            Top = Math.Max(10, (screen.Height - Height) / 2);
        }

        // ═════════════════════════════════════════════════════════════════
        // Section Navigation
        // ═════════════════════════════════════════════════════════════════

        private void NavTab_Checked(object sender, RoutedEventArgs e)
        {
            if (NavHome == null || NavExplore == null || NavFavorites == null || NavDownloads == null || NavHistory == null) return;

            if (NavHome.IsChecked == true)
            {
                _currentSection = ActiveSection.Home;
                HomeView.Visibility = Visibility.Visible;
                ExploreResultsContainer.Visibility = Visibility.Collapsed;
                ExploreToolbar.Visibility = Visibility.Collapsed;
                FilterDrawer.Visibility = Visibility.Collapsed;
                if (HistoryToolbar != null) HistoryToolbar.Visibility = Visibility.Collapsed;
                EmptyState.Visibility = Visibility.Collapsed;
                DisplayCurrentSection();
            }
            else if (NavExplore.IsChecked == true)
            {
                _currentSection = ActiveSection.Explore;
                HomeView.Visibility = Visibility.Collapsed;
                ExploreResultsContainer.Visibility = Visibility.Visible;
                ExploreToolbar.Visibility = Visibility.Visible;
                FilterDrawer.Visibility = FilterDrawerToggle.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                if (HistoryToolbar != null) HistoryToolbar.Visibility = Visibility.Collapsed;
                DisplayCurrentSection();
            }
            else if (NavFavorites.IsChecked == true)
            {
                _currentSection = ActiveSection.Favorites;
                HomeView.Visibility = Visibility.Collapsed;
                ExploreResultsContainer.Visibility = Visibility.Visible;
                ExploreToolbar.Visibility = Visibility.Collapsed;
                FilterDrawer.Visibility = Visibility.Collapsed;
                if (HistoryToolbar != null) HistoryToolbar.Visibility = Visibility.Collapsed;
                DisplayCurrentSection();
            }
            else if (NavDownloads.IsChecked == true)
            {
                _currentSection = ActiveSection.Downloaded;
                HomeView.Visibility = Visibility.Collapsed;
                ExploreResultsContainer.Visibility = Visibility.Visible;
                ExploreToolbar.Visibility = Visibility.Collapsed;
                FilterDrawer.Visibility = Visibility.Collapsed;
                if (HistoryToolbar != null) HistoryToolbar.Visibility = Visibility.Collapsed;
                DisplayCurrentSection();
            }
            else if (NavHistory.IsChecked == true)
            {
                _currentSection = ActiveSection.History;
                HomeView.Visibility = Visibility.Collapsed;
                ExploreResultsContainer.Visibility = Visibility.Visible;
                ExploreToolbar.Visibility = Visibility.Collapsed;
                FilterDrawer.Visibility = Visibility.Collapsed;
                if (HistoryToolbar != null) HistoryToolbar.Visibility = Visibility.Visible;
                DisplayCurrentSection();
            }

            // The filter chips are only meaningful in Explore.
            bool explore = _currentSection == ActiveSection.Explore;
            if (ExploreChipsRow != null) ExploreChipsRow.Visibility = explore ? Visibility.Visible : Visibility.Collapsed;
            if (explore) RebuildFilterChips();

            // The collection bar is only meaningful in Favorites.
            bool favorites = _currentSection == ActiveSection.Favorites;
            if (FavoritesCollectionBar != null) FavoritesCollectionBar.Visibility = favorites ? Visibility.Visible : Visibility.Collapsed;
            if (favorites) RebuildCollectionChips();
        }

        private void DisplayCurrentSection()
        {
            switch (_currentSection)
            {
                case ActiveSection.Home:
                    UpdateQuickShelf();
                    if (StatusText != null) StatusText.Text = "WallSafe Engine Ready  •  Global Panic Hotkey Active";
                    break;

                case ActiveSection.Explore:
                    WallpaperGridControl.IsInCollectionFolder = false;
                    WallpaperGridControl.SetPosts(_cachedExplorePosts, canLoadMore: _cachedExplorePosts.Count >= 10);
                    CheckEmptyState(_cachedExplorePosts.Count, "No wallpapers found", "Try adjusting tags or content filters.");
                    if (StatusText != null) StatusText.Text = $"{_cachedExplorePosts.Count} wallpapers online  •  Page {_currentPage}";
                    break;

                case ActiveSection.Favorites:
                    // "\u0001ALL" = every favorite; "" = Unsorted (uncategorized); else a specific collection.
                    bool isFolder = !string.IsNullOrEmpty(_selectedCollection) && _selectedCollection != "\u0001ALL";
                    WallpaperGridControl.IsInCollectionFolder = isFolder;
                    List<PostItem> favs;
                    if (_selectedCollection == "\u0001ALL")
                        favs = FavoritesManager.Instance.GetAllFavorites();
                    else if (string.IsNullOrEmpty(_selectedCollection))
                        favs = FavoritesManager.Instance.GetUncategorizedFavorites();
                    else
                        favs = FavoritesManager.Instance.GetFavoritesByCollection(_selectedCollection);
                    WallpaperGridControl.SetPosts(favs, canLoadMore: false);
                    CheckEmptyState(favs.Count,
                        _selectedCollection == "\u0001ALL" ? "No Favorites Yet"
                            : string.IsNullOrEmpty(_selectedCollection) ? "No unsorted favorites"
                            : $"No wallpapers in \u201c{_selectedCollection}\u201d",
                        _selectedCollection == "\u0001ALL" ? "Click the \u2665 heart on any wallpaper to save it here."
                            : string.IsNullOrEmpty(_selectedCollection)
                                ? "Favorites you haven't filed yet appear here. Drag them onto a folder above to organize."
                                : "Drag wallpapers onto this folder to add them here.");
                    if (StatusText != null) StatusText.Text = $"{favs.Count} favorite wallpapers";
                    break;

                case ActiveSection.Downloaded:
                    WallpaperGridControl.IsInCollectionFolder = false;
                    var downs = DownloadsManager.Instance.GetAllDownloads();
                    WallpaperGridControl.SetPosts(downs, canLoadMore: false);
                    CheckEmptyState(downs.Count, "No Downloaded Wallpapers", "Click 📥 on any wallpaper to download original high-res files.");
                    if (StatusText != null) StatusText.Text = $"{downs.Count} offline wallpapers";
                    break;

                case ActiveSection.History:
                    WallpaperGridControl.IsInCollectionFolder = false;
                    var hist = HistoryManager.Instance.GetAllHistory();
                    WallpaperGridControl.SetPosts(hist, canLoadMore: false);
                    if (HistoryCountBadgeText != null)
                        HistoryCountBadgeText.Text = $"{hist.Count} wallpaper{(hist.Count == 1 ? "" : "s")}";
                    CheckEmptyState(hist.Count, "No Wallpaper History Yet", "Wallpapers you apply to your desktop will automatically appear here.");
                    if (StatusText != null) StatusText.Text = $"{hist.Count} applied wallpaper{(hist.Count == 1 ? "" : "s")} in history";
                    break;
            }
        }

        private void CheckEmptyState(int count, string title, string subtitle)
        {
            if (count == 0)
            {
                EmptyTitleText.Text = title;
                EmptySubtitleText.Text = subtitle;
                EmptyState.Visibility = Visibility.Visible;
            }
            else
            {
                EmptyState.Visibility = Visibility.Collapsed;
            }
        }

        private void OnFavoritesChanged()
        {
            Dispatcher.Invoke(() =>
            {
                if (_currentSection == ActiveSection.Favorites)
                    DisplayCurrentSection();
                else if (_currentSection == ActiveSection.Home)
                    UpdateQuickShelf();
            });
        }

        private void OnDownloadsChanged()
        {
            Dispatcher.Invoke(() =>
            {
                if (_currentSection == ActiveSection.Downloaded)
                    DisplayCurrentSection();
                else if (_currentSection == ActiveSection.Home)
                    UpdateQuickShelf();
            });
        }

        private void OnHistoryChanged()
        {
            Dispatcher.Invoke(() =>
            {
                if (_currentSection == ActiveSection.History)
                    DisplayCurrentSection();
            });
        }

        private void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            var snapshot = HistoryManager.Instance.GetAllHistory();
            if (snapshot.Count == 0)
            {
                ShowToast("✓", "History is already empty");
                return;
            }

            HistoryManager.Instance.ClearHistory();
            if (_currentSection == ActiveSection.History)
            {
                DisplayCurrentSection();
            }

            // Offer a one-tap Undo instead of a blocking confirmation dialog.
            ShowToast("✓", $"Cleared {snapshot.Count} history item(s)", () =>
            {
                HistoryManager.Instance.RestoreHistory(snapshot);
                if (_currentSection == ActiveSection.History)
                {
                    DisplayCurrentSection();
                }
                ShowToast("✓", "History restored");
            });
        }

        // ═════════════════════════════════════════════════════════════════
        // Source & Filters
        // ═════════════════════════════════════════════════════════════════

        private bool _isUpdatingFilters = false;
        private bool _isApplyingFilterProfile = false;

        private void Source_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingFilters) return;
            if (SourceAll == null || SourceKonaSfw == null || SourceYande == null || SourceKonaNsfw == null) return;

            if (SourceAll.IsChecked == true) _currentSource = "all";
            else if (SourceKonaSfw.IsChecked == true) _currentSource = "konasfw";
            else if (SourceZerochan?.IsChecked == true) _currentSource = "zerochan";
            else if (SourceSafebooru?.IsChecked == true) _currentSource = "safebooru";
            else if (SourceWaifuIm?.IsChecked == true) _currentSource = "waifuim";
            else if (SourceYande.IsChecked == true) _currentSource = "yande";
            else if (SourceGelbooru?.IsChecked == true) _currentSource = "gelbooru";
            else if (SourceDanbooru?.IsChecked == true) _currentSource = "danbooru";
            else if (SourceKonaNsfw.IsChecked == true) _currentSource = "konansfw";
            else if (sender is RadioButton rb && rb.Tag is string customId) _currentSource = customId;

            _currentPage = 1;
            SaveCurrentFilterProfile();
            if (IsLoaded)
            {
                _ = RefreshCategoriesForCurrentSourceAsync();
            }
            if (_currentSection == ActiveSection.Explore && IsLoaded)
            {
                _ = DoExploreSearch(append: false);
            }
        }

        public void SfwToggle_Click(object sender, RoutedEventArgs e)
        {
            // Legacy cycle entry point (kept for compatibility) — advance one mode.
            bool allowCustom = Settings.Instance.CustomFilterProfileEnabled;
            var nextMode = Settings.Instance.RatingMode switch
            {
                ContentRatingMode.SfwOnly => ContentRatingMode.Questionable,
                ContentRatingMode.Questionable => ContentRatingMode.Explicit,
                ContentRatingMode.Explicit => allowCustom ? ContentRatingMode.Custom : ContentRatingMode.SfwOnly,
                ContentRatingMode.Custom => ContentRatingMode.SfwOnly,
                _ => ContentRatingMode.SfwOnly
            };
            SetRatingMode(nextMode);
        }

        private bool _isUpdatingRatingSegments;

        /// <summary>Handler for the segmented rating control (SFW | Questionable | Explicit).</summary>
        private void RatingSegment_Checked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingRatingSegments) return;
            if (sender is not RadioButton rb || rb.Tag is not string tag || !int.TryParse(tag, out int modeVal)) return;
            SetRatingMode((ContentRatingMode)modeVal);
        }

        /// <summary>Set the content rating mode directly (no cycling).</summary>
        private void SetRatingMode(ContentRatingMode mode)
        {
            Settings.Instance.RatingMode = mode;
            Settings.Instance.Save();

            UpdateSfwToggleVisuals();
            ApplySfwModeToFilters();

            string toastIcon = mode switch
            {
                ContentRatingMode.SfwOnly => "🛡",
                ContentRatingMode.Questionable => "⚠️",
                ContentRatingMode.Explicit => "🔞",
                _ => "⚙️"
            };
            string toastMsg = mode switch
            {
                ContentRatingMode.SfwOnly => "Safe Mode: SFW Content Only (konachan.net)",
                ContentRatingMode.Questionable => "Questionable Mode: Mild / Suggestive / Ecchi Content",
                ContentRatingMode.Explicit => "Explicit Mode: Unrestricted 18+ NSFW Content Included",
                _ => "Custom Profile Mode: All filters unlocked & automatically saved"
            };
            ShowToast(toastIcon, toastMsg);

            if (IsLoaded)
            {
                _ = RefreshCategoriesForCurrentSourceAsync();
            }

            if (_currentSection == ActiveSection.Explore && IsLoaded)
            {
                _currentPage = 1;
                _ = DoExploreSearch(append: false);
            }
            else if (_currentSection == ActiveSection.Home && IsLoaded)
            {
                _ = LoadHomeDataAsync();
            }
        }

        public void UpdateSfwToggleVisuals()
        {
            if (RatingSegSfw == null || RatingSegQ == null || RatingSegE == null) return;

            var mode = Settings.Instance.RatingMode;
            bool customEnabled = Settings.Instance.CustomFilterProfileEnabled;

            _isUpdatingRatingSegments = true;
            try
            {
                // Show the Custom segment only when the custom profile feature is enabled.
                if (RatingSegCustom != null)
                    RatingSegCustom.Visibility = customEnabled ? Visibility.Visible : Visibility.Collapsed;

                RatingSegSfw.IsChecked = mode == ContentRatingMode.SfwOnly;
                RatingSegQ.IsChecked = mode == ContentRatingMode.Questionable;
                RatingSegE.IsChecked = mode == ContentRatingMode.Explicit;
                if (RatingSegCustom != null)
                    RatingSegCustom.IsChecked = mode == ContentRatingMode.Custom;

                // If custom mode is active but the feature was disabled, fall back to SFW selection visually.
                if (mode == ContentRatingMode.Custom && !customEnabled)
                    RatingSegSfw.IsChecked = true;
            }
            finally
            {
                _isUpdatingRatingSegments = false;
            }
        }

        public void ApplySfwModeToFilters()
        {
            if (SourceKonaNsfw == null || SourceYande == null || SourceKonaSfw == null) return;

            if (!Settings.Instance.CustomFilterProfileEnabled && Settings.Instance.RatingMode == ContentRatingMode.Custom)
            {
                Settings.Instance.RatingMode = ContentRatingMode.SfwOnly;
                Settings.Instance.Save();
            }

            _isUpdatingFilters = true;
            try
            {
                var mode = Settings.Instance.RatingMode;
                bool allowAll = Settings.Instance.AllowAllSources;

                // Honor the "All Sources" setting: show/hide the aggregation pill.
                if (SourceAll != null)
                    SourceAll.Visibility = allowAll ? Visibility.Visible : Visibility.Collapsed;

                // When "All Sources" aggregation is enabled, it becomes the default active
                // source whenever the rating mode is (re)applied. Users can still click a
                // single source pill afterward to narrow the results for that session.
                bool keepAll = allowAll;

                if (mode == ContentRatingMode.SfwOnly)
                {
                    SourceKonaNsfw.Visibility = Visibility.Collapsed;
                    SourceYande.Visibility = Visibility.Collapsed;
                    if (SourceGelbooru != null) SourceGelbooru.Visibility = Visibility.Collapsed;
                    if (SourceDanbooru != null) SourceDanbooru.Visibility = Visibility.Collapsed;

                    SourceKonaSfw.Visibility = Visibility.Visible;
                    if (SourceZerochan != null) SourceZerochan.Visibility = Visibility.Visible;
                    if (SourceSafebooru != null) SourceSafebooru.Visibility = Visibility.Visible;
                    if (SourceWaifuIm != null) SourceWaifuIm.Visibility = Visibility.Visible;

                    if (RatingBox != null)
                    {
                        RatingBox.SelectedIndex = 0; // Safe Only (SFW)
                        RatingBox.IsEnabled = false;
                        RatingBox.ToolTip = "Rating restricted strictly to SFW. (Switch to Custom mode to override).";
                    }

                    if (keepAll && SourceAll != null) { SourceAll.IsChecked = true; _currentSource = "all"; }
                    else { SourceKonaSfw.IsChecked = true; _currentSource = "konasfw"; }
                }
                else if (mode == ContentRatingMode.Questionable)
                {
                    SourceKonaNsfw.Visibility = Visibility.Visible;
                    SourceYande.Visibility = Visibility.Visible;
                    if (SourceGelbooru != null) SourceGelbooru.Visibility = Visibility.Visible;
                    if (SourceDanbooru != null) SourceDanbooru.Visibility = Visibility.Visible;

                    SourceKonaSfw.Visibility = Visibility.Visible;
                    if (SourceZerochan != null) SourceZerochan.Visibility = Visibility.Visible;
                    if (SourceSafebooru != null) SourceSafebooru.Visibility = Visibility.Visible;
                    if (SourceWaifuIm != null) SourceWaifuIm.Visibility = Visibility.Visible;

                    if (RatingBox != null)
                    {
                        RatingBox.SelectedIndex = 1; // Questionable
                        RatingBox.IsEnabled = false;
                        RatingBox.ToolTip = "Rating locked to Questionable. (Switch to Custom mode to override).";
                    }

                    if (keepAll && SourceAll != null) { SourceAll.IsChecked = true; _currentSource = "all"; }
                    else { SourceYande.IsChecked = true; _currentSource = "yande"; }
                }
                else if (mode == ContentRatingMode.Explicit)
                {
                    SourceKonaNsfw.Visibility = Visibility.Visible;
                    SourceYande.Visibility = Visibility.Visible;
                    if (SourceGelbooru != null) SourceGelbooru.Visibility = Visibility.Visible;
                    if (SourceDanbooru != null) SourceDanbooru.Visibility = Visibility.Visible;

                    SourceKonaSfw.Visibility = Visibility.Visible;
                    if (SourceZerochan != null) SourceZerochan.Visibility = Visibility.Visible;
                    if (SourceSafebooru != null) SourceSafebooru.Visibility = Visibility.Visible;
                    if (SourceWaifuIm != null) SourceWaifuIm.Visibility = Visibility.Visible;

                    if (RatingBox != null)
                    {
                        RatingBox.SelectedIndex = 2; // Explicit (NSFW)
                        RatingBox.IsEnabled = false;
                        RatingBox.ToolTip = "Rating locked to Explicit (NSFW). (Switch to Custom mode to override).";
                    }

                    if (keepAll && SourceAll != null) { SourceAll.IsChecked = true; _currentSource = "all"; }
                    else { SourceYande.IsChecked = true; _currentSource = "yande"; }
                }
                else // ContentRatingMode.Custom
                {
                    SourceKonaNsfw.Visibility = Visibility.Visible;
                    SourceYande.Visibility = Visibility.Visible;
                    if (SourceGelbooru != null) SourceGelbooru.Visibility = Visibility.Visible;
                    if (SourceDanbooru != null) SourceDanbooru.Visibility = Visibility.Visible;

                    SourceKonaSfw.Visibility = Visibility.Visible;
                    if (SourceZerochan != null) SourceZerochan.Visibility = Visibility.Visible;
                    if (SourceSafebooru != null) SourceSafebooru.Visibility = Visibility.Visible;
                    if (SourceWaifuIm != null) SourceWaifuIm.Visibility = Visibility.Visible;

                    if (RatingBox != null)
                    {
                        RatingBox.IsEnabled = true;
                        RatingBox.ToolTip = "Custom Profile Active: Content Rating is freely customizable and persists across sessions.";
                    }

                    ApplySavedFilterProfileToUi();
                }

                RebuildCustomSourcePills();
            }
            finally
            {
                _isUpdatingFilters = false;
            }
        }

        public void ApplySavedFilterProfileToUi()
        {
            var prof = Settings.Instance.SavedFilterProfile;
            if (prof == null) return;

            _isApplyingFilterProfile = true;
            try
            {
                if (RatingBox != null && !string.IsNullOrEmpty(prof.RatingTag))
                {
                    for (int i = 0; i < RatingBox.Items.Count; i++)
                    {
                        if (RatingBox.Items[i] is ComboBoxItem cbi && (cbi.Tag as string) == prof.RatingTag)
                        {
                            RatingBox.SelectedIndex = i;
                            break;
                        }
                    }
                }
                if (SortBox != null && !string.IsNullOrEmpty(prof.SortTag))
                {
                    for (int i = 0; i < SortBox.Items.Count; i++)
                    {
                        if (SortBox.Items[i] is ComboBoxItem cbi && (cbi.Tag as string) == prof.SortTag)
                        {
                            SortBox.SelectedIndex = i;
                            break;
                        }
                    }
                }
                if (ResolutionBox != null && prof.ResolutionIndex >= 0 && prof.ResolutionIndex < ResolutionBox.Items.Count)
                {
                    ResolutionBox.SelectedIndex = prof.ResolutionIndex;
                }
                if (AspectBox != null && prof.AspectIndex >= 0 && prof.AspectIndex < AspectBox.Items.Count)
                {
                    AspectBox.SelectedIndex = prof.AspectIndex;
                }

                if (!string.IsNullOrEmpty(prof.Source))
                {
                    if (prof.Source == "all" && SourceAll != null) { SourceAll.IsChecked = true; _currentSource = "all"; }
                    else if (prof.Source == "konasfw" && SourceKonaSfw != null) { SourceKonaSfw.IsChecked = true; _currentSource = "konasfw"; }
                    else if (prof.Source == "zerochan" && SourceZerochan != null) { SourceZerochan.IsChecked = true; _currentSource = "zerochan"; }
                    else if (prof.Source == "safebooru" && SourceSafebooru != null) { SourceSafebooru.IsChecked = true; _currentSource = "safebooru"; }
                    else if (prof.Source == "waifuim" && SourceWaifuIm != null) { SourceWaifuIm.IsChecked = true; _currentSource = "waifuim"; }
                    else if (prof.Source == "yande" && SourceYande != null) { SourceYande.IsChecked = true; _currentSource = "yande"; }
                    else if (prof.Source == "gelbooru" && SourceGelbooru != null) { SourceGelbooru.IsChecked = true; _currentSource = "gelbooru"; }
                    else if (prof.Source == "danbooru" && SourceDanbooru != null) { SourceDanbooru.IsChecked = true; _currentSource = "danbooru"; }
                    else if (prof.Source == "konansfw" && SourceKonaNsfw != null) { SourceKonaNsfw.IsChecked = true; _currentSource = "konansfw"; }
                }
            }
            finally
            {
                _isApplyingFilterProfile = false;
            }
        }

        public void SaveCurrentFilterProfile()
        {
            if (_isUpdatingFilters || _isApplyingFilterProfile || Settings.Instance.RatingMode != ContentRatingMode.Custom) return;

            var prof = Settings.Instance.SavedFilterProfile ??= new FilterProfile();
            prof.RatingTag = ((ComboBoxItem?)RatingBox?.SelectedItem)?.Tag as string ?? "rating:s";
            prof.SortTag = ((ComboBoxItem?)SortBox?.SelectedItem)?.Tag as string ?? "order:score";
            prof.ResolutionIndex = ResolutionBox?.SelectedIndex ?? 0;
            prof.AspectIndex = AspectBox?.SelectedIndex ?? 0;
            prof.Source = _currentSource;

            Settings.Instance.Save();
        }

        public void RebuildCustomSourcePills()
        {
            if (SourcePillsPanel == null) return;

            var toRemove = SourcePillsPanel.Children.OfType<RadioButton>()
                .Where(rb => rb != SourceAll &&
                             rb != SourceKonaSfw &&
                             rb != SourceZerochan &&
                             rb != SourceSafebooru &&
                             rb != SourceWaifuIm &&
                             rb != SourceYande &&
                             rb != SourceGelbooru &&
                             rb != SourceDanbooru &&
                             rb != SourceKonaNsfw)
                .ToList();

            foreach (var rb in toRemove)
                SourcePillsPanel.Children.Remove(rb);

            bool sfw = Settings.Instance.SfwOnlyMode;
            foreach (var cs in Settings.Instance.CustomSources)
            {
                if (!cs.Enabled) continue;
                if (sfw && !cs.IsSfw) continue;

                var pill = new RadioButton
                {
                    Content = cs.Name,
                    Tag = cs.Id,
                    Style = (Style)FindResource("FilterPillStyle"),
                    Margin = new Thickness(0, 0, 6, 6)
                };
                pill.Checked += Source_Changed;
                SourcePillsPanel.Children.Add(pill);
            }
        }

        private void FilterDrawer_Click(object sender, RoutedEventArgs e)
        {
            FilterDrawer.Visibility = FilterDrawerToggle.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingFilters) return;
            SaveCurrentFilterProfile();
            UpdateFilterBadge();
            RebuildFilterChips();
            if (_currentSection == ActiveSection.Explore && IsLoaded)
            {
                _currentPage = 1;
                _ = DoExploreSearch(append: false);
            }
        }

        // ── Active-filter chips + Reset + result count ──

        private sealed class FilterChip
        {
            public string Label { get; set; } = "";
            public Action Clear { get; set; } = () => { };
        }

        /// <summary>Rebuild the dismissible chip row from the currently-active (non-default) filters.</summary>
        private void RebuildFilterChips()
        {
            if (FilterChipsPanel == null || RatingBox == null || SortBox == null || ResolutionBox == null || AspectBox == null) return;

            var chips = new List<FilterChip>();

            if (SortBox.SelectedIndex > 0 && SortBox.SelectedItem is ComboBoxItem sortItem)
                chips.Add(new FilterChip { Label = "Sort: " + StripText(sortItem.Content?.ToString()), Clear = () => SortBox.SelectedIndex = 0 });
            if (ResolutionBox.SelectedIndex > 0 && ResolutionBox.SelectedItem is ComboBoxItem resItem)
                chips.Add(new FilterChip { Label = StripText(resItem.Content?.ToString()), Clear = () => ResolutionBox.SelectedIndex = 0 });
            if (AspectBox.SelectedIndex > 0 && AspectBox.SelectedItem is ComboBoxItem aspItem)
                chips.Add(new FilterChip { Label = StripText(aspItem.Content?.ToString()), Clear = () => AspectBox.SelectedIndex = 0 });

            FilterChipsPanel.Items.Clear();
            foreach (var chip in chips)
            {
                FilterChipsPanel.Items.Add(BuildChipControl(chip));
            }

            if (ResetFiltersBtn != null)
                ResetFiltersBtn.Visibility = chips.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private static string StripText(string? s) => string.IsNullOrEmpty(s) ? "" :
            (s.Contains('(') ? s.Substring(0, s.IndexOf('(')).Trim() : s.Trim());

        private System.Windows.Controls.Border BuildChipControl(FilterChip chip)
        {
            var panel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            panel.Children.Add(new TextBlock
            {
                Text = chip.Label,
                FontSize = 10.5,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (System.Windows.Media.Brush)FindResource("TextBrush")
            });
            var x = new TextBlock
            {
                Text = "  ✕",
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (System.Windows.Media.Brush)FindResource("SubtextBrush")
            };
            panel.Children.Add(x);

            var border = new System.Windows.Controls.Border
            {
                Background = (System.Windows.Media.Brush)FindResource("ElevatedBrush"),
                BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(9, 2, 9, 2),
                Margin = new Thickness(0, 0, 6, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Child = panel,
                ToolTip = "Remove this filter"
            };
            border.MouseLeftButtonDown += (_, _) => { chip.Clear(); };
            return border;
        }

        private void ResetFilters_Click(object sender, RoutedEventArgs e)
        {
            if (RatingBox == null) return;
            _isUpdatingFilters = true;
            try
            {
                // Rating stays governed by the rating mode; reset the other three.
                SortBox.SelectedIndex = 0;
                ResolutionBox.SelectedIndex = 0;
                AspectBox.SelectedIndex = 0;
            }
            finally { _isUpdatingFilters = false; }

            SaveCurrentFilterProfile();
            UpdateFilterBadge();
            RebuildFilterChips();
            if (_currentSection == ActiveSection.Explore && IsLoaded)
            {
                _currentPage = 1;
                _ = DoExploreSearch(append: false);
            }
        }

        private void WallpaperGrid_PostsChanged(object? sender, int count)
        {
            if (ResultCountText == null) return;
            ResultCountText.Text = count > 0
                ? (count == 1 ? "1 wallpaper" : $"{count:N0} wallpapers")
                : "";
        }

        // ─────────────── Favorite collections / folders ───────────────

        private string _selectedCollection = "\u0001ALL"; // default: All Favorites

        /// <summary>Rebuild the collection folder tiles (All + each collection folder).</summary>
        private void RebuildCollectionChips()
        {
            if (CollectionChipsPanel == null) return;
            CollectionChipsPanel.Items.Clear();

            // "All Favorites" (everything) + each collection folder.
            CollectionChipsPanel.Items.Add(BuildFolderTile("All Favorites", "\u0001ALL", isAll: false, isAllFavorites: true));
            foreach (var name in FavoritesManager.Instance.GetCollections())
                CollectionChipsPanel.Items.Add(BuildFolderTile(name, name, isAll: false));
            CollectionChipsPanel.Items.Add(BuildAddFolderTile());
        }

        /// <summary>A dashed, card-sized "New Folder" tile aligned with the folder cards.</summary>
        private System.Windows.Controls.Border BuildAddFolderTile()
        {
            const double CardW = 236, CardH = 140, CardRadius = 10;

            var content = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            content.Children.Add(new TextBlock
            {
                Text = "+  New Folder",
                Foreground = (System.Windows.Media.Brush)FindResource("SubtextBrush"),
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Semibold"),
                FontSize = 12.5
            });

            var tile = new System.Windows.Controls.Border
            {
                Width = CardW, Height = CardH,
                Background = (System.Windows.Media.Brush)FindResource("ElevatedBrush"),
                BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(CardRadius),
                Margin = new Thickness(0, 0, 10, 10),
                Cursor = System.Windows.Input.Cursors.Hand,
                Child = content,
                ToolTip = "Create a new collection folder"
            };
            tile.MouseLeftButtonUp += (_, _) => NewCollection_Click(tile, new RoutedEventArgs());
            tile.MouseEnter += (_, _) => tile.BorderBrush = (System.Windows.Media.Brush)FindResource("AccentHoverBrush");
            tile.MouseLeave += (_, _) => tile.BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush");
            return tile;
        }

        /// <summary>Build a Home-style banner folder card: cover image + gradient + name + count,
        /// with drag/drop (assign wallpapers, reorder folders) and right-click rename/delete.</summary>
        private System.Windows.Controls.Border BuildFolderTile(string label, string value, bool isAll, bool isAllFavorites = false)
        {
            const double CardW = 236, CardH = 140, CardRadius = 10;

            bool active = string.Equals(_selectedCollection, value, StringComparison.OrdinalIgnoreCase);
            double bt = active ? 2 : 1;
            double innerW = CardW - bt * 2, innerH = CardH - bt * 2, innerR = System.Math.Max(0, CardRadius - bt);
            int count = isAllFavorites
                ? FavoritesManager.Instance.GetAllFavorites().Count
                : (isAll ? FavoritesManager.Instance.GetUncategorizedFavorites().Count
                         : FavoritesManager.Instance.GetFavoritesByCollection(value).Count);
            string cover = isAllFavorites
                ? (FavoritesManager.Instance.GetAllFavorites().FirstOrDefault()?.PreviewUrl ?? "")
                : FavoritesManager.Instance.GetCollectionCover(value);

            var grid = new Grid { Width = innerW, Height = innerH };

            // Cover artwork (falls back to a flat surface when empty).
            if (!string.IsNullOrEmpty(cover))
            {
                try
                {
                    var img = new System.Windows.Controls.Image
                    {
                        Stretch = System.Windows.Media.Stretch.UniformToFill,
                        Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(cover, UriKind.Absolute))
                    };
                    System.Windows.Media.RenderOptions.SetBitmapScalingMode(img, System.Windows.Media.BitmapScalingMode.LowQuality);
                    grid.Children.Add(img);
                }
                catch { }
            }

            // Top-left: item count pill.
            var countPill = new System.Windows.Controls.Border
            {
                Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#B3000000")!,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(7, 3, 7, 3),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(10, 10, 0, 0),
                Child = new TextBlock
                {
                    Text = count == 1 ? "1 item" : $"{count} items",
                    Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#E2E8F0")!,
                    FontSize = 9, FontFamily = new System.Windows.Media.FontFamily("Segoe UI Semibold")
                }
            };
            grid.Children.Add(countPill);

            // Bottom gradient with folder icon + name.
            var gradient = new System.Windows.Media.LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0), EndPoint = new System.Windows.Point(0, 1)
            };
            gradient.GradientStops.Add(new System.Windows.Media.GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#00090A0F"), 0.0));
            gradient.GradientStops.Add(new System.Windows.Media.GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#CC090A0F"), 0.45));
            gradient.GradientStops.Add(new System.Windows.Media.GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F0090A0F"), 1.0));

            string glyph = isAllFavorites ? "IconHeartGeo" : (isAll ? "IconSparkleGeo" : "IconFolderGeo");
            var titleRow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(12, 0, 12, 10) };
            titleRow.Children.Add(new System.Windows.Shapes.Path
            {
                Data = (System.Windows.Media.Geometry)FindResource(glyph),
                Width = 14, Height = 14, Stretch = System.Windows.Media.Stretch.Uniform,
                Fill = System.Windows.Media.Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0)
            });
            titleRow.Children.Add(new TextBlock
            {
                Text = label, Foreground = System.Windows.Media.Brushes.White,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Bold"), FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis
            });
            var gradientBorder = new System.Windows.Controls.Border
            {
                Height = 62, VerticalAlignment = VerticalAlignment.Bottom, Background = gradient, Child = titleRow
            };
            grid.Children.Add(gradientBorder);

            // Clip the cover/content to rounded corners matching the card interior.
            grid.Clip = new System.Windows.Media.RectangleGeometry(new Rect(0, 0, innerW, innerH), innerR, innerR);

            var tile = new System.Windows.Controls.Border
            {
                Width = CardW, Height = CardH,
                Background = string.IsNullOrEmpty(cover) ? (System.Windows.Media.Brush)FindResource("ElevatedBrush") : (System.Windows.Media.Brush)FindResource("CardBgBrush"),
                BorderBrush = active ? (System.Windows.Media.Brush)FindResource("AccentBrush") : (System.Windows.Media.Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(bt),
                CornerRadius = new CornerRadius(CardRadius),
                Margin = new Thickness(0, 0, 10, 10),
                Cursor = System.Windows.Input.Cursors.Hand,
                Child = grid,
                AllowDrop = !isAllFavorites,
                Tag = value
            };

            tile.MouseLeftButtonUp += (_, _) =>
            {
                if (_folderDragging) return;
                _selectedCollection = value;
                RebuildCollectionChips();
                if (_currentSection == ActiveSection.Favorites) DisplayCurrentSection();
            };

            // Only real collection folders can be dragged (to reorder).
            if (!isAll && !isAllFavorites)
            {
                tile.PreviewMouseLeftButtonDown += (_, e) => _folderPress = e.GetPosition(null);
                tile.MouseMove += (s, e) =>
                {
                    if (e.LeftButton != MouseButtonState.Pressed) return;
                    var p = e.GetPosition(null);
                    if (System.Math.Abs(p.X - _folderPress.X) < SystemParameters.MinimumHorizontalDragDistance &&
                        System.Math.Abs(p.Y - _folderPress.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                    _folderDragging = true;
                    try { System.Windows.DragDrop.DoDragDrop(tile, new System.Windows.DataObject("WallSafeCollection", value), System.Windows.DragDropEffects.Move); }
                    finally { _folderDragging = false; }
                };
            }

            // "All Favorites" is not a drop target (it shows everything).
            if (!isAllFavorites)
            {
                tile.DragOver += (_, e) =>
                {
                    if (e.Data.GetDataPresent("WallSafePostItem")) e.Effects = System.Windows.DragDropEffects.Copy;
                    else if (e.Data.GetDataPresent("WallSafeCollection") && !isAll) e.Effects = System.Windows.DragDropEffects.Move;
                    else e.Effects = System.Windows.DragDropEffects.None;
                    if (e.Effects != System.Windows.DragDropEffects.None)
                        tile.BorderBrush = (System.Windows.Media.Brush)FindResource("AccentHoverBrush");
                    e.Handled = true;
                };
                tile.DragLeave += (_, _) =>
                    tile.BorderBrush = active ? (System.Windows.Media.Brush)FindResource("AccentBrush") : (System.Windows.Media.Brush)FindResource("BorderBrush");
                tile.Drop += (_, e) =>
                {
                    tile.BorderBrush = active ? (System.Windows.Media.Brush)FindResource("AccentBrush") : (System.Windows.Media.Brush)FindResource("BorderBrush");
                    if (e.Data.GetDataPresent("WallSafePostItem") && e.Data.GetData("WallSafePostItem") is PostItem dropped)
                    {
                        AssignItemToCollection(dropped, isAll ? "" : value);
                    }
                    else if (e.Data.GetDataPresent("WallSafeCollection") && e.Data.GetData("WallSafeCollection") is string draggedName && !isAll)
                    {
                        var order = FavoritesManager.Instance.GetCollections();
                        int targetIndex = order.FindIndex(c => c.Equals(value, StringComparison.OrdinalIgnoreCase));
                        if (targetIndex >= 0 && !draggedName.Equals(value, StringComparison.OrdinalIgnoreCase))
                        {
                            FavoritesManager.Instance.MoveCollection(draggedName, targetIndex);
                            RebuildCollectionChips();
                        }
                    }
                    e.Handled = true;
                };
            }

            if (!isAll && !isAllFavorites)
            {
                var menu = new System.Windows.Controls.ContextMenu();
                var rename = new System.Windows.Controls.MenuItem { Header = "Rename Collection…" };
                rename.Click += (_, _) => RenameCollectionPrompt(value);
                var delete = new System.Windows.Controls.MenuItem { Header = "Delete Collection" };
                delete.Click += (_, _) => DeleteCollectionConfirm(value);
                menu.Items.Add(rename);
                menu.Items.Add(delete);
                tile.ContextMenu = menu;
            }
            return tile;
        }

        private System.Windows.Point _folderPress;
        private bool _folderDragging;

        private void NewCollection_Click(object sender, RoutedEventArgs e)
        {
            var name = ThemedDialog.Prompt(this, "New Collection", "Enter a name for the new collection:", "", "Create");
            if (string.IsNullOrWhiteSpace(name)) return;
            if (FavoritesManager.Instance.CreateCollection(name.Trim()))
            {
                _selectedCollection = name.Trim();
                RebuildCollectionChips();
                if (_currentSection == ActiveSection.Favorites) DisplayCurrentSection();
                ShowToast("✓", $"Collection \u201c{name.Trim()}\u201d created");
            }
            else
            {
                ShowToast("⚠", "A collection with that name already exists");
            }
        }

        private void RenameCollectionPrompt(string oldName)
        {
            var name = ThemedDialog.Prompt(this, "Rename Collection", $"Rename \u201c{oldName}\u201d to:", oldName, "Rename");
            if (string.IsNullOrWhiteSpace(name) || name.Trim() == oldName) return;
            if (FavoritesManager.Instance.RenameCollection(oldName, name.Trim()))
            {
                if (_selectedCollection == oldName) _selectedCollection = name.Trim();
                RebuildCollectionChips();
                if (_currentSection == ActiveSection.Favorites) DisplayCurrentSection();
            }
            else ShowToast("⚠", "Couldn't rename (name may already exist)");
        }

        private void DeleteCollectionConfirm(string name)
        {
            bool ok = ThemedDialog.Confirm(this, "Delete Collection",
                $"Delete the collection \u201c{name}\u201d?\n\nThe wallpapers stay in your favorites — they just become uncategorized.",
                "Delete");
            if (!ok) return;
            FavoritesManager.Instance.DeleteCollection(name);
            if (_selectedCollection == name) _selectedCollection = "\u0001ALL";
            RebuildCollectionChips();
            if (_currentSection == ActiveSection.Favorites) DisplayCurrentSection();
        }

        private void WallpaperGrid_MoveToCollectionRequested(object? sender, PostItem item)
        {
            var collections = FavoritesManager.Instance.GetCollections();

            var menu = new System.Windows.Controls.ContextMenu();
            var uncategorized = new System.Windows.Controls.MenuItem { Header = "Uncategorized (no collection)" };
            uncategorized.Click += (_, _) => AssignItemToCollection(item, "");
            menu.Items.Add(uncategorized);
            if (collections.Count > 0) menu.Items.Add(new System.Windows.Controls.Separator());
            foreach (var c in collections)
            {
                var mi = new System.Windows.Controls.MenuItem { Header = c };
                var captured = c;
                mi.Click += (_, _) => AssignItemToCollection(item, captured);
                menu.Items.Add(mi);
            }
            menu.Items.Add(new System.Windows.Controls.Separator());
            var newC = new System.Windows.Controls.MenuItem { Header = "New Collection…" };
            newC.Click += (_, _) =>
            {
                var name = ThemedDialog.Prompt(this, "New Collection", "Enter a name for the new collection:", "", "Create");
                if (!string.IsNullOrWhiteSpace(name))
                    AssignItemToCollection(item, name.Trim());
            };
            menu.Items.Add(newC);
            menu.IsOpen = true;
        }

        private void AssignItemToCollection(PostItem item, string collection)
        {
            FavoritesManager.Instance.AssignToCollection(item, collection);
            RebuildCollectionChips();
            if (_currentSection == ActiveSection.Favorites) DisplayCurrentSection();
            ShowToast("✓", string.IsNullOrEmpty(collection)
                ? "Removed from collection"
                : $"Added to \u201c{collection}\u201d");
        }

        private void WallpaperGrid_RemoveFromCollectionRequested(object? sender, PostItem item)
        {
            RebuildCollectionChips();
            if (_currentSection == ActiveSection.Favorites) DisplayCurrentSection();
            ShowToast("✓", "Removed from folder (kept in Favorites)");
        }


        private void UpdateFilterBadge()
        {
            if (RatingBox == null || SortBox == null || ResolutionBox == null || AspectBox == null) return;

            int activeCount = 0;
            if (RatingBox.SelectedIndex > 0) activeCount++;
            if (SortBox.SelectedIndex > 0) activeCount++;
            if (ResolutionBox.SelectedIndex > 0) activeCount++;
            if (AspectBox.SelectedIndex > 0) activeCount++;

            if (activeCount > 0)
            {
                FilterCountText.Text = activeCount.ToString();
                FilterCountBadge.Visibility = Visibility.Visible;
            }
            else
            {
                FilterCountBadge.Visibility = Visibility.Collapsed;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        // Modern Interactive Search Engine & Discovery
        // ═════════════════════════════════════════════════════════════════

        private readonly List<string> _recentSearches = new();
        private bool _suppressAutocomplete = false;

        private void TagSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TagSearchBox.Text))
            {
                ShowRecentSearches();
            }
        }

        private void TagSearch_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // Close popup when focus leaves both search box and popup elements
            if (!TagSearchBox.IsKeyboardFocusWithin && !AutocompletePopup.IsKeyboardFocusWithin)
            {
                AutocompletePopup.IsOpen = false;
            }
        }

        private async void TagSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            var text = TagSearchBox.Text;
            var trimmed = text.Trim();
            TagPlaceholder.Visibility = string.IsNullOrEmpty(text) ? Visibility.Visible : Visibility.Collapsed;
            ClearSearchBtn.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;

            if (_suppressAutocomplete)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                ShowRecentSearches();
                return;
            }

            RecentSearchesPanel.Visibility = Visibility.Collapsed;
            AutocompleteList.Visibility = Visibility.Visible;

            // Zero-latency instant local predictive match (aliases, popular characters, series, cache)
            var instant = _api.GetInstantLocalSuggestions(trimmed);
            if (instant.Count > 0)
            {
                AutocompleteList.ItemsSource = instant;
                AutocompletePopup.IsOpen = true;
            }

            if (trimmed.Length < 2)
            {
                if (instant.Count == 0)
                    AutocompletePopup.IsOpen = false;
                return;
            }

            _autocompleteCts.Cancel();
            _autocompleteCts = new CancellationTokenSource();
            var ct = _autocompleteCts.Token;

            try
            {
                // Fast 80ms debounce for network query
                await Task.Delay(80, ct);
                var suggestions = await _api.FetchTagSuggestionsAsync(_currentSource, trimmed, ct);
                if (ct.IsCancellationRequested || _suppressAutocomplete) return;

                if (suggestions.Count > 0)
                {
                    AutocompleteList.ItemsSource = suggestions;
                    AutocompletePopup.IsOpen = true;
                }
                else if (instant.Count == 0)
                {
                    AutocompletePopup.IsOpen = false;
                }
            }
            catch (OperationCanceledException) { }
            catch { }
        }

        private void TagSearch_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Down)
            {
                if (AutocompletePopup.IsOpen)
                {
                    if (AutocompleteList.Visibility == Visibility.Visible && AutocompleteList.Items.Count > 0)
                    {
                        int next = AutocompleteList.SelectedIndex + 1;
                        if (next >= AutocompleteList.Items.Count) next = AutocompleteList.Items.Count - 1;
                        AutocompleteList.SelectedIndex = next;
                        AutocompleteList.ScrollIntoView(AutocompleteList.SelectedItem);
                        e.Handled = true;
                    }
                    else if (RecentSearchesPanel.Visibility == Visibility.Visible && RecentSearchesList.Items.Count > 0)
                    {
                        int next = RecentSearchesList.SelectedIndex + 1;
                        if (next >= RecentSearchesList.Items.Count) next = RecentSearchesList.Items.Count - 1;
                        RecentSearchesList.SelectedIndex = next;
                        RecentSearchesList.ScrollIntoView(RecentSearchesList.SelectedItem);
                        e.Handled = true;
                    }
                }
            }
            else if (e.Key == Key.Up)
            {
                if (AutocompletePopup.IsOpen)
                {
                    if (AutocompleteList.Visibility == Visibility.Visible && AutocompleteList.Items.Count > 0)
                    {
                        if (AutocompleteList.SelectedIndex > 0)
                        {
                            AutocompleteList.SelectedIndex--;
                            AutocompleteList.ScrollIntoView(AutocompleteList.SelectedItem);
                        }
                        else
                        {
                            AutocompleteList.SelectedIndex = -1;
                        }
                        e.Handled = true;
                    }
                    else if (RecentSearchesPanel.Visibility == Visibility.Visible && RecentSearchesList.Items.Count > 0)
                    {
                        if (RecentSearchesList.SelectedIndex > 0)
                        {
                            RecentSearchesList.SelectedIndex--;
                            RecentSearchesList.ScrollIntoView(RecentSearchesList.SelectedItem);
                        }
                        else
                        {
                            RecentSearchesList.SelectedIndex = -1;
                        }
                        e.Handled = true;
                    }
                }
            }
            else if (e.Key == Key.Tab)
            {
                if (AutocompletePopup.IsOpen)
                {
                    if (AutocompleteList.Visibility == Visibility.Visible && AutocompleteList.Items.Count > 0)
                    {
                        var target = AutocompleteList.SelectedItem as TagSuggestion ?? AutocompleteList.Items[0] as TagSuggestion;
                        if (target != null)
                        {
                            _suppressAutocomplete = true;
                            try
                            {
                                TagSearchBox.Text = target.Name;
                                TagSearchBox.CaretIndex = TagSearchBox.Text.Length;
                                AutocompletePopup.IsOpen = false;
                            }
                            finally
                            {
                                _suppressAutocomplete = false;
                            }
                            e.Handled = true;
                        }
                    }
                    else if (RecentSearchesPanel.Visibility == Visibility.Visible && RecentSearchesList.Items.Count > 0)
                    {
                        var target = RecentSearchesList.SelectedItem as string ?? RecentSearchesList.Items[0] as string;
                        if (!string.IsNullOrEmpty(target))
                        {
                            _suppressAutocomplete = true;
                            try
                            {
                                TagSearchBox.Text = target;
                                TagSearchBox.CaretIndex = TagSearchBox.Text.Length;
                                AutocompletePopup.IsOpen = false;
                            }
                            finally
                            {
                                _suppressAutocomplete = false;
                            }
                            e.Handled = true;
                        }
                    }
                }
            }
            else if (e.Key == Key.Enter)
            {
                if (AutocompletePopup.IsOpen)
                {
                    if (AutocompleteList.Visibility == Visibility.Visible && AutocompleteList.SelectedItem is TagSuggestion selected)
                    {
                        SelectAutocompleteTag(selected.Name);
                        e.Handled = true;
                        return;
                    }
                    else if (RecentSearchesPanel.Visibility == Visibility.Visible && RecentSearchesList.SelectedItem is string recent)
                    {
                        SelectAutocompleteTag(recent);
                        e.Handled = true;
                        return;
                    }
                }
                ExecuteSearch();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                AutocompletePopup.IsOpen = false;
                e.Handled = true;
            }
        }

        private void AutocompleteList_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter && AutocompleteList.SelectedItem is TagSuggestion tag)
            {
                SelectAutocompleteTag(tag.Name);
                e.Handled = true;
            }
            else if (e.Key == Key.Tab)
            {
                var target = AutocompleteList.SelectedItem as TagSuggestion ?? (AutocompleteList.Items.Count > 0 ? AutocompleteList.Items[0] as TagSuggestion : null);
                if (target != null)
                {
                    _suppressAutocomplete = true;
                    try
                    {
                        TagSearchBox.Text = target.Name;
                        TagSearchBox.CaretIndex = TagSearchBox.Text.Length;
                        AutocompletePopup.IsOpen = false;
                    }
                    finally
                    {
                        _suppressAutocomplete = false;
                    }
                    TagSearchBox.Focus();
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Escape)
            {
                AutocompletePopup.IsOpen = false;
                TagSearchBox.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Up && AutocompleteList.SelectedIndex == 0)
            {
                AutocompleteList.SelectedIndex = -1;
                TagSearchBox.Focus();
                TagSearchBox.CaretIndex = TagSearchBox.Text.Length;
                e.Handled = true;
            }
        }

        private void Autocomplete_Selected(object sender, SelectionChangedEventArgs e)
        {
            // Only act on mouse-driven selection (not keyboard navigation which also fires SelectionChanged)
        }

        private void AutocompleteList_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && AutocompleteList.SelectedItem is TagSuggestion tag)
            {
                SelectAutocompleteTag(tag.Name);
            }
        }

        private void SelectAutocompleteTag(string tagName)
        {
            _suppressAutocomplete = true;
            try
            {
                _autocompleteCts.Cancel();
                TagSearchBox.Text = tagName;
                TagSearchBox.CaretIndex = TagSearchBox.Text.Length;
                AutocompletePopup.IsOpen = false;
            }
            finally
            {
                _suppressAutocomplete = false;
            }
            ExecuteSearch();
        }

        private void ShowRecentSearches()
        {
            if (_recentSearches.Count > 0)
            {
                RecentSearchesList.ItemsSource = null;
                RecentSearchesList.ItemsSource = _recentSearches;
                RecentSearchesPanel.Visibility = Visibility.Visible;
                AutocompleteList.Visibility = Visibility.Collapsed;
                AutocompletePopup.IsOpen = true;
            }
            else
            {
                AutocompletePopup.IsOpen = false;
            }
        }

        private void RecentSearch_Selected(object sender, SelectionChangedEventArgs e)
        {
            if (RecentSearchesList.SelectedItem is string query)
            {
                _suppressAutocomplete = true;
                try
                {
                    _autocompleteCts.Cancel();
                    TagSearchBox.Text = query;
                    TagSearchBox.CaretIndex = TagSearchBox.Text.Length;
                    AutocompletePopup.IsOpen = false;
                }
                finally
                {
                    _suppressAutocomplete = false;
                }
                ExecuteSearch();
            }
        }

        private void ClearRecentSearches_Click(object sender, RoutedEventArgs e)
        {
            _recentSearches.Clear();
            AutocompletePopup.IsOpen = false;
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            _suppressAutocomplete = true;
            try
            {
                _autocompleteCts.Cancel();
                TagSearchBox.Text = "";
                AutocompletePopup.IsOpen = false;
            }
            finally
            {
                _suppressAutocomplete = false;
            }
            TagSearchBox.Focus();
            ExecuteSearch();
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            ExecuteSearch();
        }

        private void ExecuteSearch()
        {
            _autocompleteCts.Cancel();
            AutocompletePopup.IsOpen = false;
            var text = TagSearchBox.Text.Trim();

            if (!string.IsNullOrEmpty(text))
            {
                if (!_recentSearches.Contains(text, StringComparer.OrdinalIgnoreCase))
                {
                    _recentSearches.Insert(0, text);
                    if (_recentSearches.Count > 8) _recentSearches.RemoveAt(_recentSearches.Count - 1);
                }
            }

            _currentPage = 1;
            if (_currentSection != ActiveSection.Explore)
            {
                NavExplore.IsChecked = true;
            }
            else
            {
                _ = DoExploreSearch(append: false);
            }
        }

        // ═════════════════════════════════════════════════════════════════
        // Explore Search Execution
        // ═════════════════════════════════════════════════════════════════

        private async Task DoExploreSearch(bool append)
        {
            if (Interlocked.CompareExchange(ref _isLoading, 1, 0) != 0)
            {
                if (!append || _pendingSearch != 1)
                {
                    Interlocked.Exchange(ref _pendingSearch, append ? 2 : 1);
                }
                _searchCts.Cancel();
                return;
            }

            try
            {
                bool currentAppend = append;
                while (true)
                {
                    _searchCts.Cancel();
                    _searchCts = new CancellationTokenSource();
                    var ct = _searchCts.Token;

                    if (!currentAppend)
                    {
                        _currentPage = 1;
                        _cachedExplorePosts.Clear();
                        WallpaperGridControl.Clear();
                        SetLoading(true, "Searching anime wallpapers…");
                    }
                    else
                    {
                        WallpaperGridControl.SetLoadingMore(true);
                    }

                    try
                    {
                        string tagQuery = BuildTagQuery();
                        int limit = 24;

                        var rawPosts = await _api.FetchPostsAsync(_currentSource, tagQuery, _currentPage, limit, ct);
                        ct.ThrowIfCancellationRequested();

                        // Apply client-side resolution, blacklist and aspect ratio filters
                        var filtered = FilterPosts(rawPosts);

                        // Batch Accumulator: if filtering aggressively reduced cards, fetch next page automatically
                        int attempts = 0;
                        while (filtered.Count < 10 && rawPosts.Count >= limit && attempts < 2)
                        {
                            attempts++;
                            _currentPage++;
                            var extra = await _api.FetchPostsAsync(_currentSource, tagQuery, _currentPage, limit, ct);
                            if (extra.Count == 0) break;
                            rawPosts.AddRange(extra);
                            filtered.AddRange(FilterPosts(extra));
                        }

                        ct.ThrowIfCancellationRequested();

                        if (!currentAppend)
                        {
                            _cachedExplorePosts = filtered;
                            WallpaperGridControl.SetPosts(_cachedExplorePosts, canLoadMore: rawPosts.Count >= limit);
                        }
                        else
                        {
                            _cachedExplorePosts.AddRange(filtered);
                            WallpaperGridControl.AddPosts(filtered, canLoadMore: rawPosts.Count >= limit);
                        }

                        // Background pre-fetch thumbnails to disk cache for buttery smooth scrolling
                        ImageCacheService.Instance.PreloadThumbnails(filtered.Select(p => p.PreviewUrl));

                        CheckEmptyState(_cachedExplorePosts.Count, "No wallpapers found", "Try removing some tags or relaxing filters.");
                        if (StatusText != null) StatusText.Text = $"{_cachedExplorePosts.Count} wallpapers loaded  •  Page {_currentPage}";
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        if (StatusText != null) StatusText.Text = $"Error: {ex.Message}";
                    }

                    int pending = Interlocked.Exchange(ref _pendingSearch, 0);
                    if (pending == 0)
                    {
                        break;
                    }
                    currentAppend = (pending == 2);
                }
            }
            finally
            {
                WallpaperGridControl.SetLoadingMore(false);
                SetLoading(false);
                Interlocked.Exchange(ref _isLoading, 0); // Release lock
                if (Interlocked.Exchange(ref _pendingSearch, 0) != 0)
                {
                    _ = DoExploreSearch(append: false);
                }
            }
        }

        private string BuildTagQuery()
        {
            var tags = new List<string>();

            // Parse and resolve search input using predictive alias engine
            string searchText = TagSearchBox.Text.Trim();
            if (!string.IsNullOrEmpty(searchText))
            {
                if (searchText.Contains(','))
                {
                    var parts = searchText.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        string resolved = ApiClient.ResolveTag(part);
                        if (!string.IsNullOrEmpty(resolved) && !tags.Contains(resolved, StringComparer.OrdinalIgnoreCase))
                            tags.Add(resolved);
                    }
                }
                else
                {
                    string resolved = ApiClient.ResolveTag(searchText);
                    if (!string.IsNullOrEmpty(resolved) && !tags.Contains(resolved, StringComparer.OrdinalIgnoreCase))
                        tags.Add(resolved);
                }
            }

            var ratingTag = ((ComboBoxItem?)RatingBox?.SelectedItem)?.Tag as string;
            var sortTag = ((ComboBoxItem?)SortBox?.SelectedItem)?.Tag as string;

            if (Settings.Instance.RatingMode == ContentRatingMode.SfwOnly)
            {
                tags.RemoveAll(t => t.StartsWith("rating:", StringComparison.OrdinalIgnoreCase));
                tags.Add("rating:s");
            }
            else if (Settings.Instance.RatingMode == ContentRatingMode.Questionable)
            {
                tags.RemoveAll(t => t.StartsWith("rating:", StringComparison.OrdinalIgnoreCase));
                tags.Add("rating:q");
            }
            else if (!string.IsNullOrEmpty(ratingTag))
            {
                tags.Add(ratingTag);
            }

            if (!string.IsNullOrEmpty(sortTag)) tags.Add(sortTag);

            return string.Join(" ", tags);
        }

        private List<PostItem> FilterPosts(List<PostItem> posts)
        {
            int minWidth = 0;
            if (ResolutionBox?.SelectedItem is ComboBoxItem resItem && int.TryParse(resItem.Tag as string, out int mw))
                minWidth = mw;

            string aspect = ((ComboBoxItem?)AspectBox?.SelectedItem)?.Tag as string ?? "all";

            // Global client-side blacklist filtering — cache parsed array to avoid re-splitting every call
            string blacklistStr = Settings.Instance.TagBlacklist ?? string.Empty;
            if (blacklistStr != _cachedBlacklistSource)
            {
                _cachedBlacklistSource = blacklistStr;
                _cachedBlacklist = !string.IsNullOrWhiteSpace(blacklistStr)
                    ? blacklistStr.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    : null;
            }
            var blacklist = _cachedBlacklist;

            bool discretionBlur = Settings.Instance.DiscretionBlur;
            var ratingMode = Settings.Instance.RatingMode;
            string selectedRatingTag = ((ComboBoxItem?)RatingBox?.SelectedItem)?.Tag as string ?? "";

            return posts.Where(p =>
            {
                // Strict Rating Enforcement: Never leak non-SFW into SFW mode
                if (ratingMode == ContentRatingMode.SfwOnly)
                {
                    if (p.Rating != "s") return false;
                }
                else if (ratingMode == ContentRatingMode.Questionable)
                {
                    if (p.Rating == "e") return false;
                }
                else if (ratingMode == ContentRatingMode.Custom)
                {
                    if (selectedRatingTag == "rating:s" && p.Rating != "s") return false;
                    if (selectedRatingTag == "rating:q" && p.Rating != "q") return false;
                    if (selectedRatingTag == "rating:e" && p.Rating != "e") return false;
                }

                if (minWidth > 0 && p.Width < minWidth) return false;

                if (aspect == "landscape" && (p.Width <= p.Height || (double)p.Width / p.Height < 1.3)) return false;
                if (aspect == "ultrawide" && ((double)p.Width / Math.Max(1, p.Height) < 2.0)) return false;
                if (aspect == "portrait" && (p.Width >= p.Height)) return false;

                if (blacklist != null && blacklist.Length > 0)
                {
                    if (blacklist.Any(b => p.Tags.Contains(b, StringComparison.OrdinalIgnoreCase)))
                        return false;
                }

                p.IsDiscreet = discretionBlur && (p.Rating == "q" || p.Rating == "e");

                return true;
            }).ToList();
        }

        private async void WallpaperGrid_LoadMoreRequested(object? sender, EventArgs e)
        {
            _currentPage++;
            await DoExploreSearch(append: true);
        }

        // ═════════════════════════════════════════════════════════════════
        // Card Events & Wallpaper Applied
        // ═════════════════════════════════════════════════════════════════

        private void WallpaperGrid_WallpaperApplied(object? sender, PostItem e)
        {
            ShowToast("✓", "Wallpaper applied to desktop!");
        }

        private void WallpaperGrid_FavoriteToggled(object? sender, PostItem e)
        {
            if (e.IsFavorite)
            {
                ShowToast("♥", "Added to Favorites");
            }
            else
            {
                // Offer a one-tap Undo to restore the just-removed favorite.
                var removed = e;
                ShowToast("✕", "Removed from Favorites", () =>
                {
                    FavoritesManager.Instance.AddFavorite(removed);
                    removed.IsFavorite = true;
                    if (_currentSection == ActiveSection.Favorites)
                        DisplayCurrentSection();
                    ShowToast("♥", "Favorite restored");
                });
            }

            if (_currentSection == ActiveSection.Favorites)
                DisplayCurrentSection();
        }

        private void WallpaperGrid_DownloadCompleted(object? sender, PostItem e)
        {
            ShowToast("📥", "Saved to Downloaded Wallpapers!");
        }

        private async void WallpaperGrid_PreviewRequested(object? sender, PostItem item)
        {
            _previewItem = item;
            if (_previewItem == null) return;
            int targetId = _previewItem.Id;

            PreviewTitleText.Text = $"Wallpaper #{_previewItem.Id} ({_previewItem.Source})";
            PreviewResText.Text = $" • {_previewItem.ResolutionText} • {_previewItem.AspectRatioText}";
            PreviewRatingText.Text = _previewItem.RatingDisplay;
            try
            {
                PreviewRatingBadge.Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_previewItem.RatingBadgeBackground));
            }
            catch { }

            PreviewTagsText.Text = string.IsNullOrWhiteSpace(_previewItem.Tags) ? "No tags" : _previewItem.Tags.Replace(" ", " • ");
            PreviewScoreText.Text = $"★ {_previewItem.Score}";

            UpdatePreviewFavState();
            UpdatePreviewDownloadState();
            if (PreviewApplyText != null) PreviewApplyText.Text = "Apply";
            if (PreviewApplyBtn != null) PreviewApplyBtn.IsEnabled = true;

            // 1. INSTANT ARTWORK DISPLAY (0ms delay):
            // Show the already-cached thumbnail immediately so user never stares at an empty black box!
            var memThumb = ImageCacheService.Instance.GetFromMemoryCache(_previewItem.PreviewUrl);
            if (memThumb != null)
            {
                PreviewMainImage.Source = memThumb;
            }
            else
            {
                string thumbPath = await ImageCacheService.Instance.GetCachedImagePathAsync(_previewItem.PreviewUrl);
                if (_previewItem?.Id == targetId && File.Exists(thumbPath))
                {
                    try
                    {
                        var thumbBi = new System.Windows.Media.Imaging.BitmapImage();
                        thumbBi.BeginInit();
                        thumbBi.UriSource = new Uri(thumbPath, UriKind.Absolute);
                        thumbBi.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        thumbBi.EndInit();
                        thumbBi.Freeze();
                        PreviewMainImage.Source = thumbBi;
                    }
                    catch { }
                }
            }

            PreviewLoadingIndicator.Visibility = Visibility.Visible;
            PreviewModal.Visibility = Visibility.Visible;

            // 2. FAST BACKGROUND HIGH-RES STREAMING:
            // Prefer SampleUrl (~1500px web preview, 300KB-800KB) over huge 40MB raw file for fast lightbox preview
            string targetHighResUrl = !string.IsNullOrEmpty(_previewItem?.SampleUrl)
                ? _previewItem.SampleUrl
                : (_previewItem?.BestImageUrl ?? "");

            try
            {
                string highResPath = await ImageCacheService.Instance.GetCachedImagePathAsync(targetHighResUrl);
                if (_previewItem?.Id != targetId) return;

                if (File.Exists(highResPath))
                {
                    var highResBi = await Task.Run(() =>
                    {
                        var bi = new System.Windows.Media.Imaging.BitmapImage();
                        bi.BeginInit();
                        bi.UriSource = new Uri(highResPath, UriKind.Absolute);
                        bi.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bi.DecodePixelWidth = 1920; // Crisp full-HD preview, fast decode & lightweight memory
                        bi.EndInit();
                        bi.Freeze();
                        return bi;
                    });

                    if (_previewItem?.Id == targetId)
                    {
                        PreviewMainImage.Source = highResBi;
                    }
                }
            }
            catch { }
            finally
            {
                if (_previewItem?.Id == targetId)
                {
                    PreviewLoadingIndicator.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void ClosePreview_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ClosePreview_Click(sender, e);
        }

        private void ClosePreview_Click(object sender, RoutedEventArgs e)
        {
            PreviewModal.Visibility = Visibility.Collapsed;
            PreviewMainImage.Source = null;
            _previewItem = null;
        }

        private async void PreviewApply_Click(object sender, RoutedEventArgs e)
        {
            if (_previewItem == null) return;
            try
            {
                if (PreviewApplyText != null) PreviewApplyText.Text = "Applying…";
                if (PreviewApplyBtn != null) PreviewApplyBtn.IsEnabled = false;

                ShowToast("⏳", "Applying wallpaper…");
                await WallpaperManager.Instance.ApplyWallpaperAsync(_previewItem);

                if (PreviewApplyText != null) PreviewApplyText.Text = "Applied ✓";
                ShowToast("✓", "Wallpaper applied to desktop!");
            }
            catch (Exception ex)
            {
                if (PreviewApplyText != null) PreviewApplyText.Text = "Apply";
                ShowToast("⚠", $"Failed: {ex.Message}");
            }
            finally
            {
                if (PreviewApplyBtn != null) PreviewApplyBtn.IsEnabled = true;
            }
        }

        private async void PreviewLockScreen_Click(object sender, RoutedEventArgs e)
        {
            if (_previewItem == null) return;
            try
            {
                ShowToast("⏳", "Setting Windows lock screen…");
                bool ok = await WallpaperManager.Instance.SetLockScreenAsync(_previewItem.BestImageUrl);
                if (ok) ShowToast("✓", "Lock Screen updated!");
                else ShowToast("⚠", "Could not set lock screen");
            }
            catch (Exception ex)
            {
                ShowToast("⚠", $"Failed: {ex.Message}");
            }
        }

        private async void PreviewDownload_Click(object sender, RoutedEventArgs e)
        {
            if (_previewItem == null) return;
            try
            {
                if (PreviewDownloadText != null) PreviewDownloadText.Text = "Downloading…";
                if (PreviewDownloadBtn != null) PreviewDownloadBtn.IsEnabled = false;

                ShowToast("⏳", "Downloading full-resolution image…");
                await DownloadsManager.Instance.DownloadPostAsync(_previewItem, null, CancellationToken.None);
                UpdatePreviewDownloadState();
                ShowToast("✓", "Saved to Downloaded Wallpapers!");
            }
            catch (Exception ex)
            {
                if (PreviewDownloadText != null) PreviewDownloadText.Text = "Download";
                ShowToast("⚠", $"Download failed: {ex.Message}");
            }
            finally
            {
                if (PreviewDownloadBtn != null) PreviewDownloadBtn.IsEnabled = true;
            }
        }

        private void PreviewFavorite_Click(object sender, RoutedEventArgs e)
        {
            if (_previewItem == null) return;
            FavoritesManager.Instance.ToggleFavorite(_previewItem);
            UpdatePreviewFavState();
            if (_previewItem.IsFavorite) ShowToast("♥", "Added to Favorites");
            else ShowToast("✕", "Removed from Favorites");

            if (_currentSection == ActiveSection.Favorites)
                DisplayCurrentSection();
        }

        private void PreviewWeb_Click(object sender, RoutedEventArgs e)
        {
            if (_previewItem == null || string.IsNullOrEmpty(_previewItem.SourceUrl)) return;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _previewItem.SourceUrl,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void UpdatePreviewFavState()
        {
            if (_previewItem == null || PreviewFavIcon == null) return;
            if (_previewItem.IsFavorite)
            {
                PreviewFavIcon.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF4, 0x3F, 0x5E));
                PreviewFavBtn.ToolTip = "Remove from Favorites";
            }
            else
            {
                PreviewFavIcon.Fill = (System.Windows.Media.Brush)FindResource("SubtextBrush");
                PreviewFavBtn.ToolTip = "Add to Favorites";
            }
        }

        private void UpdatePreviewDownloadState()
        {
            if (_previewItem == null || PreviewDownloadIcon == null || PreviewDownloadText == null) return;
            if (_previewItem.IsDownloaded)
            {
                if (TryFindResource("IconCheckGeo") is System.Windows.Media.Geometry checkGeo)
                    PreviewDownloadIcon.Data = checkGeo;
                if (TryFindResource("EmeraldBrush") is System.Windows.Media.Brush emerald)
                    PreviewDownloadIcon.Fill = emerald;
                PreviewDownloadText.Text = "Downloaded";
            }
            else
            {
                if (TryFindResource("IconDownloadGeo") is System.Windows.Media.Geometry dlGeo)
                    PreviewDownloadIcon.Data = dlGeo;
                PreviewDownloadIcon.Fill = (System.Windows.Media.Brush)FindResource("SubtextBrush");
                PreviewDownloadText.Text = "Download";
            }
        }

        // ═════════════════════════════════════════════════════════════════
        // Panic Button & Safe Wallpaper
        // ═════════════════════════════════════════════════════════════════

        private void Panic_Click(object sender, RoutedEventArgs e)
        {
            WallpaperManager.Instance.ApplySafeWallpaper();
        }

        private void OnSafeWallpaperTriggered()
        {
            Dispatcher.Invoke(() =>
            {
                ShowToast("🛡", "Safe Wallpaper Restored!");
                if (StatusText != null) StatusText.Text = "Default safe wallpaper applied.";
            });
        }

        // ═════════════════════════════════════════════════════════════════
        // Window Chrome & Utility
        // ═════════════════════════════════════════════════════════════════

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // WindowChrome handles caption dragging; this is a harmless fallback.
            if (e.ChangedButton == MouseButton.Left)
            {
                try { DragMove(); } catch { }
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSection == ActiveSection.Home)
                _ = LoadHomeDataAsync();
            else if (_currentSection == ActiveSection.Explore)
                _ = DoExploreSearch(append: false);
            else
                DisplayCurrentSection();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var sw = new SettingsWindow { Owner = this };
            sw.ShowDialog();
            UpdatePanicButtonLabel();
            ApplyHomeSectionsVisibility();
            UpdateSfwToggleVisuals();
            ApplySfwModeToFilters();
        }

        public void ApplyHomeSectionsVisibility()
        {
            var s = Settings.Instance;
            bool anyVisible = false;

            if (HeroSpotlightCard != null)
            {
                HeroSpotlightCard.Visibility = s.ShowHomeHero ? Visibility.Visible : Visibility.Collapsed;
                if (s.ShowHomeHero) anyVisible = true;
            }
            if (HomeQuickControlsSection != null)
            {
                HomeQuickControlsSection.Visibility = s.ShowHomeQuickControls ? Visibility.Visible : Visibility.Collapsed;
                if (s.ShowHomeQuickControls) anyVisible = true;
            }
            if (HomeTrendingSection != null)
            {
                HomeTrendingSection.Visibility = s.ShowHomeTrending ? Visibility.Visible : Visibility.Collapsed;
                if (s.ShowHomeTrending) anyVisible = true;
            }
            if (HomeFranchisesSection != null)
            {
                HomeFranchisesSection.Visibility = s.ShowHomeFranchises ? Visibility.Visible : Visibility.Collapsed;
                if (s.ShowHomeFranchises) anyVisible = true;
            }
            if (HomeQuickShelfSection != null)
            {
                HomeQuickShelfSection.Visibility = s.ShowHomeQuickShelf ? Visibility.Visible : Visibility.Collapsed;
                if (s.ShowHomeQuickShelf) anyVisible = true;
            }

            if (HomeAllSectionsHiddenNotice != null)
            {
                HomeAllSectionsHiddenNotice.Visibility = anyVisible ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private void Hide_Click(object sender, RoutedEventArgs e)
        {
            AppSuspensionManager.EnterBackgroundMode(this);
        }

        private void OpenDownloadsFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string folder = DownloadsManager.Instance.DownloadFolder;
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                Process.Start(new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowToast("⚠", $"Cannot open folder: {ex.Message}");
            }
        }

        private void SetLoading(bool loading, string? text = null)
        {
            LoadingOverlay.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
            if (text != null) LoadingText.Text = text;
        }

        private async void ShowToast(string icon, string message)
        {
            // Cancel any previous toast that is still showing
            _toastCts.Cancel();
            _toastCts = new CancellationTokenSource();
            var ct = _toastCts.Token;

            if (ToastStatusIcon != null)
            {
                if (icon == "✓" || icon == "📥")
                {
                    if (TryFindResource("IconCheckGeo") is System.Windows.Media.Geometry checkGeo)
                        ToastStatusIcon.Data = checkGeo;
                    ToastBanner.Background = (System.Windows.Media.Brush)FindResource("AccentBrush");
                }
                else if (icon == "⚠" || icon == "✕")
                {
                    if (TryFindResource("IconAlertGeo") is System.Windows.Media.Geometry alertGeo)
                        ToastStatusIcon.Data = alertGeo;
                    ToastBanner.Background = (System.Windows.Media.Brush)FindResource("DangerBrush");
                }
                else if (icon == "🛡")
                {
                    if (TryFindResource("IconShieldGeo") is System.Windows.Media.Geometry shieldGeo)
                        ToastStatusIcon.Data = shieldGeo;
                    ToastBanner.Background = (System.Windows.Media.Brush)FindResource("EmeraldBrush");
                }
                else if (icon == "🔞")
                {
                    if (TryFindResource("IconFireGeo") is System.Windows.Media.Geometry fireGeo)
                        ToastStatusIcon.Data = fireGeo;
                    ToastBanner.Background = (System.Windows.Media.Brush)FindResource("DangerBrush");
                }
            }

            ToastIcon.Text = icon;
            ToastMessage.Text = message;
            if (ToastUndoBtn != null && _toastUndoAction == null)
                ToastUndoBtn.Visibility = Visibility.Collapsed;
            ToastBanner.Visibility = Visibility.Visible;

            try
            {
                await Task.Delay(_toastUndoAction != null ? 6000 : 2500, ct);
                ToastBanner.Visibility = Visibility.Collapsed;
                if (ToastUndoBtn != null) ToastUndoBtn.Visibility = Visibility.Collapsed;
                _toastUndoAction = null;
            }
            catch (OperationCanceledException) { /* replaced by newer toast — leave visible */ }
        }

        private Action? _toastUndoAction;

        /// <summary>Show a toast with an Undo button that invokes <paramref name="undo"/> when clicked.</summary>
        private void ShowToast(string icon, string message, Action undo)
        {
            _toastUndoAction = undo;
            if (ToastUndoBtn != null) ToastUndoBtn.Visibility = Visibility.Visible;
            ShowToast(icon, message);
        }

        private void ToastUndo_Click(object sender, RoutedEventArgs e)
        {
            var action = _toastUndoAction;
            _toastUndoAction = null;
            if (ToastUndoBtn != null) ToastUndoBtn.Visibility = Visibility.Collapsed;
            ToastBanner.Visibility = Visibility.Collapsed;
            action?.Invoke();
        }

        // ═════════════════════════════════════════════════════════════════
        // Home Page Dashboard Methods
        // ═════════════════════════════════════════════════════════════════

        private async Task LoadHomeDataAsync()
        {
            try
            {
                // Fetch top trending wallpapers for hero spotlight & trending row
                string homeSource = Settings.Instance.RatingMode == ContentRatingMode.SfwOnly ? "konasfw" : "yande";
                string homeRating = Settings.Instance.RatingMode switch
                {
                    ContentRatingMode.SfwOnly => "rating:s",
                    ContentRatingMode.Questionable => "rating:q",
                    _ => ""
                };
                string homeQuery = string.IsNullOrEmpty(homeRating) ? "order:score" : $"order:score {homeRating}";
                var rawPosts = await _api.FetchPostsAsync(homeSource, homeQuery, 1, 16, CancellationToken.None);
                var posts = FilterPosts(rawPosts);
                _trendingPosts = posts;

                // Priority: Show user's saved favorite artwork if available
                var favs = FavoritesManager.Instance.GetAllFavorites();
                if (favs.Count > 0)
                {
                    _showingFavoriteInHero = true;
                    _favoriteHeroIndex = 0;
                    _heroPost = favs[0];
                    PopulateHeroCard(_heroPost);
                }
                else if (posts.Count > 0)
                {
                    _showingFavoriteInHero = false;
                    _heroPost = posts[0];
                    PopulateHeroCard(_heroPost);
                }

                if (posts.Count > 0)
                {
                    var trendingSubset = posts.Skip(_showingFavoriteInHero ? 0 : 1).Take(4).ToList();
                    TrendingCard0.DataContext = trendingSubset.Count > 0 ? trendingSubset[0] : null;
                    TrendingCard1.DataContext = trendingSubset.Count > 1 ? trendingSubset[1] : null;
                    TrendingCard2.DataContext = trendingSubset.Count > 2 ? trendingSubset[2] : null;
                    TrendingCard3.DataContext = trendingSubset.Count > 3 ? trendingSubset[3] : null;
                }

                if (HomePanicKeyText != null)
                    HomePanicKeyText.Text = Settings.Instance.GetHotkeyDisplayName();

                UpdateSlideshowQuickToggleState();

                int screenW = (int)SystemParameters.PrimaryScreenWidth;
                int screenH = (int)SystemParameters.PrimaryScreenHeight;
                if (HomeDisplayInfoText != null)
                    HomeDisplayInfoText.Text = $"{screenW} × {screenH}";

                UpdateQuickShelf();
                InitializeCategories();
            }
            catch { }
        }

        private void PopulateHeroCard(PostItem item)
        {
            if (HeroTitleText == null || HeroTagsText == null || HeroResText == null || HeroRatingText == null || HeroImage == null) return;

            if (_showingFavoriteInHero)
            {
                HeroTitleText.Text = $"Your Favorite #{item.Id}";
                if (HeroSpotlightText != null) HeroSpotlightText.Text = "FAVORITE SPOTLIGHT";
                if (HeroSpotlightBadge != null)
                    HeroSpotlightBadge.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF4, 0x3F, 0x5E));
                if (HeroSpotlightIcon != null)
                    HeroSpotlightIcon.Data = (System.Windows.Media.Geometry)FindResource("IconHeartGeo");
                if (HeroSourceToggleText != null) HeroSourceToggleText.Text = "Show Trending";
                if (HeroSourceToggleIcon != null)
                {
                    HeroSourceToggleIcon.Data = (System.Windows.Media.Geometry)FindResource("IconFireGeo");
                    HeroSourceToggleIcon.Fill = (System.Windows.Media.Brush)FindResource("DangerBrush");
                }
            }
            else
            {
                HeroTitleText.Text = $"Spotlight Artwork #{item.Id}";
                if (HeroSpotlightText != null) HeroSpotlightText.Text = "SPOTLIGHT OF THE DAY";
                if (HeroSpotlightBadge != null)
                    HeroSpotlightBadge.Background = (System.Windows.Media.Brush)FindResource("AccentBrush");
                if (HeroSpotlightIcon != null)
                    HeroSpotlightIcon.Data = (System.Windows.Media.Geometry)FindResource("IconSparkleGeo");
                if (HeroSourceToggleText != null) HeroSourceToggleText.Text = "Show Favorite";
                if (HeroSourceToggleIcon != null)
                {
                    HeroSourceToggleIcon.Data = (System.Windows.Media.Geometry)FindResource("IconHeartGeo");
                    HeroSourceToggleIcon.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF4, 0x3F, 0x5E));
                }
            }

            HeroTagsText.Text = string.IsNullOrWhiteSpace(item.Tags) ? "anime art, high resolution wallpaper" : item.Tags.Replace(" ", " • ");
            HeroResText.Text = $"{item.ResolutionText} • {item.AspectRatioText}";
            HeroRatingText.Text = item.RatingDisplay;

            try
            {
                HeroRatingBadge.Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(item.RatingBadgeBackground));
            }
            catch { }

            UpdateHeroFavState();

            try
            {
                var bi = new System.Windows.Media.Imaging.BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(item.BestImageUrl, UriKind.RelativeOrAbsolute);
                bi.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bi.DecodePixelWidth = 1280;
                bi.EndInit();
                HeroImage.Source = bi;
            }
            catch { }
        }

        private void HeroSourceToggle_Click(object sender, RoutedEventArgs e)
        {
            var favs = FavoritesManager.Instance.GetAllFavorites();
            if (!_showingFavoriteInHero)
            {
                if (favs.Count > 0)
                {
                    _showingFavoriteInHero = true;
                    _favoriteHeroIndex = 0;
                    _heroPost = favs[0];
                    PopulateHeroCard(_heroPost);
                    ShowToast("♥", "Displaying your saved favorite!");
                }
                else
                {
                    ShowToast("♥", "No favorites yet — click ♥ on any wallpaper!");
                }
            }
            else
            {
                if (favs.Count > 1 && _favoriteHeroIndex + 1 < favs.Count)
                {
                    _favoriteHeroIndex++;
                    _heroPost = favs[_favoriteHeroIndex];
                    PopulateHeroCard(_heroPost);
                    ShowToast("♥", $"Favorite {_favoriteHeroIndex + 1} of {favs.Count}");
                }
                else if (_trendingPosts.Count > 0)
                {
                    _showingFavoriteInHero = false;
                    _heroPost = _trendingPosts[0];
                    PopulateHeroCard(_heroPost);
                    ShowToast("⚡", "Displaying trending community spotlight!");
                }
            }
        }

        private void UpdateHeroFavState()
        {
            if (_heroPost == null || HeroFavIcon == null) return;
            if (_heroPost.IsFavorite)
            {
                HeroFavIcon.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF4, 0x3F, 0x5E));
            }
            else
            {
                HeroFavIcon.Fill = System.Windows.Media.Brushes.White;
            }
        }

        private void UpdateQuickShelf()
        {
            if (QuickShelfGrid == null || QuickShelfEmpty == null) return;

            var shelfItems = FavoritesManager.Instance.GetAllFavorites();
            if (shelfItems.Count == 0)
            {
                shelfItems = DownloadsManager.Instance.GetAllDownloads();
            }

            if (shelfItems.Count == 0)
            {
                QuickShelfEmpty.Visibility = Visibility.Visible;
                QuickShelfGrid.Visibility = Visibility.Collapsed;
            }
            else
            {
                QuickShelfEmpty.Visibility = Visibility.Collapsed;
                QuickShelfGrid.Visibility = Visibility.Visible;

                ShelfCard0.Visibility = shelfItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                ShelfCard0.DataContext = shelfItems.Count > 0 ? shelfItems[0] : null;

                ShelfCard1.Visibility = shelfItems.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
                ShelfCard1.DataContext = shelfItems.Count > 1 ? shelfItems[1] : null;

                ShelfCard2.Visibility = shelfItems.Count > 2 ? Visibility.Visible : Visibility.Collapsed;
                ShelfCard2.DataContext = shelfItems.Count > 2 ? shelfItems[2] : null;

                ShelfCard3.Visibility = shelfItems.Count > 3 ? Visibility.Visible : Visibility.Collapsed;
                ShelfCard3.DataContext = shelfItems.Count > 3 ? shelfItems[3] : null;
            }
        }

        private async void HeroApply_Click(object sender, RoutedEventArgs e)
        {
            if (_heroPost == null) return;
            try
            {
                ShowToast("⏳", "Applying wallpaper…");
                await WallpaperManager.Instance.ApplyWallpaperAsync(_heroPost);
                ShowToast("✓", "Wallpaper applied to desktop!");
            }
            catch (Exception ex)
            {
                ShowToast("⚠", $"Failed: {ex.Message}");
            }
        }

        private void HeroPreview_Click(object sender, RoutedEventArgs e)
        {
            if (_heroPost != null)
                WallpaperGrid_PreviewRequested(this, _heroPost);
        }

        private void HeroFav_Click(object sender, RoutedEventArgs e)
        {
            if (_heroPost == null) return;
            FavoritesManager.Instance.ToggleFavorite(_heroPost);
            UpdateHeroFavState();
            UpdateQuickShelf();
            ShowToast(_heroPost.IsFavorite ? "♥" : "✕", _heroPost.IsFavorite ? "Added to Favorites" : "Removed from Favorites");
        }

        private static readonly Dictionary<string, (string Name, string Type)> FranchiseMetadata = new(StringComparer.OrdinalIgnoreCase)
        {
            // Anime
            ["sousou_no_frieren"] = ("Frieren", "Anime"),
            ["bocchi_the_rock!"] = ("Bocchi the Rock!", "Anime"),
            ["chainsaw_man"] = ("Chainsaw Man", "Anime"),
            ["fate/stay_night"] = ("Fate / Stay Night", "Anime"),
            ["kimetsu_no_yaiba"] = ("Demon Slayer", "Anime"),
            ["neon_genesis_evangelion"] = ("Evangelion", "Anime"),
            ["sword_art_online"] = ("Sword Art Online", "Anime"),
            ["cyberpunk:_edgerunners"] = ("Cyberpunk: Edgerunners", "Anime"),
            ["spy_x_family"] = ("Spy × Family", "Anime"),
            ["oshi_no_ko"] = ("Oshi no Ko", "Anime"),
            ["vocaloid"] = ("Vocaloid", "Anime"),
            ["jujutsu_kaisen"] = ("Jujutsu Kaisen", "Anime"),
            ["shingeki_no_kyojin"] = ("Attack on Titan", "Anime"),
            ["re:zero_kara_hajimeru_isekai_seikatsu"] = ("Re:Zero", "Anime"),
            ["mahou_shoujo_madoka_magica"] = ("Madoka Magica", "Anime"),
            ["lycoris_recoil"] = ("Lycoris Recoil", "Anime"),
            ["dungeon_meshi"] = ("Delicious in Dungeon", "Anime"),
            ["one_piece"] = ("One Piece", "Anime"),
            ["bleach"] = ("Bleach", "Anime"),
            ["naruto"] = ("Naruto", "Anime"),
            ["dragon_ball"] = ("Dragon Ball", "Anime"),
            ["k-on!"] = ("K-On!", "Anime"),
            ["toaru_majutsu_no_index"] = ("A Certain Magical Index", "Anime"),
            ["toaru_kagaku_no_railgun"] = ("A Certain Scientific Railgun", "Anime"),

            // Games
            ["genshin_impact"] = ("Genshin Impact", "Game"),
            ["honkai:_star_rail"] = ("Honkai: Star Rail", "Game"),
            ["blue_archive"] = ("Blue Archive", "Game"),
            ["nier"] = ("NieR", "Game"),
            ["nier:_automata"] = ("NieR: Automata", "Game"),
            ["arknights"] = ("Arknights", "Game"),
            ["fate/grand_order"] = ("Fate / Grand Order", "Game"),
            ["elden_ring"] = ("Elden Ring", "Game"),
            ["touhou"] = ("Touhou Project", "Game"),
            ["hololive"] = ("Hololive", "Game"),
            ["azur_lane"] = ("Azur Lane", "Game"),
            ["kantai_collection"] = ("Kantai Collection", "Game"),
            ["the_idolm@ster"] = ("The Idolmaster", "Game"),
            ["idolmaster"] = ("The Idolmaster", "Game"),
            ["zenless_zone_zero"] = ("Zenless Zone Zero", "Game"),
            ["wuthering_waves"] = ("Wuthering Waves", "Game"),
            ["persona_5"] = ("Persona 5", "Game"),
            ["persona"] = ("Persona", "Game"),
            ["granblue_fantasy"] = ("Granblue Fantasy", "Game"),
            ["umamusume"] = ("Uma Musume", "Game"),
            ["umamusume_pretty_derby"] = ("Uma Musume", "Game"),
            ["goddess_of_victory:_nikke"] = ("Nikke", "Game"),
            ["honkai_impact_3rd"] = ("Honkai Impact 3rd", "Game"),
            ["pokemon"] = ("Pokémon", "Game"),
            ["league_of_legends"] = ("League of Legends", "Game")
        };

        private static string FormatSeriesTitle(string tag)
        {
            if (FranchiseMetadata.TryGetValue(tag, out var meta))
                return meta.Name;

            string clean = tag.Replace('_', ' ').Replace(":", " ").Trim();
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(clean);
        }

        private static string FormatSeriesType(string tag)
        {
            if (FranchiseMetadata.TryGetValue(tag, out var meta))
                return meta.Type;

            return "Anime";
        }

        private CancellationTokenSource? _categoryRefreshCts;
        private readonly List<SeriesCategoryItem> _currentSourceCategories = new();

        private async Task RefreshCategoriesForCurrentSourceAsync()
        {
            _categoryRefreshCts?.Cancel();
            _categoryRefreshCts = new CancellationTokenSource();
            var ct = _categoryRefreshCts.Token;

            string sourceKey = _currentSource;
            bool sfw = Settings.Instance.SfwOnlyMode;
            string sourceDisplayName = sourceKey switch
            {
                "konasfw" => "konachan.net (SFW)",
                "zerochan" => "Zerochan (SFW)",
                "safebooru" => "Safebooru (SFW)",
                "waifuim" => sfw ? "waifu.im (SFW)" : "waifu.im (Unrestricted)",
                "gelbooru" => sfw ? "Gelbooru (SFW)" : "Gelbooru (Unrestricted)",
                "danbooru" => sfw ? "Danbooru (SFW)" : "Danbooru (Unrestricted)",
                "konansfw" => "konachan.com (Unrestricted)",
                "yande" => sfw ? "yande.re (SFW)" : "yande.re (Unrestricted)",
                "all" => sfw ? "All Sources (SFW)" : "All Sources (Unrestricted)",
                _ => sfw ? $"{sourceKey} (SFW)" : sourceKey
            };

            if (CategorySourceSubtitle != null)
            {
                CategorySourceSubtitle.Text = $"Live Ranking • {sourceDisplayName}";
            }

            try
            {
                var rawTags = await _api.GetPopularSeriesFromSourceAsync(sourceKey, 24, ct);
                if (ct.IsCancellationRequested) return;

                if (rawTags != null && rawTags.Count > 0)
                {
                    var dynamicList = new List<SeriesCategoryItem>();
                    foreach (var (tag, count) in rawTags)
                    {
                        string name = FormatSeriesTitle(tag);
                        string type = FormatSeriesType(tag);

                        // If SFW mode, only use curated Konachan preview if it matches, otherwise let background fetcher get SFW image
                        var existingCurated = _curatedCategories.FirstOrDefault(c => c.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase));
                        string previewUrl = existingCurated?.PreviewImageUrl ?? "";

                        var item = new SeriesCategoryItem
                        {
                            Name = name,
                            Tag = tag,
                            Type = type,
                            PostCount = count,
                            PreviewImageUrl = previewUrl,
                            IsCustom = false
                        };
                        dynamicList.Add(item);
                    }

                    _currentSourceCategories.Clear();
                    _currentSourceCategories.AddRange(dynamicList);
                    InitializeCategories();

                    // Background thumbnail enrichment: sync artwork directly to active source and SFW/NSFW rating
                    _ = Task.Run(async () =>
                    {
                        foreach (var item in dynamicList)
                        {
                            if (ct.IsCancellationRequested) break;
                            try
                            {
                                var preview = await _api.GetSourceCategoryPreviewUrlAsync(sourceKey, item.Tag, ct);
                                if (!string.IsNullOrEmpty(preview) && preview != item.PreviewImageUrl)
                                {
                                    Dispatcher.Invoke(() => item.PreviewImageUrl = preview);
                                }
                            }
                            catch { }
                        }
                    }, ct);

                    return;
                }
            }
            catch { }

            // Graceful fallback to curated catalog
            if (_currentSourceCategories.Count == 0)
            {
                _currentSourceCategories.Clear();
                _currentSourceCategories.AddRange(_curatedCategories);
            }
            InitializeCategories();
        }

        private void InitializeCategories()
        {
            if (CategoriesItemsControl == null) return;

            var sourceList = _currentSourceCategories.Count > 0 ? _currentSourceCategories : _curatedCategories;
            var all = new List<SeriesCategoryItem>(sourceList);
            if (Settings.Instance.CustomCategories != null)
            {
                all.AddRange(Settings.Instance.CustomCategories);
            }

            IEnumerable<SeriesCategoryItem> filtered = _currentCategoryFilter switch
            {
                "Anime" => all.Where(c => c.Type == "Anime"),
                "Games" => all.Where(c => c.Type == "Game"),
                "Custom" => all.Where(c => c.IsCustom),
                _ => all
            };

            CategoriesItemsControl.ItemsSource = filtered.ToList();
        }

        private void CategoryType_Checked(object sender, RoutedEventArgs e)
        {
            if (CategoryFilterAnime?.IsChecked == true)
                _currentCategoryFilter = "Anime";
            else if (CategoryFilterGames?.IsChecked == true)
                _currentCategoryFilter = "Games";
            else if (CategoryFilterCustom?.IsChecked == true)
                _currentCategoryFilter = "Custom";
            else
                _currentCategoryFilter = "All";

            InitializeCategories();
        }

        private void CategoryCardGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is UIElement elem && e.NewSize.Width > 0 && e.NewSize.Height > 0)
            {
                elem.Clip = new System.Windows.Media.RectangleGeometry(
                    new Rect(0, 0, e.NewSize.Width, e.NewSize.Height), 8, 8);
            }
        }

        private void CategoryPill_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.DataContext is SeriesCategoryItem item)
            {
                NavExplore.IsChecked = true;
                _suppressAutocomplete = true;
                try
                {
                    _autocompleteCts.Cancel();
                    AutocompletePopup.IsOpen = false;
                    TagSearchBox.Text = item.Tag;
                }
                finally
                {
                    _suppressAutocomplete = false;
                }
                _ = DoExploreSearch(append: false);
            }
        }

        private void NewCategoryNameBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (NewCategoryNamePlaceholder != null && NewCategoryNameBox != null)
            {
                NewCategoryNamePlaceholder.Visibility = string.IsNullOrEmpty(NewCategoryNameBox.Text)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void NewCategoryTagBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (NewCategoryTagPlaceholder != null && NewCategoryTagBox != null)
            {
                NewCategoryTagPlaceholder.Visibility = string.IsNullOrEmpty(NewCategoryTagBox.Text)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void AddCategoryToggle_Click(object sender, RoutedEventArgs e)
        {
            if (AddCategoryFormBorder == null) return;
            AddCategoryFormBorder.Visibility = AddCategoryFormBorder.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;

            if (AddCategoryFormBorder.Visibility == Visibility.Visible && NewCategoryNameBox != null)
            {
                NewCategoryNameBox.Focus();
            }
        }

        private void CancelCustomCategory_Click(object sender, RoutedEventArgs e)
        {
            if (AddCategoryFormBorder != null)
                AddCategoryFormBorder.Visibility = Visibility.Collapsed;
        }

        private async void SaveCustomCategory_Click(object sender, RoutedEventArgs e)
        {
            string name = NewCategoryNameBox?.Text?.Trim() ?? "";
            string tag = NewCategoryTagBox?.Text?.Trim() ?? "";
            string type = (NewCategoryTypeCombo?.SelectedItem as ComboBoxItem)?.Tag as string ?? "Custom";

            if (string.IsNullOrWhiteSpace(name))
            {
                ShowToast("⚠", "Please enter a category name");
                return;
            }

            if (string.IsNullOrWhiteSpace(tag))
            {
                tag = name.ToLower().Replace(" ", "_").Replace(":", "");
            }
            else
            {
                tag = tag.ToLower().Replace(" ", "_");
            }

            var customItem = new SeriesCategoryItem
            {
                Name = name,
                Tag = tag,
                Type = type,
                IsCustom = true
            };

            try
            {
                var preview = await _api.GetCategoryPreviewUrlAsync(tag);
                if (!string.IsNullOrEmpty(preview))
                    customItem.PreviewImageUrl = preview;
            }
            catch { }

            Settings.Instance.CustomCategories.Add(customItem);
            Settings.Instance.Save();

            if (NewCategoryNameBox != null) NewCategoryNameBox.Text = "";
            if (NewCategoryTagBox != null) NewCategoryTagBox.Text = "";
            if (AddCategoryFormBorder != null) AddCategoryFormBorder.Visibility = Visibility.Collapsed;

            InitializeCategories();
            ShowToast("✓", $"Category '{name}' added!");
        }

        private void DeleteCustomCategory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement btn && btn.Tag is string id)
            {
                var item = Settings.Instance.CustomCategories.FirstOrDefault(c => c.Id == id);
                if (item != null)
                {
                    Settings.Instance.CustomCategories.Remove(item);
                    Settings.Instance.Save();
                    InitializeCategories();
                    ShowToast("✓", $"Category '{item.Name}' removed");
                }
            }
        }

        private void UniversePill_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement btn && btn.Tag is string tag)
            {
                NavExplore.IsChecked = true;
                TagSearchBox.Text = tag;
                _ = DoExploreSearch(append: false);
            }
        }

        private void ExploreMoreTrending_Click(object sender, RoutedEventArgs e)
        {
            NavExplore.IsChecked = true;
            TagSearchBox.Text = "order:score";
            _ = DoExploreSearch(append: false);
        }

        private void ViewFavorites_Click(object sender, RoutedEventArgs e)
        {
            NavFavorites.IsChecked = true;
        }

        private void QuickFilter4K_Click(object sender, RoutedEventArgs e)
        {
            NavExplore.IsChecked = true;
            if (ResolutionBox != null) ResolutionBox.SelectedIndex = 3; // 4K UHD (3840+)
            _ = DoExploreSearch(append: false);
        }

        private void QuickFilterUltrawide_Click(object sender, RoutedEventArgs e)
        {
            NavExplore.IsChecked = true;
            if (AspectBox != null) AspectBox.SelectedIndex = 2; // Ultrawide (21:9+)
            _ = DoExploreSearch(append: false);
        }

        private void QuickFilterAll_Click(object sender, RoutedEventArgs e)
        {
            NavExplore.IsChecked = true;
            if (ResolutionBox != null) ResolutionBox.SelectedIndex = 0;
            if (AspectBox != null) AspectBox.SelectedIndex = 0;
            _ = DoExploreSearch(append: false);
        }

        private void HeroSpotlightGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is UIElement elem && e.NewSize.Width > 0 && e.NewSize.Height > 0)
            {
                elem.Clip = new System.Windows.Media.RectangleGeometry(
                    new Rect(0, 0, e.NewSize.Width, e.NewSize.Height), 12, 12);
            }
        }

        private void PreviewCardGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is UIElement elem && e.NewSize.Width > 0 && e.NewSize.Height > 0)
            {
                elem.Clip = new System.Windows.Media.RectangleGeometry(
                    new Rect(0, 0, e.NewSize.Width, e.NewSize.Height), 12, 12);
            }
        }
    }
}
