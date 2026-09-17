package com.wallsafe.feature.favorites

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.wallsafe.core.data.repository.FavoritesRepository
import com.wallsafe.core.database.dao.CollectionWithCount
import com.wallsafe.core.database.entity.CollectionEntity
import com.wallsafe.core.model.PostItem
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

@HiltViewModel
class FavoritesViewModel @Inject constructor(
    private val favoritesRepository: FavoritesRepository
) : ViewModel() {

    private val _uiState = MutableStateFlow(FavoritesUiState())
    val uiState: StateFlow<FavoritesUiState> = _uiState.asStateFlow()

    init {
        loadData()
    }

    private fun loadData() {
        viewModelScope.launch {
            favoritesRepository.getCollectionsWithCount().collect { collections ->
                _uiState.update { it.copy(collections = collections, isLoading = false) }
            }
        }
        viewModelScope.launch {
            favoritesRepository.getAllFavorites().collect { allFavs ->
                val posts = allFavs.map { entity ->
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
                _uiState.update { it.copy(uncategorizedFavorites = posts, isLoading = false) }
            }
        }
    }

    fun handleIntent(intent: FavoritesIntent) {
        when (intent) {
            is FavoritesIntent.CreateCollection -> {
                viewModelScope.launch {
                    favoritesRepository.createCollection(intent.name)
                    _uiState.update { it.copy(showCreateDialog = false) }
                }
            }
            is FavoritesIntent.DeleteCollection -> {
                viewModelScope.launch {
                    favoritesRepository.deleteCollection(intent.id)
                }
            }
            is FavoritesIntent.RenameCollection -> {
                viewModelScope.launch {
                    favoritesRepository.renameCollection(intent.id, intent.newName)
                    _uiState.update { it.copy(showRenameDialog = null) }
                }
            }
            is FavoritesIntent.SelectCollection -> selectCollection(intent.collection)
            is FavoritesIntent.BackToCollections -> _uiState.update { it.copy(selectedCollection = null, collectionFavorites = emptyList()) }
            is FavoritesIntent.RemoveFromFavorites -> {
                // Remove favorite handling
            }
            is FavoritesIntent.AssignToCollection -> {
                viewModelScope.launch {
                    favoritesRepository.assignToCollection(intent.postId, intent.source, intent.collectionId)
                }
            }
            is FavoritesIntent.ShowCreateDialog -> _uiState.update { it.copy(showCreateDialog = true) }
            is FavoritesIntent.DismissCreateDialog -> _uiState.update { it.copy(showCreateDialog = false) }
            is FavoritesIntent.ShowRenameDialog -> _uiState.update { it.copy(showRenameDialog = Pair(intent.id, intent.currentName)) }
            is FavoritesIntent.DismissRenameDialog -> _uiState.update { it.copy(showRenameDialog = null) }
        }
    }

    private fun selectCollection(collection: CollectionEntity) {
        viewModelScope.launch {
            _uiState.update { it.copy(selectedCollection = collection) }
            favoritesRepository.getFavoritesByCollection(collection.id).collect { favs ->
                val posts = favs.map { entity ->
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
                _uiState.update { it.copy(collectionFavorites = posts) }
            }
        }
    }
}
