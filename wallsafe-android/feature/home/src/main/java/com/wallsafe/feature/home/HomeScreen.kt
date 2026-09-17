package com.wallsafe.feature.home

import android.content.res.Configuration
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.wallsafe.core.ui.components.WallpaperCard
import com.wallsafe.feature.home.components.FranchiseCard
import com.wallsafe.feature.home.components.WelcomeHeader

@Composable
fun HomeRoute(
    onNavigateToExplore: () -> Unit,
    onNavigateToSeries: (String) -> Unit,
    onNavigateToFavorites: () -> Unit = {},
    onNavigateToDownloads: () -> Unit = {},
    onNavigateToPreview: (Int, String) -> Unit = { _, _ -> },
    viewModel: HomeViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()

    HomeScreen(
        uiState = uiState,
        onIntent = { intent ->
            when (intent) {
                is HomeIntent.NavigateToExplore -> onNavigateToExplore()
                is HomeIntent.NavigateToSeries -> onNavigateToSeries(intent.tag)
                else -> viewModel.handleIntent(intent)
            }
        },
        onNavigateToExplore = onNavigateToExplore,
        onNavigateToFavorites = onNavigateToFavorites,
        onNavigateToDownloads = onNavigateToDownloads,
        onNavigateToPreview = onNavigateToPreview
    )
}

@Composable
fun HomeScreen(
    uiState: HomeUiState,
    onIntent: (HomeIntent) -> Unit,
    onNavigateToExplore: () -> Unit = {},
    onNavigateToFavorites: () -> Unit = {},
    onNavigateToDownloads: () -> Unit = {},
    onNavigateToPreview: (Int, String) -> Unit = { _, _ -> }
) {
    if (uiState.showNamePromptDialog) {
        var inputName by remember { mutableStateOf("") }
        AlertDialog(
            onDismissRequest = { onIntent(HomeIntent.DismissNamePrompt(null)) },
            title = { Text("Welcome to WallSafe! 👋") },
            text = {
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text(
                        "What should we call you? (Optional)",
                        style = MaterialTheme.typography.bodyMedium
                    )
                    OutlinedTextField(
                        value = inputName,
                        onValueChange = { inputName = it },
                        label = { Text("Your Name") },
                        placeholder = { Text("e.g. Alex") },
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth()
                    )
                    Text(
                        "You can change this or turn it off anytime in Settings.",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            },
            confirmButton = {
                Button(
                    onClick = {
                        onIntent(HomeIntent.DismissNamePrompt(inputName.trim()))
                    }
                ) {
                    Text(if (inputName.isBlank()) "Continue" else "Save")
                }
            },
            dismissButton = {
                TextButton(
                    onClick = {
                        onIntent(HomeIntent.DismissNamePrompt(null))
                    }
                ) {
                    Text("Skip")
                }
            }
        )
    }

    Scaffold(
        contentWindowInsets = WindowInsets.statusBars
    ) { paddingValues ->
        BoxWithConstraints(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues),
            contentAlignment = Alignment.TopCenter
        ) {
            val configuration = LocalConfiguration.current
            val isLandscape = configuration.orientation == Configuration.ORIENTATION_LANDSCAPE
            val isTablet = maxWidth >= 600.dp || configuration.screenWidthDp >= 600
            val isExpanded = maxWidth >= 840.dp
            val franchiseColumns = when {
                isExpanded -> 4
                isTablet || isLandscape -> 3
                else -> 2
            }
            val trendingCardWidth = when {
                isTablet && isLandscape -> 300.dp
                isTablet -> 260.dp
                isLandscape -> 250.dp
                else -> 160.dp
            }
            val maxContentWidth = if (isExpanded) 1200.dp else if (isTablet || isLandscape) 980.dp else 840.dp

            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .verticalScroll(rememberScrollState())
                    .padding(horizontal = 16.dp, vertical = 8.dp)
                    .widthIn(max = maxContentWidth),
                verticalArrangement = Arrangement.spacedBy(20.dp)
            ) {
                // Welcome Header (Greeting + Crescent Moon + Pill Filter + 2x2 Soft Pastel Cards)
                WelcomeHeader(
                    userName = uiState.userName,
                    themeMode = uiState.themeMode,
                    selectedFilter = uiState.selectedFilter,
                    onToggleTheme = { onIntent(HomeIntent.ToggleTheme) },
                    onSetFilter = { onIntent(HomeIntent.SetFilter(it)) },
                    onSetUserName = { onIntent(HomeIntent.SetUserName(it)) },
                    onDiscoverClick = onNavigateToExplore,
                    onSeriesClick = onNavigateToExplore,
                    onFavoritesClick = onNavigateToFavorites,
                    onDownloadsClick = onNavigateToDownloads,
                    isTabletOrLandscape = isTablet || isLandscape,
                    isGreetingNameEnabled = uiState.isGreetingNameEnabled
                )

                if (uiState.showTrending) {
                    SectionHeader(
                        title = "Trending Wallpapers",
                        onSeeAllClick = onNavigateToExplore,
                        actionText = "More >"
                    )

                    if (uiState.isLoading && uiState.trendingPosts.isEmpty()) {
                        Box(
                            modifier = Modifier
                                .fillMaxWidth()
                                .height(180.dp),
                            contentAlignment = Alignment.Center
                        ) {
                            CircularProgressIndicator()
                        }
                    } else if (uiState.trendingPosts.isEmpty()) {
                        Box(
                            modifier = Modifier
                                .fillMaxWidth()
                                .height(80.dp),
                            contentAlignment = Alignment.Center
                        ) {
                            Text(
                                text = if (uiState.error != null) "Failed to load: ${uiState.error}" else "No wallpapers found",
                                style = MaterialTheme.typography.bodyMedium,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }
                    } else {
                        LazyRow(
                            horizontalArrangement = Arrangement.spacedBy(10.dp),
                            modifier = Modifier.fillMaxWidth()
                        ) {
                            items(uiState.trendingPosts, key = { "${it.source}_${it.id}" }) { post ->
                                val isFav = uiState.favoriteKeys.contains("${post.source}_${post.id}")
                                WallpaperCard(
                                    post = post,
                                    onTap = { onNavigateToPreview(post.id, post.source) },
                                    onFavoriteToggle = { onIntent(HomeIntent.ToggleFavorite(post)) },
                                    onApplyWallpaper = { onIntent(HomeIntent.ApplyWallpaper(post)) },
                                    onDownload = { onIntent(HomeIntent.Download(post)) },
                                    onOpenSource = { onNavigateToPreview(post.id, post.source) },
                                    modifier = Modifier.width(trendingCardWidth),
                                    isFavorite = isFav,
                                    isBlurEnabled = uiState.isDiscretionBlur
                                )
                            }
                        }
                    }
                }

                if (uiState.showFranchises) {
                    SectionHeader(
                        title = "Popular Franchises",
                        onSeeAllClick = onNavigateToExplore,
                        actionText = "More >"
                    )

                    Column(
                        modifier = Modifier.fillMaxWidth(),
                        verticalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        uiState.popularSeries.chunked(franchiseColumns).forEach { rowSeries ->
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.spacedBy(8.dp)
                            ) {
                                rowSeries.forEach { series ->
                                    Box(modifier = Modifier.weight(1f)) {
                                        FranchiseCard(
                                            series = series,
                                            onClick = { onIntent(HomeIntent.NavigateToSeries(series.tag)) }
                                        )
                                    }
                                }
                                if (rowSeries.size < franchiseColumns) {
                                    repeat(franchiseColumns - rowSeries.size) {
                                        Spacer(modifier = Modifier.weight(1f))
                                    }
                                }
                            }
                        }
                    }
                }

                if (uiState.recentFavorites.isNotEmpty()) {
                    SectionHeader(
                        title = "Recent Favorites",
                        onSeeAllClick = onNavigateToFavorites,
                        actionText = "More >"
                    )
                    LazyRow(
                        horizontalArrangement = Arrangement.spacedBy(10.dp),
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        items(uiState.recentFavorites, key = { "fav_${it.source}_${it.id}" }) { post ->
                            WallpaperCard(
                                post = post,
                                onTap = { onNavigateToPreview(post.id, post.source) },
                                onFavoriteToggle = { onIntent(HomeIntent.ToggleFavorite(post)) },
                                onApplyWallpaper = { onIntent(HomeIntent.ApplyWallpaper(post)) },
                                onDownload = { onIntent(HomeIntent.Download(post)) },
                                onOpenSource = { onNavigateToPreview(post.id, post.source) },
                                modifier = Modifier.width(trendingCardWidth),
                                isFavorite = true,
                                isBlurEnabled = uiState.isDiscretionBlur
                            )
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun SectionHeader(
    title: String,
    onSeeAllClick: () -> Unit,
    actionText: String = "More >"
) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Text(
            text = title,
            style = MaterialTheme.typography.titleMedium.copy(
                fontWeight = FontWeight.Bold,
                fontSize = 18.sp
            ),
            color = MaterialTheme.colorScheme.onSurface
        )
        TextButton(
            onClick = onSeeAllClick,
            contentPadding = PaddingValues(horizontal = 6.dp, vertical = 2.dp)
        ) {
            Text(
                text = actionText,
                style = MaterialTheme.typography.labelLarge.copy(
                    fontWeight = FontWeight.SemiBold
                ),
                color = MaterialTheme.colorScheme.primary
            )
        }
    }
}
