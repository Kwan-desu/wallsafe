package com.wallsafe.feature.home

import com.wallsafe.core.model.PostItem
import com.wallsafe.core.model.SeriesCategoryItem

/**
 * UI State for the Home Screen
 */
data class HomeUiState(
    val userName: String = "",
    val isGreetingNameEnabled: Boolean = true,
    val showNamePromptDialog: Boolean = false,
    val themeMode: String = "system",
    val selectedFilter: String = "all", // "all", "portrait", "landscape"
    val trendingPosts: List<PostItem> = emptyList(),
    val popularSeries: List<SeriesCategoryItem> = emptyList(),
    val recentFavorites: List<PostItem> = emptyList(),
    val favoriteKeys: Set<String> = emptySet(),
    val isLoading: Boolean = true,
    val currentWallpaperInfo: String? = null,
    val isDiscretionBlur: Boolean = false,
    val showHero: Boolean = true,
    val showTrending: Boolean = true,
    val showFranchises: Boolean = true,
    val error: String? = null
)

/**
 * Actions that can be performed on the Home Screen
 */
sealed interface HomeIntent {
    data object Refresh : HomeIntent
    data object NavigateToExplore : HomeIntent
    data class NavigateToSeries(val tag: String) : HomeIntent
    data object ApplyRandomWallpaper : HomeIntent
    data class ToggleFavorite(val post: PostItem) : HomeIntent
    data class ApplyWallpaper(val post: PostItem) : HomeIntent
    data class Download(val post: PostItem) : HomeIntent
    data object DismissHero : HomeIntent
    data object ToggleTheme : HomeIntent
    data class SetFilter(val filter: String) : HomeIntent
    data class SetUserName(val name: String) : HomeIntent
    data class DismissNamePrompt(val enteredName: String?) : HomeIntent
    data class ToggleGreetingName(val enabled: Boolean) : HomeIntent
}
