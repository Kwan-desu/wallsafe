package com.wallsafe.feature.downloads

import android.app.WallpaperManager
import android.content.Context
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.wallsafe.core.data.repository.DownloadsRepository
import com.wallsafe.core.database.entity.DownloadEntity
import com.wallsafe.core.model.PostItem
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
class DownloadsViewModel @Inject constructor(
    private val downloadsRepository: DownloadsRepository,
    private val wallpaperEngine: WallpaperEngine,
    @ApplicationContext private val context: Context
) : ViewModel() {

    private val _uiState = MutableStateFlow(DownloadsUiState())
    val uiState: StateFlow<DownloadsUiState> = _uiState.asStateFlow()

    init {
        observeDownloads()
    }

    private fun observeDownloads() {
        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true) }
            downloadsRepository.getDownloads().collect { entities ->
                val posts = entities.map { entity ->
                    PostItem(
                        id = entity.postId,
                        source = entity.source,
                        sourceUrl = entity.fileUrl ?: entity.sampleUrl ?: entity.previewUrl,
                        previewUrl = entity.previewUrl,
                        sampleUrl = entity.sampleUrl ?: entity.previewUrl,
                        fileUrl = entity.fileUrl ?: entity.localUri,
                        width = entity.width,
                        height = entity.height,
                        rating = entity.rating,
                        score = entity.score,
                        tags = entity.tags,
                        localPath = entity.localUri
                    )
                }
                _uiState.update { it.copy(downloads = posts, isLoading = false) }
            }
        }
    }

    fun handleIntent(intent: DownloadsIntent) {
        when (intent) {
            is DownloadsIntent.DeleteDownload -> {
                viewModelScope.launch {
                    val id = intent.postId.toIntOrNull() ?: return@launch
                    downloadsRepository.deleteDownload(id, intent.source)
                }
            }
            is DownloadsIntent.ApplyWallpaper -> {
                viewModelScope.launch {
                    val url = intent.post.localPath ?: intent.post.bestImageUrl
                    wallpaperEngine.applyWallpaper(context, url, WallpaperManager.FLAG_SYSTEM or WallpaperManager.FLAG_LOCK)
                }
            }
            is DownloadsIntent.OpenInGallery -> {
                // Handled in UI
            }
        }
    }
}
