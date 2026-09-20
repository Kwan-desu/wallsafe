package com.wallsafe.core.ui.components

import android.content.res.Configuration
import androidx.compose.animation.core.Spring
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.spring
import androidx.compose.foundation.ExperimentalFoundationApi
import androidx.compose.foundation.background
import androidx.compose.foundation.combinedClickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.OpenInNew
import androidx.compose.material.icons.filled.Download
import androidx.compose.material.icons.filled.Favorite
import androidx.compose.material.icons.filled.FavoriteBorder
import androidx.compose.material.icons.filled.Star
import androidx.compose.material.icons.filled.Visibility
import androidx.compose.material.icons.filled.Wallpaper
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.blur
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.scale
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import coil3.compose.AsyncImage
import com.wallsafe.core.model.PostItem

@OptIn(ExperimentalFoundationApi::class)
@Composable
fun WallpaperCard(
    post: PostItem,
    onTap: () -> Unit,
    onFavoriteToggle: () -> Unit,
    onApplyWallpaper: () -> Unit,
    onDownload: () -> Unit,
    onOpenSource: () -> Unit,
    modifier: Modifier = Modifier,
    isFavorite: Boolean = false,
    isDownloaded: Boolean = false,
    isBlurEnabled: Boolean = false,
    cardAspectRatio: Float? = null
) {
    val configuration = LocalConfiguration.current
    val isLandscape = configuration.orientation == Configuration.ORIENTATION_LANDSCAPE
    val isTablet = configuration.screenWidthDp >= 600

    val effectiveAspectRatio = cardAspectRatio ?: when {
        isLandscape -> 16f / 10f // Landscape widescreen wallpaper format (1.6)
        isTablet -> 16f / 10f // Tablets have large screens, ideal for widescreen wallpapers
        else -> 0.75f // Mobile phone portrait format (3:4)
    }

    val hasWallpaperTag = remember(post.tags) {
        post.tags.split(" ").any { it.equals("wallpaper", ignoreCase = true) }
    }

    var showMenu by remember { mutableStateOf(false) }
    var isTemporarilyRevealed by remember { mutableStateOf(false) }

    val shouldBlur = isBlurEnabled && 
                     post.rating.lowercase() != "s" && 
                     post.rating.lowercase() != "safe" && 
                     !isTemporarilyRevealed

    val hapticFeedback = androidx.compose.ui.platform.LocalHapticFeedback.current
    var showHeartBurst by remember { mutableStateOf(false) }

    val scale by animateFloatAsState(
        targetValue = if (isFavorite) 1.25f else 1f,
        animationSpec = spring(
            dampingRatio = Spring.DampingRatioMediumBouncy,
            stiffness = Spring.StiffnessLow
        ),
        label = "favorite_scale"
    )

    ElevatedCard(
        modifier = modifier
            .fillMaxWidth()
            .aspectRatio(effectiveAspectRatio)
            .combinedClickable(
                onClick = onTap,
                onLongClick = { showMenu = true },
                onDoubleClick = {
                    showHeartBurst = true
                    hapticFeedback.performHapticFeedback(androidx.compose.ui.hapticfeedback.HapticFeedbackType.LongPress)
                    if (!isFavorite) {
                        onFavoriteToggle()
                    }
                }
            ),
        shape = RoundedCornerShape(18.dp),
        elevation = CardDefaults.elevatedCardElevation(defaultElevation = 2.dp)
    ) {
        Box(modifier = Modifier.fillMaxSize()) {
            // Background Image
            AsyncImage(
                model = post.thumbnailUrl,
                contentDescription = "Wallpaper thumbnail",
                contentScale = ContentScale.Crop,
                modifier = Modifier
                    .fillMaxSize()
                    .then(if (shouldBlur) Modifier.blur(22.dp) else Modifier)
            )

            // Heart burst animation overlay on double tap
            HeartBurstAnimation(
                trigger = showHeartBurst,
                onAnimationEnd = { showHeartBurst = false },
                size = 64.dp
            )

            // Blur Reveal Overlay Button if blurred
            if (shouldBlur) {
                Surface(
                    onClick = { isTemporarilyRevealed = true },
                    shape = RoundedCornerShape(50),
                    color = Color.Black.copy(alpha = 0.7f),
                    modifier = Modifier.align(Alignment.Center)
                ) {
                    Row(
                        modifier = Modifier.padding(horizontal = 12.dp, vertical = 6.dp),
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.spacedBy(6.dp)
                    ) {
                        Icon(
                            imageVector = Icons.Default.Visibility,
                            contentDescription = "Reveal",
                            tint = Color.White,
                            modifier = Modifier.size(16.dp)
                        )
                        Text(
                            text = "Reveal",
                            color = Color.White,
                            fontSize = 12.sp,
                            fontWeight = FontWeight.SemiBold
                        )
                    }
                }
            }

            // Top Badges Row
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(8.dp)
                    .align(Alignment.TopStart),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                if (post.rating.lowercase() != "s" && post.rating.lowercase() != "safe") {
                    RatingBadge(rating = post.rating)
                } else {
                    Spacer(modifier = Modifier.width(1.dp))
                }

                // Score Chip
                Box(
                    modifier = Modifier
                        .clip(RoundedCornerShape(50))
                        .background(Color.Black.copy(alpha = 0.55f))
                        .padding(horizontal = 7.dp, vertical = 3.dp)
                ) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Icon(
                            imageVector = Icons.Default.Star,
                            contentDescription = "Score",
                            modifier = Modifier.size(11.dp),
                            tint = Color(0xFFFFC107)
                        )
                        Spacer(modifier = Modifier.width(3.dp))
                        Text(
                            text = "${post.score}",
                            color = Color.White,
                            fontSize = 11.sp,
                            fontWeight = FontWeight.Medium
                        )
                    }
                }
            }

            // Bottom Gradient Scrim & Info/Actions (Clean & High Density)
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .align(Alignment.BottomCenter)
                    .background(
                        Brush.verticalGradient(
                            colors = listOf(
                                Color.Transparent,
                                Color.Black.copy(alpha = 0.25f),
                                Color.Black.copy(alpha = 0.75f)
                            )
                        )
                    )
                    .padding(horizontal = 8.dp, vertical = 6.dp)
            ) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    val resLabel = when {
                        post.width >= 3840 || post.height >= 2160 -> "4K"
                        post.width >= 2560 || post.height >= 1440 -> "2K"
                        post.width >= 1920 || post.height >= 1080 -> "FHD"
                        post.width > 0 -> "${post.width}p"
                        else -> "HD"
                    }
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.spacedBy(4.dp)
                    ) {
                        Surface(
                            shape = RoundedCornerShape(6.dp),
                            color = Color.Black.copy(alpha = 0.45f),
                            contentColor = Color.White
                        ) {
                            Text(
                                text = resLabel,
                                fontSize = 10.sp,
                                fontWeight = FontWeight.Bold,
                                modifier = Modifier.padding(horizontal = 5.dp, vertical = 2.dp)
                            )
                        }

                        if (hasWallpaperTag) {
                            Surface(
                                shape = RoundedCornerShape(6.dp),
                                color = MaterialTheme.colorScheme.primary.copy(alpha = 0.85f),
                                contentColor = Color.White
                            ) {
                                Row(
                                    modifier = Modifier.padding(horizontal = 5.dp, vertical = 2.dp),
                                    verticalAlignment = Alignment.CenterVertically,
                                    horizontalArrangement = Arrangement.spacedBy(2.dp)
                                ) {
                                    Icon(
                                        imageVector = Icons.Default.Wallpaper,
                                        contentDescription = "Wallpaper Tag",
                                        modifier = Modifier.size(10.dp),
                                        tint = Color.White
                                    )
                                    Text(
                                        text = "WP",
                                        fontSize = 10.sp,
                                        fontWeight = FontWeight.Bold
                                    )
                                }
                            }
                        }
                    }

                    // Single favorite icon
                    IconButton(
                        onClick = onFavoriteToggle,
                        modifier = Modifier.size(32.dp)
                    ) {
                        Icon(
                            imageVector = if (isFavorite) Icons.Default.Favorite else Icons.Default.FavoriteBorder,
                            contentDescription = "Favorite",
                            tint = if (isFavorite) Color(0xFFFF3B30) else Color.White,
                            modifier = Modifier
                                .size(18.dp)
                                .scale(scale)
                        )
                    }
                }
            }

            // Dropdown Menu
            DropdownMenu(
                expanded = showMenu,
                onDismissRequest = { showMenu = false }
            ) {
                DropdownMenuItem(
                    text = { Text("Open in Browser") },
                    onClick = {
                        showMenu = false
                        onOpenSource()
                    }
                )
                DropdownMenuItem(
                    text = { Text(if (isFavorite) "Remove from Favorites" else "Add to Favorites") },
                    onClick = {
                        showMenu = false
                        onFavoriteToggle()
                    }
                )
                DropdownMenuItem(
                    text = { Text("Set as Wallpaper") },
                    onClick = {
                        showMenu = false
                        onApplyWallpaper()
                    }
                )
            }
        }
    }
}
