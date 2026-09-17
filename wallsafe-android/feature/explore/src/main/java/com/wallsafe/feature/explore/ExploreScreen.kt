package com.wallsafe.feature.explore

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.GridItemSpan
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Search
import androidx.compose.material3.*
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.onFocusChanged
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.SolidColor
import android.content.res.Configuration
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.platform.LocalFocusManager
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.paging.LoadState
import androidx.paging.compose.collectAsLazyPagingItems
import com.wallsafe.core.ui.components.LoadingIndicator
import com.wallsafe.core.ui.components.WallpaperCard
import com.wallsafe.feature.explore.components.FilterChipRow
import com.wallsafe.feature.explore.components.SuggestionsDropdown

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ExploreScreen(
    initialTag: String? = null,
    onNavigateToPreview: (postId: Int, source: String) -> Unit,
    viewModel: ExploreViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    val pagingItems = viewModel.wallpaperPagingFlow.collectAsLazyPagingItems()
    val isRefreshing = pagingItems.loadState.refresh is LoadState.Loading
    val focusManager = LocalFocusManager.current

    LaunchedEffect(initialTag) {
        if (!initialTag.isNullOrBlank()) {
            viewModel.handleIntent(ExploreIntent.UpdateSearchQuery(initialTag))
            viewModel.handleIntent(ExploreIntent.SetSearchFocused(false))
            viewModel.handleIntent(ExploreIntent.Search)
            pagingItems.refresh()
        }
    }

    Scaffold(
        topBar = {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .statusBarsPadding()
                    .padding(top = 6.dp),
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                // Non-clipping Pill Search Bar (Phone & Tablet Scaled)
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(horizontal = 16.dp, vertical = 4.dp),
                    contentAlignment = Alignment.Center
                ) {
                    Surface(
                        modifier = Modifier
                            .fillMaxWidth()
                            .widthIn(max = 720.dp)
                            .height(50.dp),
                        shape = RoundedCornerShape(25.dp),
                        color = MaterialTheme.colorScheme.surfaceContainerHigh,
                        tonalElevation = 2.dp
                    ) {
                        Row(
                            modifier = Modifier
                                .fillMaxSize()
                                .padding(horizontal = 14.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Icon(
                                imageVector = Icons.Default.Search,
                                contentDescription = "Search",
                                tint = MaterialTheme.colorScheme.primary,
                                modifier = Modifier.size(20.dp)
                            )
                            Spacer(modifier = Modifier.width(10.dp))
                            Box(
                                modifier = Modifier.weight(1f),
                                contentAlignment = Alignment.CenterStart
                            ) {
                                if (uiState.searchQuery.isEmpty()) {
                                    Text(
                                        text = "Search tags, artists, series...",
                                        style = MaterialTheme.typography.bodyMedium,
                                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                                        maxLines = 1,
                                        overflow = TextOverflow.Ellipsis
                                    )
                                }
                                BasicTextField(
                                    value = uiState.searchQuery,
                                    onValueChange = { viewModel.handleIntent(ExploreIntent.UpdateSearchQuery(it)) },
                                    singleLine = true,
                                    textStyle = MaterialTheme.typography.bodyMedium.copy(
                                        color = MaterialTheme.colorScheme.onSurface
                                    ),
                                    cursorBrush = SolidColor(MaterialTheme.colorScheme.primary),
                                    keyboardOptions = KeyboardOptions(imeAction = ImeAction.Search),
                                    keyboardActions = KeyboardActions(
                                        onSearch = {
                                            focusManager.clearFocus()
                                            viewModel.handleIntent(ExploreIntent.SetSearchFocused(false))
                                            viewModel.handleIntent(ExploreIntent.Search)
                                            pagingItems.refresh()
                                        }
                                    ),
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .onFocusChanged { focusState ->
                                            viewModel.handleIntent(ExploreIntent.SetSearchFocused(focusState.isFocused))
                                        }
                                )
                            }
                            if (uiState.searchQuery.isNotEmpty()) {
                                IconButton(
                                    onClick = {
                                        viewModel.handleIntent(ExploreIntent.UpdateSearchQuery(""))
                                        viewModel.handleIntent(ExploreIntent.SetSearchFocused(false))
                                        viewModel.handleIntent(ExploreIntent.Search)
                                        focusManager.clearFocus()
                                        pagingItems.refresh()
                                    },
                                    modifier = Modifier.size(30.dp)
                                ) {
                                    Icon(
                                        imageVector = Icons.Default.Close,
                                        contentDescription = "Clear",
                                        modifier = Modifier.size(16.dp),
                                        tint = MaterialTheme.colorScheme.onSurfaceVariant
                                    )
                                }
                            }
                        }
                    }
                }

                // Floating Suggestions Dropdown (Tablet responsive)
                if (uiState.isSuggestionsVisible && uiState.suggestions.isNotEmpty()) {
                    Box(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(horizontal = 16.dp, vertical = 2.dp),
                        contentAlignment = Alignment.Center
                    ) {
                        ElevatedCard(
                            modifier = Modifier
                                .fillMaxWidth()
                                .widthIn(max = 720.dp)
                                .heightIn(max = 240.dp),
                            shape = RoundedCornerShape(16.dp),
                            elevation = CardDefaults.elevatedCardElevation(defaultElevation = 6.dp)
                        ) {
                            SuggestionsDropdown(
                                suggestions = uiState.suggestions,
                                onSuggestionSelected = { selectedTag ->
                                    focusManager.clearFocus()
                                    viewModel.handleIntent(ExploreIntent.SetSearchFocused(false))
                                    viewModel.handleIntent(ExploreIntent.UpdateSearchQuery(selectedTag))
                                    viewModel.handleIntent(ExploreIntent.Search)
                                    pagingItems.refresh()
                                }
                            )
                        }
                    }
                }

                Box(
                    modifier = Modifier.fillMaxWidth(),
                    contentAlignment = Alignment.Center
                ) {
                    FilterChipRow(
                        ratingMode = uiState.ratingMode,
                        onRatingModeSelected = { viewModel.handleIntent(ExploreIntent.SetRatingMode(it)) },
                        sortOrder = uiState.sortOrder,
                        onSortOrderSelected = { viewModel.handleIntent(ExploreIntent.SetSortOrder(it)) },
                        resolutionFilter = uiState.resolutionFilter,
                        onResolutionFilterSelected = { viewModel.handleIntent(ExploreIntent.SetResolutionFilter(it)) },
                        aspectRatioFilter = uiState.aspectRatioFilter,
                        onAspectRatioFilterSelected = { viewModel.handleIntent(ExploreIntent.SetAspectRatioFilter(it)) },
                        isWallpaperTagOnly = uiState.isWallpaperTagOnly,
                        onToggleWallpaperTag = { viewModel.handleIntent(ExploreIntent.ToggleWallpaperTag(it)) },
                        modifier = Modifier
                            .fillMaxWidth()
                            .widthIn(max = 720.dp)
                    )
                }
            }
        }
    ) { paddingValues ->
        val configuration = LocalConfiguration.current
        val isLandscape = configuration.orientation == Configuration.ORIENTATION_LANDSCAPE
        val isTablet = configuration.screenWidthDp >= 600

        val minCardSize = when {
            isTablet && isLandscape -> 300.dp
            isTablet -> 260.dp
            isLandscape -> 250.dp
            else -> 160.dp
        }

        PullToRefreshBox(
            isRefreshing = isRefreshing,
            onRefresh = { 
                focusManager.clearFocus()
                pagingItems.refresh() 
            },
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
        ) {
            LazyVerticalGrid(
                columns = GridCells.Adaptive(minSize = minCardSize),
                contentPadding = androidx.compose.foundation.layout.PaddingValues(12.dp),
                horizontalArrangement = androidx.compose.foundation.layout.Arrangement.spacedBy(10.dp),
                verticalArrangement = androidx.compose.foundation.layout.Arrangement.spacedBy(10.dp),
                modifier = Modifier.fillMaxSize()
            ) {
                items(
                    count = pagingItems.itemCount,
                    key = { index -> pagingItems[index]?.id ?: index }
                ) { index ->
                    val post = pagingItems[index]
                    if (post != null) {
                        val isFav = uiState.favoriteKeys.contains("${post.source}_${post.id}")
                        WallpaperCard(
                            post = post,
                            onTap = { 
                                focusManager.clearFocus()
                                onNavigateToPreview(post.id, post.source) 
                            },
                            onFavoriteToggle = { viewModel.handleIntent(ExploreIntent.ToggleFavorite(post)) },
                            onApplyWallpaper = { viewModel.handleIntent(ExploreIntent.ApplyWallpaper(post)) },
                            onDownload = { viewModel.handleIntent(ExploreIntent.DownloadWallpaper(post)) },
                            onOpenSource = { /* Open source */ },
                            isFavorite = isFav,
                            isBlurEnabled = uiState.isDiscretionBlur
                        )
                    }
                }
                
                when (pagingItems.loadState.append) {
                    is LoadState.Loading -> {
                        item(span = { GridItemSpan(maxLineSpan) }) {
                            LoadingIndicator()
                        }
                    }
                    is LoadState.Error -> {
                        item(span = { GridItemSpan(maxLineSpan) }) {
                            Text("Error loading more items. Tap to retry.", modifier = Modifier.padding(16.dp))
                        }
                    }
                    else -> {}
                }
            }
        }
    }
}
