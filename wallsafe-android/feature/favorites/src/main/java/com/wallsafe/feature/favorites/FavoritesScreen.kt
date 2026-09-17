package com.wallsafe.feature.favorites

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.FavoriteBorder
import androidx.compose.material3.*
import androidx.compose.runtime.*
import android.content.res.Configuration
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import coil3.compose.AsyncImage
import com.wallsafe.core.database.entity.CollectionEntity
import com.wallsafe.core.model.PostItem
import com.wallsafe.feature.favorites.components.CollectionCard

@Composable
fun FavoritesRoute(
    onNavigateToPost: (String) -> Unit,
    viewModel: FavoritesViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()

    FavoritesScreen(
        uiState = uiState,
        onIntent = viewModel::handleIntent,
        onNavigateToPost = onNavigateToPost
    )
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun FavoritesScreen(
    uiState: FavoritesUiState,
    onIntent: (FavoritesIntent) -> Unit,
    onNavigateToPost: (String) -> Unit
) {
    Scaffold(
        topBar = {
            TopAppBar(
                title = { 
                    Text(if (uiState.selectedCollection != null) uiState.selectedCollection.name else "Favorites")
                },
                navigationIcon = {
                    if (uiState.selectedCollection != null) {
                        IconButton(onClick = { onIntent(FavoritesIntent.BackToCollections) }) {
                            Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back")
                        }
                    }
                }
            )
        },
        floatingActionButton = {
            if (uiState.selectedCollection == null) {
                FloatingActionButton(onClick = { onIntent(FavoritesIntent.ShowCreateDialog) }) {
                    Icon(Icons.Default.Add, contentDescription = "New Collection")
                }
            }
        }
    ) { paddingValues ->
        if (uiState.selectedCollection == null) {
            val allPosts = uiState.uncategorizedFavorites
            val collections = uiState.collections

            if (collections.isEmpty() && allPosts.isEmpty()) {
                Box(
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(paddingValues),
                    contentAlignment = Alignment.Center
                ) {
                    Column(horizontalAlignment = Alignment.CenterHorizontally) {
                        Icon(
                            imageVector = Icons.Default.FavoriteBorder,
                            contentDescription = null,
                            modifier = Modifier.size(64.dp),
                            tint = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                        Spacer(modifier = Modifier.height(16.dp))
                        Text(
                            text = "No favorites yet",
                            style = MaterialTheme.typography.titleMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                        Spacer(modifier = Modifier.height(8.dp))
                        Text(
                            text = "Tap the heart on any wallpaper to add it here",
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }
            } else {
                val configuration = LocalConfiguration.current
                val isLandscape = configuration.orientation == Configuration.ORIENTATION_LANDSCAPE
                val isTablet = configuration.screenWidthDp >= 600

                val minCardSize = when {
                    isTablet && isLandscape -> 240.dp
                    isTablet || isLandscape -> 200.dp
                    else -> 120.dp
                }
                val cardRatio = if (isLandscape || isTablet) 16f / 10f else 1f

                LazyVerticalGrid(
                    columns = GridCells.Adaptive(minSize = minCardSize),
                    contentPadding = PaddingValues(16.dp),
                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                    verticalArrangement = Arrangement.spacedBy(8.dp),
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(paddingValues)
                ) {
                    if (collections.isNotEmpty()) {
                        items(collections) { collectionCount ->
                            CollectionCard(
                                collectionWithCount = collectionCount,
                                onClick = { 
                                    onIntent(
                                        FavoritesIntent.SelectCollection(
                                            CollectionEntity(
                                                id = collectionCount.id,
                                                name = collectionCount.name,
                                                orderIndex = collectionCount.orderIndex,
                                                createdAt = collectionCount.createdAt
                                            )
                                        )
                                    ) 
                                },
                                onRename = { onIntent(FavoritesIntent.ShowRenameDialog(collectionCount.id, collectionCount.name)) },
                                onDelete = { onIntent(FavoritesIntent.DeleteCollection(collectionCount.id)) }
                            )
                        }
                    }

                    items(allPosts, key = { "fav_${it.source}_${it.id}" }) { post ->
                        FavoritePostCard(
                            post = post,
                            onClick = { onNavigateToPost("${post.id}:${post.source}") },
                            cardAspectRatio = cardRatio
                        )
                    }
                }
            }
        } else {
            if (uiState.collectionFavorites.isEmpty()) {
                Box(
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(paddingValues),
                    contentAlignment = Alignment.Center
                ) {
                    Text(
                        text = "No wallpapers in this collection",
                        style = MaterialTheme.typography.bodyLarge,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            } else {
                val configuration = LocalConfiguration.current
                val isLandscape = configuration.orientation == Configuration.ORIENTATION_LANDSCAPE
                val isTablet = configuration.screenWidthDp >= 600

                val minCardSize = when {
                    isTablet && isLandscape -> 240.dp
                    isTablet || isLandscape -> 200.dp
                    else -> 120.dp
                }
                val cardRatio = if (isLandscape || isTablet) 16f / 10f else 1f

                LazyVerticalGrid(
                    columns = GridCells.Adaptive(minSize = minCardSize),
                    contentPadding = PaddingValues(16.dp),
                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                    verticalArrangement = Arrangement.spacedBy(8.dp),
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(paddingValues)
                ) {
                    items(uiState.collectionFavorites, key = { "col_${it.source}_${it.id}" }) { post ->
                        FavoritePostCard(
                            post = post,
                            onClick = { onNavigateToPost("${post.id}:${post.source}") },
                            cardAspectRatio = cardRatio
                        )
                    }
                }
            }
        }
    }

    if (uiState.showCreateDialog) {
        var name by remember { mutableStateOf("") }
        AlertDialog(
            onDismissRequest = { onIntent(FavoritesIntent.DismissCreateDialog) },
            title = { Text("New Collection") },
            text = {
                OutlinedTextField(
                    value = name,
                    onValueChange = { name = it },
                    label = { Text("Name") },
                    singleLine = true
                )
            },
            confirmButton = {
                TextButton(
                    onClick = { 
                        if (name.isNotBlank()) {
                            onIntent(FavoritesIntent.CreateCollection(name.trim()))
                        }
                    }
                ) {
                    Text("Create")
                }
            },
            dismissButton = {
                TextButton(onClick = { onIntent(FavoritesIntent.DismissCreateDialog) }) {
                    Text("Cancel")
                }
            }
        )
    }

    uiState.showRenameDialog?.let { (id, currentName) ->
        var name by remember { mutableStateOf(currentName) }
        AlertDialog(
            onDismissRequest = { onIntent(FavoritesIntent.DismissRenameDialog) },
            title = { Text("Rename Collection") },
            text = {
                OutlinedTextField(
                    value = name,
                    onValueChange = { name = it },
                    label = { Text("Name") },
                    singleLine = true
                )
            },
            confirmButton = {
                TextButton(
                    onClick = { 
                        if (name.isNotBlank()) {
                            onIntent(FavoritesIntent.RenameCollection(id, name.trim()))
                        }
                    }
                ) {
                    Text("Rename")
                }
            },
            dismissButton = {
                TextButton(onClick = { onIntent(FavoritesIntent.DismissRenameDialog) }) {
                    Text("Cancel")
                }
            }
        )
    }
}

@Composable
private fun FavoritePostCard(
    post: PostItem,
    onClick: () -> Unit,
    cardAspectRatio: Float = 1f
) {
    ElevatedCard(
        modifier = Modifier
            .fillMaxWidth()
            .aspectRatio(cardAspectRatio)
            .clickable(onClick = onClick),
        shape = RoundedCornerShape(12.dp)
    ) {
        AsyncImage(
            model = post.thumbnailUrl,
            contentDescription = "Favorite wallpaper",
            contentScale = ContentScale.Crop,
            modifier = Modifier.fillMaxSize()
        )
    }
}
