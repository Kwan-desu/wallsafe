package com.wallsafe.feature.favorites

import com.wallsafe.core.database.dao.CollectionWithCount
import com.wallsafe.core.database.entity.CollectionEntity
import com.wallsafe.core.model.PostItem

data class FavoritesUiState(
    val collections: List<CollectionWithCount> = emptyList(),
    val uncategorizedFavorites: List<PostItem> = emptyList(),
    val selectedCollection: CollectionEntity? = null,
    val collectionFavorites: List<PostItem> = emptyList(),
    val isLoading: Boolean = true,
    val showCreateDialog: Boolean = false,
    val showRenameDialog: Pair<Int, String>? = null
)

sealed interface FavoritesIntent {
    data class CreateCollection(val name: String) : FavoritesIntent
    data class DeleteCollection(val id: Int) : FavoritesIntent
    data class RenameCollection(val id: Int, val newName: String) : FavoritesIntent
    data class SelectCollection(val collection: CollectionEntity) : FavoritesIntent
    data object BackToCollections : FavoritesIntent
    data class RemoveFromFavorites(val postId: Int, val source: String) : FavoritesIntent
    data class AssignToCollection(val postId: Int, val source: String, val collectionId: Int) : FavoritesIntent
    data object ShowCreateDialog : FavoritesIntent
    data object DismissCreateDialog : FavoritesIntent
    data class ShowRenameDialog(val id: Int, val currentName: String) : FavoritesIntent
    data object DismissRenameDialog : FavoritesIntent
}
