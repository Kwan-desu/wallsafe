package com.wallsafe.feature.explore

import android.content.Context
import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import androidx.paging.Pager
import androidx.paging.PagingConfig
import androidx.paging.PagingData
import androidx.paging.cachedIn
import com.wallsafe.core.data.paging.BooruPagingSource
import com.wallsafe.core.data.repository.BooruRepository
import com.wallsafe.core.data.repository.FavoritesRepository
import com.wallsafe.core.data.repository.SettingsRepository
import com.wallsafe.core.model.ContentRatingMode
import com.wallsafe.core.model.PostItem
import com.wallsafe.core.wallpaper.DownloadManager
import com.wallsafe.core.wallpaper.WallpaperEngine
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.FlowPreview
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.debounce
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.filter
import kotlinx.coroutines.flow.flatMapLatest
import kotlinx.coroutines.flow.launchIn
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.onEach
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

@OptIn(ExperimentalCoroutinesApi::class, FlowPreview::class)
@HiltViewModel
class ExploreViewModel @Inject constructor(
    private val savedStateHandle: SavedStateHandle,
    @ApplicationContext private val context: Context,
    private val booruRepository: BooruRepository,
    private val favoritesRepository: FavoritesRepository,
    private val settingsRepository: SettingsRepository,
    private val wallpaperEngine: WallpaperEngine,
    private val downloadManager: DownloadManager
) : ViewModel() {

    private var isInitialTagConsumed: Boolean
        get() = savedStateHandle["is_initial_tag_consumed"] ?: false
        set(value) { savedStateHandle["is_initial_tag_consumed"] = value }

    fun setInitialTagOnce(tag: String?) {
        val lastTag = savedStateHandle.get<String>("last_applied_tag")
        if (!tag.isNullOrBlank() && (!isInitialTagConsumed || tag != lastTag)) {
            isInitialTagConsumed = true
            savedStateHandle["last_applied_tag"] = tag
            _uiState.update { 
                it.copy(
                    searchQuery = tag, 
                    isSearchFocused = false, 
                    isSuggestionsVisible = false 
                ) 
            }
        }
    }

    private val _uiState = MutableStateFlow(ExploreUiState())
    val uiState: StateFlow<ExploreUiState> = _uiState.asStateFlow()

    val wallpaperPagingFlow: Flow<PagingData<PostItem>> = _uiState
        .mapStateForPaging()
        .flatMapLatest { params ->
            Pager(
                config = PagingConfig(
                    pageSize = 40,
                    prefetchDistance = 10,
                    initialLoadSize = 40,
                    enablePlaceholders = false
                ),
                pagingSourceFactory = {
                    BooruPagingSource(
                        repository = booruRepository,
                        source = params.source,
                        tags = params.query,
                        ratingMode = params.ratingMode,
                        sortOrder = params.sortOrder,
                        resolutionFilter = params.resolutionFilter,
                        aspectRatioFilter = params.aspectRatioFilter,
                        wallpaperOnly = params.isWallpaperTagOnly,
                        tagBlacklist = params.tagBlacklist
                    )
                }
            ).flow
        }
        .cachedIn(viewModelScope)

    init {
        viewModelScope.launch {
            settingsRepository.isDiscretionBlurEnabled.collect { blurEnabled ->
                _uiState.update { it.copy(isDiscretionBlur = blurEnabled) }
            }
        }
        viewModelScope.launch {
            settingsRepository.ratingMode.collect { mode ->
                _uiState.update { it.copy(ratingMode = mode) }
            }
        }
        viewModelScope.launch {
            settingsRepository.tagBlacklist.collect { blString ->
                val set = blString.split(",").map { it.trim().lowercase() }.filter { it.isNotBlank() }.toSet()
                _uiState.update { it.copy(tagBlacklist = set) }
            }
        }
        viewModelScope.launch {
            settingsRepository.customSourcesJson.collect { json ->
                try {
                    val baseSources = listOf(
                        "all" to "All Sources",
                        "danbooru" to "Danbooru",
                        "safebooru" to "Safebooru",
                        "gelbooru" to "Gelbooru"
                    )
                    val jsonArray = org.json.JSONArray(json)
                    val customList = mutableListOf<Pair<String, String>>()
                    for (i in 0 until jsonArray.length()) {
                        val obj = jsonArray.getJSONObject(i)
                        val id = obj.optString("id", "")
                        val name = obj.optString("name", "")
                        if (id.isNotBlank() && name.isNotBlank()) {
                            customList.add(id to name)
                        }
                    }
                    _uiState.update { it.copy(availableSources = baseSources + customList) }
                } catch (e: Exception) {
                    // Ignore parse errors
                }
            }
        }
        viewModelScope.launch {
            favoritesRepository.getFavoriteKeys().collect { favKeys ->
                _uiState.update { it.copy(favoriteKeys = favKeys) }
            }
        }

        // Fast & reliable search query debouncing for suggestions
        _uiState
            .mapStateForSearch()
            .debounce(200)
            .distinctUntilChanged()
            .onEach { query ->
                val activeToken = query.substringAfterLast(' ').trim()
                if (activeToken.length < 2 || !_uiState.value.isSearchFocused) {
                    _uiState.update { it.copy(suggestions = emptyList(), isSuggestionsVisible = false) }
                    return@onEach
                }
                try {
                    val suggestions = booruRepository.searchTags(_uiState.value.selectedSource, activeToken)
                    if (_uiState.value.isSearchFocused && _uiState.value.searchQuery.isNotBlank()) {
                        _uiState.update { 
                            it.copy(suggestions = suggestions, isSuggestionsVisible = suggestions.isNotEmpty()) 
                        }
                    }
                } catch (e: Exception) {
                    _uiState.update { it.copy(suggestions = emptyList(), isSuggestionsVisible = false) }
                }
            }
            .launchIn(viewModelScope)
    }

    private fun Flow<ExploreUiState>.mapStateForPaging(): Flow<PagingParameters> = 
        map { state ->
            PagingParameters(
                query = state.searchQuery,
                source = state.selectedSource,
                ratingMode = state.ratingMode,
                sortOrder = state.sortOrder,
                resolutionFilter = state.resolutionFilter,
                aspectRatioFilter = state.aspectRatioFilter,
                isWallpaperTagOnly = state.isWallpaperTagOnly,
                tagBlacklist = state.tagBlacklist
            )
        }.distinctUntilChanged()
        
    private fun Flow<ExploreUiState>.mapStateForSearch(): Flow<String> =
        map { it.searchQuery }.distinctUntilChanged()

    fun handleIntent(intent: ExploreIntent) {
        when (intent) {
            is ExploreIntent.UpdateSearchQuery -> {
                _uiState.update { it.copy(searchQuery = intent.query) }
                if (intent.query.isBlank()) {
                    _uiState.update { it.copy(suggestions = emptyList(), isSuggestionsVisible = false) }
                }
            }
            is ExploreIntent.SelectSuggestion -> {
                val current = _uiState.value.searchQuery
                val prefix = if (current.contains(' ')) {
                    current.substringBeforeLast(' ') + " "
                } else {
                    ""
                }
                val newQuery = "$prefix${intent.suggestionTag} "
                _uiState.update { 
                    it.copy(
                        searchQuery = newQuery,
                        suggestions = emptyList(),
                        isSuggestionsVisible = false
                    ) 
                }
            }
            is ExploreIntent.SetSearchFocused -> {
                _uiState.update { it.copy(isSearchFocused = intent.focused) }
                if (!intent.focused) {
                    _uiState.update { it.copy(isSuggestionsVisible = false) }
                } else if (_uiState.value.searchQuery.isNotBlank() && _uiState.value.suggestions.isNotEmpty()) {
                    _uiState.update { it.copy(isSuggestionsVisible = true) }
                }
            }
            is ExploreIntent.Search -> {
                _uiState.update { 
                    it.copy(
                        isSuggestionsVisible = false,
                        isSearchFocused = false,
                        isSearchActive = false
                    ) 
                }
            }
            is ExploreIntent.SelectSource -> _uiState.update { it.copy(selectedSource = intent.sourceId) }
            is ExploreIntent.SetRatingMode -> _uiState.update { it.copy(ratingMode = intent.mode) }
            is ExploreIntent.SetSortOrder -> _uiState.update { it.copy(sortOrder = intent.order) }
            is ExploreIntent.SetResolutionFilter -> _uiState.update { it.copy(resolutionFilter = intent.index) }
            is ExploreIntent.SetAspectRatioFilter -> _uiState.update { it.copy(aspectRatioFilter = intent.index) }
            is ExploreIntent.ToggleWallpaperTag -> _uiState.update { it.copy(isWallpaperTagOnly = intent.enabled) }
            is ExploreIntent.ToggleFavorite -> toggleFavorite(intent.post)
            is ExploreIntent.ApplyWallpaper -> applyWallpaper(intent.post)
            is ExploreIntent.DownloadWallpaper -> downloadWallpaper(intent.post)
            is ExploreIntent.DismissSuggestions -> _uiState.update { it.copy(isSuggestionsVisible = false) }
            is ExploreIntent.ToggleSearchActive -> _uiState.update { it.copy(isSearchActive = intent.active) }
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
            wallpaperEngine.applyWallpaper(context, url)
        }
    }

    private fun downloadWallpaper(post: PostItem) {
        viewModelScope.launch {
            downloadManager.downloadWallpaper(context, post) { /* progress */ }
        }
    }
}

private data class PagingParameters(
    val query: String,
    val source: String,
    val ratingMode: ContentRatingMode,
    val sortOrder: String,
    val resolutionFilter: Int,
    val aspectRatioFilter: Int,
    val isWallpaperTagOnly: Boolean,
    val tagBlacklist: Set<String>
)
