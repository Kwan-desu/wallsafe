package com.wallsafe.feature.preview

import com.wallsafe.core.model.PostItem

data class PreviewUiState(
    val post: PostItem? = null,
    val isLoading: Boolean = true,
    val isFavorite: Boolean = false,
    val isDownloaded: Boolean = false,
    val isApplyingWallpaper: Boolean = false,
    val isDownloading: Boolean = false,
    val downloadProgress: Float? = null,
    val userMessage: String? = null,
    val error: String? = null
)

sealed interface PreviewIntent {
    data object ApplyAsHomeWallpaper : PreviewIntent
    data object ApplyAsLockWallpaper : PreviewIntent
    data object ApplyBothWallpaper : PreviewIntent
    data object ApplyWithSystemCropper : PreviewIntent
    data class BlacklistTag(val tag: String) : PreviewIntent
    data object ToggleFavorite : PreviewIntent
    data object Download : PreviewIntent
    data object OpenInBrowser : PreviewIntent
    data object Dismiss : PreviewIntent
    data object ClearUserMessage : PreviewIntent
}
