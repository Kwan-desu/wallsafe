package com.wallsafe.feature.downloads

import com.wallsafe.core.model.PostItem

data class DownloadsUiState(
    val downloads: List<PostItem> = emptyList(),
    val isLoading: Boolean = true
)

sealed interface DownloadsIntent {
    data class DeleteDownload(val postId: String, val source: String) : DownloadsIntent
    data class ApplyWallpaper(val post: PostItem) : DownloadsIntent
    data class OpenInGallery(val post: PostItem) : DownloadsIntent
}
