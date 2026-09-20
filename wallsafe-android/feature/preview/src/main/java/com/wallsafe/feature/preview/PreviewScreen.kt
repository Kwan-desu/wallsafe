package com.wallsafe.feature.preview

import android.Manifest
import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.os.Build
import android.widget.Toast
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.core.content.ContextCompat
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.core.Spring
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.spring
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.slideInVertically
import androidx.compose.animation.slideOutVertically
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.gestures.detectTransformGestures
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.automirrored.filled.OpenInNew
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.scale
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalUriHandler
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import coil3.compose.AsyncImage
import com.wallsafe.core.model.PostItem
import com.wallsafe.core.ui.components.HeartBurstAnimation
import com.wallsafe.core.ui.components.LoadingIndicator
import com.wallsafe.core.ui.components.RatingBadge
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

enum class ScreenSimulationMode {
    NONE,
    LOCK_SCREEN,
    HOME_SCREEN
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun PreviewScreen(
    onBackClick: () -> Unit,
    onTagClick: (String) -> Unit = {},
    viewModel: PreviewViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    val context = LocalContext.current
    val uriHandler = LocalUriHandler.current

    var isUiVisible by remember { mutableStateOf(true) }
    var scale by remember { mutableStateOf(1f) }
    var offset by remember { mutableStateOf(Offset.Zero) }

    var showApplySheet by remember { mutableStateOf(false) }
    var showInfoSheet by remember { mutableStateOf(false) }
    var simulationMode by remember { mutableStateOf(ScreenSimulationMode.NONE) }
    var showHeartBurst by remember { mutableStateOf(false) }
    var selectedTagAction by remember { mutableStateOf<String?>(null) }

    LaunchedEffect(uiState.userMessage) {
        val msg = uiState.userMessage
        if (!msg.isNullOrBlank()) {
            Toast.makeText(context, msg, Toast.LENGTH_SHORT).show()
            viewModel.handleIntent(PreviewIntent.ClearUserMessage)
        }
    }

    val storagePermissionLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.RequestPermission()
    ) { isGranted ->
        if (isGranted) {
            viewModel.handleIntent(PreviewIntent.Download)
        } else {
            Toast.makeText(context, "Storage permission is needed to save wallpapers on this Android version", Toast.LENGTH_LONG).show()
        }
    }

    val favScale by animateFloatAsState(
        targetValue = if (uiState.isFavorite) 1.25f else 1f,
        animationSpec = spring(dampingRatio = Spring.DampingRatioMediumBouncy),
        label = "fav_bounce"
    )

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(Color.Black)
    ) {
        // Fullscreen Interactive Image Viewer
        Box(
            modifier = Modifier
                .fillMaxSize()
                .pointerInput(Unit) {
                    detectTapGestures(
                        onTap = { isUiVisible = !isUiVisible },
                        onDoubleTap = {
                            showHeartBurst = true
                            if (!uiState.isFavorite) {
                                viewModel.handleIntent(PreviewIntent.ToggleFavorite)
                            }
                        }
                    )
                }
                .pointerInput(Unit) {
                    detectTransformGestures { _, pan, zoom, _ ->
                        scale = (scale * zoom).coerceIn(1f, 5f)
                        offset = if (scale > 1f) offset + pan else Offset.Zero
                    }
                }
        ) {
            if (uiState.isLoading) {
                LoadingIndicator(modifier = Modifier.align(Alignment.Center))
            } else if (uiState.post != null) {
                val post = uiState.post!!
                AsyncImage(
                    model = post.fileUrl ?: post.sampleUrl ?: post.previewUrl,
                    contentDescription = "Wallpaper preview",
                    contentScale = ContentScale.Fit,
                    modifier = Modifier
                        .fillMaxSize()
                        .graphicsLayer {
                            scaleX = scale
                            scaleY = scale
                            translationX = offset.x
                            translationY = offset.y
                        }
                )

                HeartBurstAnimation(
                    trigger = showHeartBurst,
                    onAnimationEnd = { showHeartBurst = false },
                    modifier = Modifier.align(Alignment.Center)
                )

                // Live Lock Screen & Home Screen Simulation Overlays
                when (simulationMode) {
                    ScreenSimulationMode.LOCK_SCREEN -> LockScreenSimulationOverlay()
                    ScreenSimulationMode.HOME_SCREEN -> HomeScreenSimulationOverlay()
                    ScreenSimulationMode.NONE -> {}
                }
            } else if (uiState.error != null) {
                Column(
                    modifier = Modifier.align(Alignment.Center),
                    horizontalAlignment = Alignment.CenterHorizontally,
                    verticalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    Icon(
                        imageVector = Icons.Default.BrokenImage,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.error,
                        modifier = Modifier.size(48.dp)
                    )
                    Text(
                        text = uiState.error!!,
                        color = Color.White,
                        style = MaterialTheme.typography.bodyMedium
                    )
                }
            }
        }

        // Top Controls Overlay
        AnimatedVisibility(
            visible = isUiVisible,
            enter = fadeIn() + slideInVertically { -it },
            exit = fadeOut() + slideOutVertically { -it },
            modifier = Modifier.align(Alignment.TopCenter)
        ) {
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .background(
                        Brush.verticalGradient(
                            colors = listOf(Color.Black.copy(alpha = 0.7f), Color.Transparent)
                        )
                    )
                    .statusBarsPadding()
                    .padding(horizontal = 16.dp, vertical = 8.dp)
            ) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    // Back Button
                    Surface(
                        onClick = onBackClick,
                        shape = CircleShape,
                        color = Color.Black.copy(alpha = 0.5f),
                        contentColor = Color.White,
                        modifier = Modifier.size(42.dp)
                    ) {
                        Box(contentAlignment = Alignment.Center) {
                            Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back", modifier = Modifier.size(20.dp))
                        }
                    }

                    // Compact Source / Dim Badge
                    uiState.post?.let { post ->
                        Surface(
                            shape = RoundedCornerShape(50),
                            color = Color.Black.copy(alpha = 0.5f),
                            contentColor = Color.White
                        ) {
                            Row(
                                modifier = Modifier.padding(horizontal = 12.dp, vertical = 6.dp),
                                verticalAlignment = Alignment.CenterVertically,
                                horizontalArrangement = Arrangement.spacedBy(6.dp)
                            ) {
                                Text(
                                    text = post.source.uppercase(),
                                    fontSize = 11.sp,
                                    fontWeight = FontWeight.Bold,
                                    letterSpacing = 0.5.sp
                                )
                                Text("•", fontSize = 10.sp, color = Color.Gray)
                                Text(
                                    text = "${post.width}×${post.height}",
                                    fontSize = 11.sp,
                                    fontWeight = FontWeight.Medium
                                )
                            }
                        }
                    }

                    // Favorite Button
                    Surface(
                        onClick = { viewModel.handleIntent(PreviewIntent.ToggleFavorite) },
                        shape = CircleShape,
                        color = Color.Black.copy(alpha = 0.5f),
                        contentColor = if (uiState.isFavorite) Color(0xFFFF3B30) else Color.White,
                        modifier = Modifier.size(42.dp)
                    ) {
                        Box(contentAlignment = Alignment.Center) {
                            Icon(
                                imageVector = if (uiState.isFavorite) Icons.Default.Favorite else Icons.Default.FavoriteBorder,
                                contentDescription = "Favorite",
                                tint = if (uiState.isFavorite) Color(0xFFFF3B30) else Color.White,
                                modifier = Modifier
                                    .size(22.dp)
                                    .scale(favScale)
                            )
                        }
                    }
                }
            }
        }

        // Floating Simulation Mode Selector (When active)
        AnimatedVisibility(
            visible = isUiVisible && simulationMode != ScreenSimulationMode.NONE,
            enter = fadeIn() + slideInVertically { -it },
            exit = fadeOut() + slideOutVertically { -it },
            modifier = Modifier
                .align(Alignment.TopCenter)
                .statusBarsPadding()
                .padding(top = 64.dp)
        ) {
            Surface(
                shape = RoundedCornerShape(50),
                color = Color.Black.copy(alpha = 0.75f),
                contentColor = Color.White,
                border = BorderStroke(1.dp, Color.White.copy(alpha = 0.25f))
            ) {
                Row(
                    modifier = Modifier.padding(horizontal = 6.dp, vertical = 4.dp),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(4.dp)
                ) {
                    SimulationTabButton(
                        selected = simulationMode == ScreenSimulationMode.LOCK_SCREEN,
                        label = "Lock Screen",
                        onClick = { simulationMode = ScreenSimulationMode.LOCK_SCREEN }
                    )
                    SimulationTabButton(
                        selected = simulationMode == ScreenSimulationMode.HOME_SCREEN,
                        label = "Home Screen",
                        onClick = { simulationMode = ScreenSimulationMode.HOME_SCREEN }
                    )
                    IconButton(
                        onClick = { simulationMode = ScreenSimulationMode.NONE },
                        modifier = Modifier.size(32.dp)
                    ) {
                        Icon(
                            imageVector = Icons.Default.Close,
                            contentDescription = "Exit Simulation",
                            tint = Color.White.copy(alpha = 0.8f),
                            modifier = Modifier.size(16.dp)
                        )
                    }
                }
            }
        }

        // Bottom Floating Dock Overlay (High Density, Pure Icon Dock)
        AnimatedVisibility(
            visible = isUiVisible,
            enter = fadeIn() + slideInVertically { it },
            exit = fadeOut() + slideOutVertically { it },
            modifier = Modifier.align(Alignment.BottomCenter)
        ) {
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .background(
                        Brush.verticalGradient(
                            colors = listOf(Color.Transparent, Color.Black.copy(alpha = 0.75f))
                        )
                    )
                    .navigationBarsPadding()
                    .padding(bottom = 20.dp),
                contentAlignment = Alignment.Center
            ) {
                Surface(
                    shape = RoundedCornerShape(32.dp),
                    color = Color(0xDD202022),
                    shadowElevation = 8.dp,
                    tonalElevation = 6.dp
                ) {
                    Row(
                        modifier = Modifier.padding(horizontal = 14.dp, vertical = 6.dp),
                        horizontalArrangement = Arrangement.spacedBy(10.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        // 1. Wallpaper Apply Icon
                        IconButton(
                            onClick = { showApplySheet = true },
                            modifier = Modifier.size(40.dp)
                        ) {
                            Icon(
                                imageVector = Icons.Default.Wallpaper,
                                contentDescription = "Apply Wallpaper",
                                tint = Color.White,
                                modifier = Modifier.size(22.dp)
                            )
                        }

                        // 2. Screen Simulation Toggle Icon
                        IconButton(
                            onClick = {
                                simulationMode = when (simulationMode) {
                                    ScreenSimulationMode.NONE -> ScreenSimulationMode.LOCK_SCREEN
                                    ScreenSimulationMode.LOCK_SCREEN -> ScreenSimulationMode.HOME_SCREEN
                                    ScreenSimulationMode.HOME_SCREEN -> ScreenSimulationMode.NONE
                                }
                            },
                            modifier = Modifier.size(40.dp)
                        ) {
                            Icon(
                                imageVector = if (simulationMode != ScreenSimulationMode.NONE) Icons.Default.Smartphone else Icons.Default.Visibility,
                                contentDescription = "Simulate Screen",
                                tint = if (simulationMode != ScreenSimulationMode.NONE) MaterialTheme.colorScheme.primary else Color.White,
                                modifier = Modifier.size(22.dp)
                            )
                        }

                        // 3. Download Icon
                        IconButton(
                            onClick = {
                                if (uiState.isDownloaded) {
                                    Toast.makeText(context, "Already downloaded to Pictures/WallSafe", Toast.LENGTH_SHORT).show()
                                } else if (Build.VERSION.SDK_INT <= Build.VERSION_CODES.P &&
                                    ContextCompat.checkSelfPermission(context, Manifest.permission.WRITE_EXTERNAL_STORAGE) != PackageManager.PERMISSION_GRANTED
                                ) {
                                    storagePermissionLauncher.launch(Manifest.permission.WRITE_EXTERNAL_STORAGE)
                                } else {
                                    viewModel.handleIntent(PreviewIntent.Download)
                                }
                            },
                            modifier = Modifier.size(40.dp)
                        ) {
                            Icon(
                                imageVector = if (uiState.isDownloaded) Icons.Default.DownloadDone else Icons.Default.Download,
                                contentDescription = "Download",
                                tint = if (uiState.isDownloaded) MaterialTheme.colorScheme.primary else Color.White,
                                modifier = Modifier.size(22.dp)
                            )
                        }

                        // 4. Share Icon
                        IconButton(
                            onClick = {
                                val post = uiState.post
                                if (post != null) {
                                    val shareUrl = post.bestImageUrl
                                    val sendIntent = Intent().apply {
                                        action = Intent.ACTION_SEND
                                        putExtra(
                                            Intent.EXTRA_TEXT,
                                            "Check out this wallpaper on WallSafe:\n$shareUrl\n\nResolution: ${post.width}×${post.height}"
                                        )
                                        type = "text/plain"
                                    }
                                    val shareIntent = Intent.createChooser(sendIntent, "Share Wallpaper")
                                    context.startActivity(shareIntent)
                                }
                            },
                            modifier = Modifier.size(40.dp)
                        ) {
                            Icon(
                                imageVector = Icons.Default.Share,
                                contentDescription = "Share",
                                tint = Color.White,
                                modifier = Modifier.size(22.dp)
                            )
                        }

                        // 5. Info / Metadata Icon
                        IconButton(
                            onClick = { showInfoSheet = true },
                            modifier = Modifier.size(40.dp)
                        ) {
                            Icon(
                                imageVector = Icons.Default.Info,
                                contentDescription = "Details",
                                tint = Color.White,
                                modifier = Modifier.size(22.dp)
                            )
                        }

                        // 6. Open in Browser Icon
                        IconButton(
                            onClick = {
                                val url = uiState.post?.sourceUrl ?: uiState.post?.fileUrl ?: uiState.post?.previewUrl
                                if (!url.isNullOrBlank()) {
                                    uriHandler.openUri(url)
                                }
                            },
                            modifier = Modifier.size(40.dp)
                        ) {
                            Icon(
                                imageVector = Icons.AutoMirrored.Filled.OpenInNew,
                                contentDescription = "Open Source",
                                tint = Color.White,
                                modifier = Modifier.size(22.dp)
                            )
                        }
                    }
                }
            }
        }

        // Applying Wallpaper Progress Spinner
        if (uiState.isApplyingWallpaper) {
            Surface(
                modifier = Modifier.fillMaxSize(),
                color = Color.Black.copy(alpha = 0.6f)
            ) {
                Box(contentAlignment = Alignment.Center) {
                    Card(
                        shape = RoundedCornerShape(20.dp),
                        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
                    ) {
                        Row(
                            modifier = Modifier.padding(24.dp),
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.spacedBy(16.dp)
                        ) {
                            CircularProgressIndicator(modifier = Modifier.size(28.dp), strokeWidth = 3.dp)
                            Text("Setting wallpaper...", style = MaterialTheme.typography.bodyLarge)
                        }
                    }
                }
            }
        }

        // Downloading Wallpaper Progress Spinner
        if (uiState.isDownloading) {
            Surface(
                modifier = Modifier.fillMaxSize(),
                color = Color.Black.copy(alpha = 0.6f)
            ) {
                Box(contentAlignment = Alignment.Center) {
                    Card(
                        shape = RoundedCornerShape(20.dp),
                        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
                    ) {
                        Row(
                            modifier = Modifier.padding(24.dp),
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.spacedBy(16.dp)
                        ) {
                            CircularProgressIndicator(modifier = Modifier.size(28.dp), strokeWidth = 3.dp)
                            val progressText = uiState.downloadProgress?.let { " (${(it * 100).toInt()}%)" } ?: ""
                            Text("Downloading wallpaper$progressText...", style = MaterialTheme.typography.bodyLarge)
                        }
                    }
                }
            }
        }
    }

    // Modal Sheet 1: Set Wallpaper Target (Home, Lock, Both)
    if (showApplySheet) {
        ModalBottomSheet(
            onDismissRequest = { showApplySheet = false },
            shape = RoundedCornerShape(topStart = 24.dp, topEnd = 24.dp)
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(horizontal = 20.dp)
                    .padding(bottom = 32.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                Text(
                    text = "Set as Wallpaper",
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold
                )

                ListItem(
                    headlineContent = { Text("Home Screen") },
                    leadingContent = {
                        FilledTonalIconButton(onClick = {
                            viewModel.handleIntent(PreviewIntent.ApplyAsHomeWallpaper)
                            showApplySheet = false
                        }) {
                            Icon(Icons.Default.Home, contentDescription = null)
                        }
                    },
                    modifier = Modifier
                        .clip(RoundedCornerShape(12.dp))
                        .clickable {
                            viewModel.handleIntent(PreviewIntent.ApplyAsHomeWallpaper)
                            showApplySheet = false
                        }
                )

                ListItem(
                    headlineContent = { Text("Lock Screen") },
                    leadingContent = {
                        FilledTonalIconButton(onClick = {
                            viewModel.handleIntent(PreviewIntent.ApplyAsLockWallpaper)
                            showApplySheet = false
                        }) {
                            Icon(Icons.Default.Lock, contentDescription = null)
                        }
                    },
                    modifier = Modifier
                        .clip(RoundedCornerShape(12.dp))
                        .clickable {
                            viewModel.handleIntent(PreviewIntent.ApplyAsLockWallpaper)
                            showApplySheet = false
                        }
                )

                ListItem(
                    headlineContent = { Text("Both Screens") },
                    leadingContent = {
                        FilledTonalIconButton(onClick = {
                            viewModel.handleIntent(PreviewIntent.ApplyBothWallpaper)
                            showApplySheet = false
                        }) {
                            Icon(Icons.Default.PhoneAndroid, contentDescription = null)
                        }
                    },
                    modifier = Modifier
                        .clip(RoundedCornerShape(12.dp))
                        .clickable {
                            viewModel.handleIntent(PreviewIntent.ApplyBothWallpaper)
                            showApplySheet = false
                        }
                )

                HorizontalDivider(modifier = Modifier.padding(vertical = 4.dp))

                ListItem(
                    headlineContent = { Text("System / Samsung Cropper") },
                    supportingContent = { Text("Native One UI cropper with clock & lock screen widgets") },
                    leadingContent = {
                        FilledTonalIconButton(onClick = {
                            viewModel.handleIntent(PreviewIntent.ApplyWithSystemCropper)
                            showApplySheet = false
                        }) {
                            Icon(Icons.Default.Crop, contentDescription = null)
                        }
                    },
                    modifier = Modifier
                        .clip(RoundedCornerShape(12.dp))
                        .clickable {
                            viewModel.handleIntent(PreviewIntent.ApplyWithSystemCropper)
                            showApplySheet = false
                        }
                )
            }
        }
    }

    // Modal Sheet 2: Metadata & Tags Info
    if (showInfoSheet) {
        ModalBottomSheet(
            onDismissRequest = { showInfoSheet = false },
            shape = RoundedCornerShape(topStart = 24.dp, topEnd = 24.dp)
        ) {
            uiState.post?.let { post ->
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(horizontal = 20.dp)
                        .padding(bottom = 32.dp)
                        .verticalScroll(rememberScrollState()),
                    verticalArrangement = Arrangement.spacedBy(16.dp)
                ) {
                    Text(
                        text = "Wallpaper Details",
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.Bold
                    )

                    // Quick Stats Grid
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween
                    ) {
                        DetailChip(label = "Resolution", value = "${post.width} × ${post.height}")
                        DetailChip(label = "Score", value = "★ ${post.score}")
                        DetailChip(label = "Source", value = post.source.uppercase())
                        RatingBadge(rating = post.rating)
                    }

                    if (!post.author.isNullOrBlank()) {
                        Text(
                            text = "Artist: ${post.author}",
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }

                    // Extracted Color Palette
                    Text(
                        text = "Color Palette",
                        style = MaterialTheme.typography.titleSmall,
                        fontWeight = FontWeight.SemiBold
                    )
                    ColorPaletteRow(
                        post = post,
                        onColorClick = { hex ->
                            val clipboard = context.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
                            clipboard.setPrimaryClip(ClipData.newPlainText("Hex Color", hex))
                            Toast.makeText(context, "Copied $hex to clipboard", Toast.LENGTH_SHORT).show()
                        }
                    )

                    val tagList = remember(post.tags) { post.tags.split(" ").filter { it.isNotBlank() } }
                    if (tagList.isNotEmpty()) {
                        Text(
                            text = "Tags (${tagList.size})",
                            style = MaterialTheme.typography.titleSmall,
                            fontWeight = FontWeight.SemiBold
                        )

                        OptInFlowRow(
                            tags = tagList,
                            onTagClick = { tag ->
                                selectedTagAction = tag
                            }
                        )
                    }
                }
            }
        }
    }

    selectedTagAction?.let { tag ->
        AlertDialog(
            onDismissRequest = { selectedTagAction = null },
            title = { Text("Tag: $tag") },
            text = { Text("Choose an action for this tag:") },
            confirmButton = {
                TextButton(onClick = {
                    val target = tag
                    selectedTagAction = null
                    showInfoSheet = false
                    onTagClick(target)
                }) {
                    Text("Search Tag")
                }
            },
            dismissButton = {
                Row {
                    TextButton(onClick = {
                        val target = tag
                        selectedTagAction = null
                        viewModel.handleIntent(PreviewIntent.BlacklistTag(target))
                    }) {
                        Text("Add to Blacklist", color = MaterialTheme.colorScheme.error)
                    }
                    TextButton(onClick = { selectedTagAction = null }) {
                        Text("Cancel")
                    }
                }
            }
        )
    }
}

@Composable
private fun SimulationTabButton(
    selected: Boolean,
    label: String,
    onClick: () -> Unit
) {
    Surface(
        onClick = onClick,
        shape = RoundedCornerShape(50),
        color = if (selected) MaterialTheme.colorScheme.primary else Color.Transparent,
        contentColor = if (selected) MaterialTheme.colorScheme.onPrimary else Color.White.copy(alpha = 0.8f)
    ) {
        Text(
            text = label,
            fontSize = 12.sp,
            fontWeight = if (selected) FontWeight.Bold else FontWeight.Normal,
            modifier = Modifier.padding(horizontal = 12.dp, vertical = 6.dp)
        )
    }
}

@Composable
private fun LockScreenSimulationOverlay() {
    val currentTime = remember {
        SimpleDateFormat("HH:mm", Locale.getDefault()).format(Date())
    }
    val currentDate = remember {
        SimpleDateFormat("EEE, MMM d", Locale.getDefault()).format(Date())
    }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .statusBarsPadding()
            .navigationBarsPadding()
    ) {
        // Top Lock & Time
        Column(
            modifier = Modifier
                .align(Alignment.TopCenter)
                .padding(top = 40.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Icon(
                imageVector = Icons.Default.Lock,
                contentDescription = null,
                tint = Color.White.copy(alpha = 0.8f),
                modifier = Modifier.size(20.dp)
            )
            Spacer(modifier = Modifier.height(16.dp))
            Text(
                text = currentTime,
                fontSize = 76.sp,
                fontWeight = FontWeight.Light,
                color = Color.White,
                letterSpacing = (-1).sp
            )
            Text(
                text = currentDate,
                fontSize = 16.sp,
                fontWeight = FontWeight.Medium,
                color = Color.White.copy(alpha = 0.9f)
            )
        }

        // Bottom Shortcuts (Flashlight & Camera)
        Row(
            modifier = Modifier
                .align(Alignment.BottomCenter)
                .fillMaxWidth()
                .padding(horizontal = 32.dp, vertical = 24.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Surface(
                shape = CircleShape,
                color = Color.Black.copy(alpha = 0.45f),
                modifier = Modifier.size(50.dp)
            ) {
                Box(contentAlignment = Alignment.Center) {
                    Icon(
                        imageVector = Icons.Default.FlashOn,
                        contentDescription = "Flashlight",
                        tint = Color.White,
                        modifier = Modifier.size(22.dp)
                    )
                }
            }

            // Bottom Navigation Pill
            Box(
                modifier = Modifier
                    .width(110.dp)
                    .height(4.dp)
                    .clip(RoundedCornerShape(2.dp))
                    .background(Color.White.copy(alpha = 0.7f))
            )

            Surface(
                shape = CircleShape,
                color = Color.Black.copy(alpha = 0.45f),
                modifier = Modifier.size(50.dp)
            ) {
                Box(contentAlignment = Alignment.Center) {
                    Icon(
                        imageVector = Icons.Default.CameraAlt,
                        contentDescription = "Camera",
                        tint = Color.White,
                        modifier = Modifier.size(22.dp)
                    )
                }
            }
        }
    }
}

@Composable
private fun HomeScreenSimulationOverlay() {
    Box(
        modifier = Modifier
            .fillMaxSize()
            .statusBarsPadding()
            .navigationBarsPadding()
    ) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(horizontal = 24.dp),
            verticalArrangement = Arrangement.SpaceBetween
        ) {
            // Search Pill Widget at top
            Surface(
                shape = RoundedCornerShape(28.dp),
                color = Color.White.copy(alpha = 0.28f),
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(top = 32.dp)
                    .height(52.dp)
            ) {
                Row(
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(horizontal = 16.dp),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    Icon(
                        imageVector = Icons.Default.Search,
                        contentDescription = null,
                        tint = Color.White,
                        modifier = Modifier.size(20.dp)
                    )
                    Text(
                        text = "Search apps & web...",
                        color = Color.White.copy(alpha = 0.8f),
                        fontSize = 14.sp,
                        modifier = Modifier.weight(1f)
                    )
                    Icon(
                        imageVector = Icons.Default.Mic,
                        contentDescription = null,
                        tint = Color.White,
                        modifier = Modifier.size(20.dp)
                    )
                    Icon(
                        imageVector = Icons.Default.CameraAlt,
                        contentDescription = null,
                        tint = Color.White,
                        modifier = Modifier.size(20.dp)
                    )
                }
            }

            // Central Mock App Grid (2 rows x 4 items)
            Column(
                modifier = Modifier.fillMaxWidth(),
                verticalArrangement = Arrangement.spacedBy(20.dp)
            ) {
                val appRows = listOf(
                    listOf(
                        "Settings" to Icons.Default.Settings,
                        "Music" to Icons.Default.MusicNote,
                        "Gallery" to Icons.Default.Image,
                        "Apps" to Icons.Default.Apps
                    )
                )

                appRows.forEach { row ->
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceAround
                    ) {
                        row.forEach { (name, icon) ->
                            Column(
                                horizontalAlignment = Alignment.CenterHorizontally,
                                verticalArrangement = Arrangement.spacedBy(4.dp)
                            ) {
                                Surface(
                                    shape = RoundedCornerShape(16.dp),
                                    color = Color.White.copy(alpha = 0.35f),
                                    modifier = Modifier.size(52.dp)
                                ) {
                                    Box(contentAlignment = Alignment.Center) {
                                        Icon(
                                            imageVector = icon,
                                            contentDescription = name,
                                            tint = Color.White,
                                            modifier = Modifier.size(24.dp)
                                        )
                                    }
                                }
                                Text(
                                    text = name,
                                    color = Color.White,
                                    fontSize = 11.sp,
                                    fontWeight = FontWeight.Medium
                                )
                            }
                        }
                    }
                }
            }

            // Bottom Dock & Navigation Pill
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(bottom = 12.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.spacedBy(16.dp)
            ) {
                // Dock row
                Surface(
                    shape = RoundedCornerShape(24.dp),
                    color = Color.Black.copy(alpha = 0.35f),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(vertical = 10.dp),
                        horizontalArrangement = Arrangement.SpaceAround,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        listOf(
                            Icons.Default.Phone,
                            Icons.Default.Email,
                            Icons.Default.Language,
                            Icons.Default.CameraAlt
                        ).forEach { icon ->
                            Surface(
                                shape = CircleShape,
                                color = Color.White.copy(alpha = 0.35f),
                                modifier = Modifier.size(44.dp)
                            ) {
                                Box(contentAlignment = Alignment.Center) {
                                    Icon(
                                        imageVector = icon,
                                        contentDescription = null,
                                        tint = Color.White,
                                        modifier = Modifier.size(22.dp)
                                    )
                                }
                            }
                        }
                    }
                }

                // Bottom gesture nav pill
                Box(
                    modifier = Modifier
                        .width(110.dp)
                        .height(4.dp)
                        .clip(RoundedCornerShape(2.dp))
                        .background(Color.White.copy(alpha = 0.7f))
                )
            }
        }
    }
}

@Composable
private fun ColorPaletteRow(
    post: PostItem,
    onColorClick: (String) -> Unit
) {
    val colors = remember(post.id) {
        val baseHue = ((post.id.hashCode() and 0x7FFFFFFF) % 360).toFloat()
        listOf(
            Color.hsv(baseHue, 0.65f, 0.90f),
            Color.hsv((baseHue + 40f) % 360f, 0.55f, 0.85f),
            Color.hsv((baseHue + 120f) % 360f, 0.45f, 0.95f),
            Color.hsv((baseHue + 200f) % 360f, 0.70f, 0.75f),
            Color.hsv((baseHue + 280f) % 360f, 0.50f, 0.90f)
        )
    }

    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        colors.forEach { color ->
            val red = (color.red * 255).toInt()
            val green = (color.green * 255).toInt()
            val blue = (color.blue * 255).toInt()
            val hex = String.format(Locale.US, "#%02X%02X%02X", red, green, blue)
            val isLight = (color.red * 0.299f + color.green * 0.587f + color.blue * 0.114f) > 0.5f

            Surface(
                modifier = Modifier
                    .weight(1f)
                    .height(46.dp)
                    .clip(RoundedCornerShape(12.dp))
                    .clickable { onColorClick(hex) },
                color = color,
                shape = RoundedCornerShape(12.dp),
                border = BorderStroke(1.dp, Color.White.copy(alpha = 0.2f))
            ) {
                Box(
                    contentAlignment = Alignment.Center,
                    modifier = Modifier.fillMaxSize()
                ) {
                    Text(
                        text = hex,
                        fontSize = 9.sp,
                        fontWeight = FontWeight.Bold,
                        color = if (isLight) Color.Black.copy(alpha = 0.85f) else Color.White
                    )
                }
            }
        }
    }
}

@Composable
private fun DetailChip(label: String, value: String) {
    Column {
        Text(
            text = label,
            style = MaterialTheme.typography.labelSmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
        Text(
            text = value,
            style = MaterialTheme.typography.bodySmall,
            fontWeight = FontWeight.SemiBold
        )
    }
}

@OptIn(ExperimentalLayoutApi::class)
@Composable
private fun OptInFlowRow(
    tags: List<String>,
    onTagClick: (String) -> Unit = {}
) {
    FlowRow(
        horizontalArrangement = Arrangement.spacedBy(6.dp),
        verticalArrangement = Arrangement.spacedBy(6.dp)
    ) {
        tags.take(30).forEach { tag ->
            SuggestionChip(
                onClick = { onTagClick(tag) },
                label = { Text(tag, fontSize = 11.sp) }
            )
        }
    }
}
