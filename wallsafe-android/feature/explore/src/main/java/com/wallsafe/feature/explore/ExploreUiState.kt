package com.wallsafe.feature.explore

import com.wallsafe.core.model.ContentRatingMode
import com.wallsafe.core.model.PostItem
import com.wallsafe.core.model.TagSuggestion

data class ExploreUiState(
    val searchQuery: String = "",
    val selectedSource: String = "all",
    val ratingMode: ContentRatingMode = ContentRatingMode.SfwOnly,
    val sortOrder: String = "score",
    val resolutionFilter: Int = 0,
    val aspectRatioFilter: Int = 0,
    val suggestions: List<TagSuggestion> = emptyList(),
    val isSuggestionsVisible: Boolean = false,
    val isSearchActive: Boolean = false,
    val isSearchFocused: Boolean = false,
    val availableSources: List<Pair<String, String>> = listOf(
        "all" to "All Sources",
        "danbooru" to "Danbooru",
        "safebooru" to "Safebooru",
        "gelbooru" to "Gelbooru"
    ),
    val favoriteKeys: Set<String> = emptySet(),
    val isDiscretionBlur: Boolean = false,
    val isWallpaperTagOnly: Boolean = false,
    val tagBlacklist: Set<String> = emptySet()
)

sealed interface ExploreIntent {
    data class UpdateSearchQuery(val query: String) : ExploreIntent
    data object Search : ExploreIntent
    data class SelectSuggestion(val suggestionTag: String) : ExploreIntent
    data class SelectSource(val sourceId: String) : ExploreIntent
    data class SetRatingMode(val mode: ContentRatingMode) : ExploreIntent
    data class SetSortOrder(val order: String) : ExploreIntent
    data class SetResolutionFilter(val index: Int) : ExploreIntent
    data class SetAspectRatioFilter(val index: Int) : ExploreIntent
    data class ToggleWallpaperTag(val enabled: Boolean) : ExploreIntent
    data class ToggleFavorite(val post: PostItem) : ExploreIntent
    data class ApplyWallpaper(val post: PostItem) : ExploreIntent
    data class DownloadWallpaper(val post: PostItem) : ExploreIntent
    data object DismissSuggestions : ExploreIntent
    data class ToggleSearchActive(val active: Boolean) : ExploreIntent
    data class SetSearchFocused(val focused: Boolean) : ExploreIntent
}
