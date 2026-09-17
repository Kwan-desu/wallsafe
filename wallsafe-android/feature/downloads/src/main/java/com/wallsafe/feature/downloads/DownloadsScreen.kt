package com.wallsafe.feature.downloads

import android.content.res.Configuration
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Download
import androidx.compose.material.icons.filled.Info
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import coil3.compose.AsyncImage
import com.wallsafe.core.model.PostItem

@Composable
fun DownloadsRoute(
    onNavigateToPreview: (Int, String) -> Unit = { _, _ -> },
    viewModel: DownloadsViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()

    DownloadsScreen(
        uiState = uiState,
        onIntent = viewModel::handleIntent,
        onWallpaperClick = onNavigateToPreview
    )
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DownloadsScreen(
    uiState: DownloadsUiState,
    onIntent: (DownloadsIntent) -> Unit,
    onWallpaperClick: (Int, String) -> Unit = { _, _ -> }
) {
    Scaffold(
        topBar = {
            TopAppBar(title = { Text("Downloads") })
        }
    ) { paddingValues ->
        if (uiState.isLoading) {
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(paddingValues),
                contentAlignment = Alignment.Center
            ) {
                CircularProgressIndicator()
            }
        } else if (uiState.downloads.isEmpty()) {
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(paddingValues),
                verticalArrangement = Arrangement.Center,
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                Icon(
                    imageVector = Icons.Default.Download,
                    contentDescription = null,
                    modifier = Modifier.size(64.dp),
                    tint = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.6f)
                )
                Spacer(modifier = Modifier.height(16.dp))
                Text(
                    text = "No downloads yet",
                    style = MaterialTheme.typography.titleMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
                Spacer(modifier = Modifier.height(8.dp))
                Text(
                    text = "Downloaded wallpapers will appear here",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.8f)
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
                horizontalArrangement = Arrangement.spacedBy(10.dp),
                verticalArrangement = Arrangement.spacedBy(10.dp),
                modifier = Modifier
                    .fillMaxSize()
                    .padding(paddingValues)
            ) {
                items(uiState.downloads, key = { "${it.source}_${it.id}" }) { post ->
                    ElevatedCard(
                        modifier = Modifier
                            .fillMaxWidth()
                            .aspectRatio(cardRatio)
                            .clip(RoundedCornerShape(14.dp))
                            .clickable {
                                onWallpaperClick(post.id, post.source)
                            },
                        shape = RoundedCornerShape(14.dp)
                    ) {
                        AsyncImage(
                            model = post.localPath ?: post.sampleUrl ?: post.previewUrl,
                            contentDescription = "Downloaded wallpaper",
                            contentScale = ContentScale.Crop,
                            modifier = Modifier.fillMaxSize()
                        )
                    }
                }
            }
        }
    }
}
