package com.wallsafe.feature.home

import android.app.WallpaperManager
import android.content.Context
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.wallsafe.core.data.repository.BooruRepository
import com.wallsafe.core.data.repository.DownloadsRepository
import com.wallsafe.core.data.repository.FavoritesRepository
import com.wallsafe.core.data.repository.SettingsRepository
import com.wallsafe.core.model.ContentRatingMode
import com.wallsafe.core.model.PostItem
import com.wallsafe.core.wallpaper.DownloadManager
import com.wallsafe.core.wallpaper.WallpaperEngine
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

@HiltViewModel
class HomeViewModel @Inject constructor(
    @ApplicationContext private val context: Context,
    private val booruRepository: BooruRepository,
    private val favoritesRepository: FavoritesRepository,
    private val downloadsRepository: DownloadsRepository,
    private val settingsRepository: SettingsRepository,
    private val wallpaperEngine: WallpaperEngine,
    private val downloadManager: DownloadManager
) : ViewModel() {

    private val _uiState = MutableStateFlow(HomeUiState())
    val uiState: StateFlow<HomeUiState> = _uiState.asStateFlow()
    private var currentRatingMode: ContentRatingMode = ContentRatingMode.SfwOnly
    private var hasPromptedForNameInSession = false

    init {
        observeFavorites()
        observeDownloads()
        observeSettings()
        loadData()
    }

    private fun observeDownloads() {
        viewModelScope.launch {
            downloadsRepository.getDownloads().collect { downloads ->
                _uiState.update { it.copy(downloadsCount = downloads.size) }
            }
        }
    }

    private fun observeSettings() {
        viewModelScope.launch {
            settingsRepository.userName.collect { name ->
                _uiState.update { it.copy(userName = name) }
            }
        }
        viewModelScope.launch {
            settingsRepository.themeMode.collect { mode ->
                _uiState.update { it.copy(themeMode = mode) }
            }
        }
        viewModelScope.launch {
            settingsRepository.isDiscretionBlurEnabled.collect { blurEnabled ->
                _uiState.update { it.copy(isDiscretionBlur = blurEnabled) }
            }
        }
        viewModelScope.launch {
            settingsRepository.ratingMode.collect { mode ->
                currentRatingMode = mode
                loadData(mode)
            }
        }
        viewModelScope.launch {
            settingsRepository.showHomeHero.collect { show ->
                _uiState.update { it.copy(showHero = show) }
            }
        }
        viewModelScope.launch {
            settingsRepository.showHomeTrending.collect { show ->
                _uiState.update { it.copy(showTrending = show) }
            }
        }
        viewModelScope.launch {
            settingsRepository.showHomeFranchises.collect { show ->
                _uiState.update { it.copy(showFranchises = show) }
            }
        }
        viewModelScope.launch {
            settingsRepository.isGreetingNameEnabled.collect { enabled ->
                _uiState.update { it.copy(isGreetingNameEnabled = enabled) }
            }
        }
        viewModelScope.launch {
            settingsRepository.appliedWallpapersCount.collect { count ->
                _uiState.update { it.copy(appliedCount = count) }
            }
        }
        viewModelScope.launch {
            settingsRepository.hasAgreedToTerms.collect { agreed ->
                if (agreed && !hasPromptedForNameInSession) {
                    val alreadyPrompted = settingsRepository.hasPromptedForName.first()
                    val existingName = settingsRepository.userName.first()
                    if (!alreadyPrompted && existingName.isBlank() && !hasPromptedForNameInSession) {
                        hasPromptedForNameInSession = true
                        _uiState.update { it.copy(showNamePromptDialog = true) }
                    }
                }
            }
        }
    }

    private fun loadData(
        ratingMode: ContentRatingMode = currentRatingMode,
        filter: String = _uiState.value.selectedFilter
    ) {
        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true, error = null) }
            try {
                val ratingTag = when (ratingMode) {
                    ContentRatingMode.SfwOnly -> "rating:s"
                    ContentRatingMode.Questionable -> "rating:q"
                    else -> ""
                }
                val rawTrending = booruRepository.fetchAllSources(ratingTag, 1, 40)
                val trending = rawTrending.filter { post ->
                    val r = post.rating.lowercase()
                    val matchesRating = when (ratingMode) {
                        ContentRatingMode.SfwOnly -> r == "s" || r == "safe" || r == "g" || r == "general"
                        ContentRatingMode.Questionable -> r != "e" && r != "explicit"
                        else -> true
                    }
                    if (!matchesRating) return@filter false

                    when (filter) {
                        "portrait" -> post.width > 0 && post.height > 0 && (post.height >= post.width)
                        "landscape" -> post.width > 0 && post.height > 0 && (post.width > post.height)
                        else -> true
                    }
                }
                _uiState.update { 
                    it.copy(
                        isLoading = false,
                        trendingPosts = trending
                    )
                }

                // Load popular series in the background without blocking the UI
                launch {
                    val popular = booruRepository.getPopularSeries("yande", 10)
                    val topPick = popular.firstOrNull()?.name ?: "Wuthering Waves"
                    val topTag = popular.firstOrNull()?.tag ?: "wuthering_waves"
                    _uiState.update { 
                        it.copy(
                            popularSeries = popular,
                            topPickTitle = topPick,
                            topPickTag = topTag
                        ) 
                    }
                }
            } catch (e: Exception) {
                _uiState.update { it.copy(isLoading = false, error = e.message) }
            }
        }
    }

    private fun observeFavorites() {
        viewModelScope.launch {
            favoritesRepository.getAllFavorites().collect { favorites ->
                val favKeys = favorites.map { "${it.source}_${it.postId}" }.toSet()
                val posts = favorites.map { entity ->
                    PostItem(
                        id = entity.postId,
                        source = entity.source,
                        previewUrl = entity.previewUrl,
                        sampleUrl = entity.sampleUrl,
                        fileUrl = entity.fileUrl,
                        width = entity.width,
                        height = entity.height,
                        rating = entity.rating,
                        score = entity.score,
                        tags = entity.tags,
                        author = entity.author,
                        sourceUrl = entity.sourceUrl,
                        createdAt = entity.createdAt,
                        isFavorite = true
                    )
                }
                _uiState.update { 
                    it.copy(
                        recentFavorites = posts,
                        favoriteKeys = favKeys,
                        favoritesCount = favKeys.size
                    ) 
                }
            }
        }
    }

    fun handleIntent(intent: HomeIntent) {
        when (intent) {
            is HomeIntent.Refresh -> loadData()
            is HomeIntent.NavigateToExplore -> { /* Handled via navigation callbacks */ }
            is HomeIntent.NavigateToSeries -> { /* Handled via navigation callbacks */ }
            is HomeIntent.ApplyRandomWallpaper -> applyRandomWallpaper()
            is HomeIntent.ToggleFavorite -> toggleFavorite(intent.post)
            is HomeIntent.ApplyWallpaper -> applyWallpaper(intent.post)
            is HomeIntent.Download -> download(intent.post)
            is HomeIntent.DismissHero -> dismissHero()
            is HomeIntent.ToggleTheme -> toggleTheme()
            is HomeIntent.SetFilter -> setFilter(intent.filter)
            is HomeIntent.SetUserName -> setUserName(intent.name)
            is HomeIntent.DismissNamePrompt -> {
                hasPromptedForNameInSession = true
                _uiState.update { it.copy(showNamePromptDialog = false) }
                viewModelScope.launch {
                    val name = intent.enteredName?.trim().orEmpty()
                    if (name.isNotBlank()) {
                        _uiState.update { it.copy(userName = name) }
                    }
                    settingsRepository.saveInitialUserName(name)
                }
            }
            is HomeIntent.ToggleGreetingName -> {
                _uiState.update { it.copy(isGreetingNameEnabled = intent.enabled) }
                viewModelScope.launch {
                    settingsRepository.setIsGreetingNameEnabled(intent.enabled)
                }
            }
        }
    }

    private fun toggleTheme() {
        val current = _uiState.value.themeMode.lowercase()
        val next = if (current == "dark") "light" else "dark"
        viewModelScope.launch {
            settingsRepository.setThemeMode(next)
        }
    }

    private fun setFilter(filter: String) {
        if (_uiState.value.selectedFilter == filter) return
        _uiState.update { it.copy(selectedFilter = filter) }
        loadData(filter = filter)
    }

    private fun setUserName(name: String) {
        val trimmed = name.trim()
        if (trimmed.isNotBlank()) {
            _uiState.update { it.copy(userName = trimmed) }
            viewModelScope.launch {
                settingsRepository.setUserName(trimmed)
            }
        }
    }

    private fun dismissHero() {
        viewModelScope.launch {
            settingsRepository.setShowHomeHero(false)
        }
    }

    private fun applyRandomWallpaper() {
        viewModelScope.launch {
            val posts = _uiState.value.trendingPosts
            if (posts.isNotEmpty()) {
                val randomPost = posts.random()
                val url = randomPost.sampleUrl ?: randomPost.fileUrl ?: randomPost.previewUrl
                val success = wallpaperEngine.applyWallpaper(context, url, WallpaperManager.FLAG_SYSTEM)
                if (success) {
                    settingsRepository.incrementAppliedCount()
                }
            }
        }
    }

    private fun toggleFavorite(post: PostItem) {
        val key = "${post.source}_${post.id}"
        val currentlyFav = _uiState.value.favoriteKeys.contains(key)
        val updatedKeys = if (currentlyFav) {
            _uiState.value.favoriteKeys - key
        } else {
            _uiState.value.favoriteKeys + key
        }
        _uiState.update { it.copy(favoriteKeys = updatedKeys) }
        viewModelScope.launch {
            favoritesRepository.toggleFavorite(post)
        }
    }

    private fun applyWallpaper(post: PostItem) {
        viewModelScope.launch {
            val url = post.sampleUrl ?: post.fileUrl ?: post.previewUrl
            val success = wallpaperEngine.applyWallpaper(context, url, WallpaperManager.FLAG_SYSTEM)
            if (success) {
                settingsRepository.incrementAppliedCount()
            }
        }
    }

    private fun download(post: PostItem) {
        viewModelScope.launch {
            downloadManager.downloadWallpaper(context, post) { }
        }
    }
}
