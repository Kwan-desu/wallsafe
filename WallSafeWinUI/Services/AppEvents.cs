using System;

namespace WallSafeWinUI.Services
{
    /// <summary>
    /// App-wide lightweight event bus so unrelated pages can react to global state
    /// changes (e.g. the rating mode changing in the title bar should refresh the
    /// Explore and Home grids).
    /// </summary>
    public static class AppEvents
    {
        /// <summary>Raised when the content rating mode (SFW/Questionable/Explicit) changes.</summary>
        public static event Action<ContentRatingMode>? RatingModeChanged;

        public static void RaiseRatingModeChanged(ContentRatingMode mode)
            => RatingModeChanged?.Invoke(mode);

        /// <summary>Raised when discretion blur mode is toggled.</summary>
        public static event Action<bool>? DiscretionBlurChanged;

        public static void RaiseDiscretionBlurChanged(bool enabled)
            => DiscretionBlurChanged?.Invoke(enabled);

        /// <summary>Raised to request navigating to Explore filtered by a tag (franchise click).</summary>
        public static event Action<string>? ExploreTagRequested;

        public static void RequestExploreTag(string tag)
            => ExploreTagRequested?.Invoke(tag);
    }
}
