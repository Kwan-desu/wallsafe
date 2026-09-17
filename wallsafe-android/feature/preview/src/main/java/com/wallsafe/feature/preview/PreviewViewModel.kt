package com.wallsafe.feature.preview

import android.app.WallpaperManager
import android.content.Context
import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.wallsafe.core.data.repository.BooruRepository
import com.wallsafe.core.data.repository.DownloadsRepository
import com.wallsafe.core.data.repository.FavoritesRepository
import com.wallsafe.core.model.PostItem
import com.wallsafe.core.wallpaper.DownloadManager
import com.wallsafe.core.wallpaper.WallpaperEngine
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

@HiltViewModel
class PreviewViewModel @Inject constructor(
    savedStateHandle: SavedStateHandle,
    @ApplicationContext private val context: Context,
    private val booruRepository: BooruRepository,
    private val favoritesRepository: FavoritesRepository,
    private val downloadsRepository: DownloadsRepository,
    private val wallpaperEngine: WallpaperEngine,
    private val downloadManager: DownloadManager
) : ViewModel() {

    private val postId: Int = when (val raw = savedStateHandle.get<Any>("postId")) {
        is Int -> raw
        is String -> raw.toIntOrNull() ?: 0
        is Number -> raw.toInt()
        else -> 0
    }
    private val source: String = savedStateHandle.get<String>("source") ?: "all"

    private val _uiState = MutableStateFlow(PreviewUiState())
    val uiState: StateFlow<PreviewUiState> = _uiState.asStateFlow()

    init {
        loadPost()
        observeFavoriteStatus()
        observeDownloadStatus()
    }

    private fun loadPost() {
        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true, error = null) }
            try {
                var post: PostItem? = null
                if (source != "all" && source.isNotBlank()) {
                    val posts = booruRepository.fetchPosts(source, "id:$postId", 1, 1)
                    post = posts.firstOrNull()
                }
                if (post == null) {
                    val posts = booruRepository.fetchAllSources("id:$postId", 1, 2)
                    post = posts.firstOrNull { it.id == postId } ?: posts.firstOrNull()
                }
                _uiState.update { it.copy(post = post, isLoading = false) }
            } catch (e: Exception) {
                _uiState.update { it.copy(error = e.message ?: "Failed to load post", isLoading = false) }
            }
        }
    }

    private fun observeFavoriteStatus() {
        viewModelScope.launch {
            favoritesRepository.getAllFavorites().collect { favorites ->
                val currentPost = _uiState.value.post
                val targetSource = currentPost?.source ?: source
                val isFav = favorites.any { it.postId == postId && (targetSource == "all" || it.source == targetSource || it.source == source) }
                _uiState.update { it.copy(isFavorite = isFav) }
            }
        }
    }

    private fun observeDownloadStatus() {
        viewModelScope.launch {
            downloadsRepository.isDownloaded(postId, source).collect { downloaded ->
                _uiState.update { it.copy(isDownloaded = downloaded) }
            }
        }
    }

    fun handleIntent(intent: PreviewIntent) {
        when (intent) {
            is PreviewIntent.ApplyAsHomeWallpaper -> applyWallpaper(WallpaperManager.FLAG_SYSTEM)
            is PreviewIntent.ApplyAsLockWallpaper -> applyWallpaper(WallpaperManager.FLAG_LOCK)
            is PreviewIntent.ApplyBothWallpaper -> applyWallpaper(WallpaperManager.FLAG_SYSTEM or WallpaperManager.FLAG_LOCK)
            is PreviewIntent.ToggleFavorite -> toggleFavorite()
            is PreviewIntent.Download -> downloadWallpaper()
            is PreviewIntent.OpenInBrowser -> { /* Handled in UI */ }
            is PreviewIntent.Dismiss -> { /* Handled in UI */ }
            is PreviewIntent.ClearUserMessage -> {
                _uiState.update { it.copy(userMessage = null) }
            }
        }
    }

    private fun applyWallpaper(flag: Int) {
        val post = _uiState.value.post ?: return
        viewModelScope.launch {
            _uiState.update { it.copy(isApplyingWallpaper = true) }
            val screenName = when (flag) {
                WallpaperManager.FLAG_SYSTEM -> "Home Screen"
                WallpaperManager.FLAG_LOCK -> "Lock Screen"
                else -> "Home & Lock Screens"
            }
            try {
                val url = post.sampleUrl ?: post.fileUrl ?: post.previewUrl
                val success = wallpaperEngine.applyWallpaper(context, url, flag)
                if (success) {
                    _uiState.update {
                        it.copy(
                            isApplyingWallpaper = false,
                            userMessage = "Wallpaper successfully applied to $screenName!"
                        )
                    }
                } else {
                    _uiState.update {
                        it.copy(
                            isApplyingWallpaper = false,
                            userMessage = "Failed to apply wallpaper to $screenName. Please try again."
                        )
                    }
                }
            } catch (e: Exception) {
                _uiState.update {
                    it.copy(
                        isApplyingWallpaper = false,
                        userMessage = "Error setting wallpaper: ${e.localizedMessage ?: "Unknown error"}"
                    )
                }
            }
        }
    }

    private fun toggleFavorite() {
        val post = _uiState.value.post ?: return
        val newFav = !_uiState.value.isFavorite
        _uiState.update { it.copy(isFavorite = newFav) }
        viewModelScope.launch {
            favoritesRepository.toggleFavorite(post)
        }
    }

    private fun downloadWallpaper() {
        val post = _uiState.value.post ?: return
        viewModelScope.launch {
            _uiState.update { it.copy(isDownloading = true, downloadProgress = 0f) }
            try {
                val uri = downloadManager.downloadWallpaper(context, post) { progress ->
                    _uiState.update { it.copy(downloadProgress = progress) }
                }
                if (uri != null) {
                    downloadsRepository.recordDownload(post, uri.toString())
                    _uiState.update {
                        it.copy(
                            isDownloading = false,
                            downloadProgress = null,
                            isDownloaded = true,
                            userMessage = "Wallpaper saved to Pictures/WallSafe!"
                        )
                    }
                } else {
                    _uiState.update {
                        it.copy(
                            isDownloading = false,
                            downloadProgress = null,
                            userMessage = "Download failed. Please check your network connection."
                        )
                    }
                }
            } catch (e: Exception) {
                _uiState.update {
                    it.copy(
                        isDownloading = false,
                        downloadProgress = null,
                        userMessage = "Download error: ${e.localizedMessage ?: "Failed to save"}"
                    )
                }
            }
        }
    }
}
