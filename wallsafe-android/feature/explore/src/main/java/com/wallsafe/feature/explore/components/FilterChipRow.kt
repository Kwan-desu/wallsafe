package com.wallsafe.feature.explore.components

import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.Sort
import androidx.compose.material.icons.filled.AspectRatio
import androidx.compose.material.icons.filled.HighQuality
import androidx.compose.material.icons.filled.Shield
import androidx.compose.material.icons.filled.Wallpaper
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.FilterChip
import androidx.compose.material3.FilterChipDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.wallsafe.core.model.ContentRatingMode

@Composable
fun FilterChipRow(
    ratingMode: ContentRatingMode,
    onRatingModeSelected: (ContentRatingMode) -> Unit,
    sortOrder: String,
    onSortOrderSelected: (String) -> Unit,
    resolutionFilter: Int,
    onResolutionFilterSelected: (Int) -> Unit,
    aspectRatioFilter: Int,
    onAspectRatioFilterSelected: (Int) -> Unit,
    isWallpaperTagOnly: Boolean = false,
    onToggleWallpaperTag: (Boolean) -> Unit = {},
    modifier: Modifier = Modifier
) {
    val scrollState = rememberScrollState()
    var ratingExpanded by remember { mutableStateOf(false) }
    var sortExpanded by remember { mutableStateOf(false) }
    var resExpanded by remember { mutableStateOf(false) }
    var aspectExpanded by remember { mutableStateOf(false) }

    val sortOptions = listOf("score", "newest", "wallpaper", "random")
    val resOptions = listOf("Any", "1080p+", "1440p+", "4K+")
    val aspectOptions = listOf("Any", "16:9", "Ultrawide", "4:3", "Portrait")

    Row(
        modifier = modifier
            .horizontalScroll(scrollState)
            .padding(horizontal = 16.dp, vertical = 4.dp),
        horizontalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        // 0. Wallpaper Filter Chip
        FilterChip(
            selected = isWallpaperTagOnly,
            onClick = { onToggleWallpaperTag(!isWallpaperTagOnly) },
            leadingIcon = {
                Icon(
                    imageVector = Icons.Default.Wallpaper,
                    contentDescription = "Wallpaper Only",
                    modifier = Modifier.size(16.dp)
                )
            },
            label = {
                Text(
                    text = "Wallpaper Only",
                    fontSize = 12.sp,
                    fontWeight = if (isWallpaperTagOnly) FontWeight.Bold else FontWeight.Normal
                )
            }
        )
        // 1. Rating Dropdown Chip
        Box {
            FilterChip(
                selected = ratingMode != ContentRatingMode.SfwOnly,
                onClick = { ratingExpanded = true },
                leadingIcon = {
                    Icon(
                        imageVector = Icons.Default.Shield,
                        contentDescription = "Rating",
                        modifier = Modifier.size(16.dp)
                    )
                },
                label = {
                    Text(
                        text = when (ratingMode) {
                            ContentRatingMode.SfwOnly -> "SFW"
                            ContentRatingMode.Questionable -> "16+"
                            ContentRatingMode.Explicit -> "NSFW"
                            else -> "Custom"
                        },
                        fontSize = 12.sp
                    )
                }
            )
            DropdownMenu(
                expanded = ratingExpanded,
                onDismissRequest = { ratingExpanded = false }
            ) {
                DropdownMenuItem(
                    text = { Text("Safe (SFW)") },
                    onClick = {
                        onRatingModeSelected(ContentRatingMode.SfwOnly)
                        ratingExpanded = false
                    }
                )
                DropdownMenuItem(
                    text = { Text("Questionable (16+)") },
                    onClick = {
                        onRatingModeSelected(ContentRatingMode.Questionable)
                        ratingExpanded = false
                    }
                )
                DropdownMenuItem(
                    text = { Text("Explicit (NSFW)") },
                    onClick = {
                        onRatingModeSelected(ContentRatingMode.Explicit)
                        ratingExpanded = false
                    }
                )
            }
        }

        // 2. Sort Dropdown Chip
        Box {
            FilterChip(
                selected = sortOrder != "score",
                onClick = { sortExpanded = true },
                leadingIcon = {
                    Icon(
                        imageVector = Icons.AutoMirrored.Filled.Sort,
                        contentDescription = "Sort",
                        modifier = Modifier.size(16.dp)
                    )
                },
                label = {
                    val labelText = when (sortOrder) {
                        "wallpaper" -> "Wallpaper Tag"
                        else -> sortOrder.replaceFirstChar { it.uppercase() }
                    }
                    Text(
                        text = labelText,
                        fontSize = 12.sp
                    )
                }
            )
            DropdownMenu(
                expanded = sortExpanded,
                onDismissRequest = { sortExpanded = false }
            ) {
                sortOptions.forEach { option ->
                    val optionText = when (option) {
                        "wallpaper" -> "Wallpaper Tag"
                        else -> option.replaceFirstChar { it.uppercase() }
                    }
                    DropdownMenuItem(
                        text = { Text(optionText) },
                        onClick = {
                            onSortOrderSelected(option)
                            sortExpanded = false
                        }
                    )
                }
            }
        }

        // 3. Resolution Dropdown Chip
        Box {
            FilterChip(
                selected = resolutionFilter != 0,
                onClick = { resExpanded = true },
                leadingIcon = {
                    Icon(
                        imageVector = Icons.Default.HighQuality,
                        contentDescription = "Resolution",
                        modifier = Modifier.size(16.dp)
                    )
                },
                label = {
                    Text(
                        text = resOptions[resolutionFilter],
                        fontSize = 12.sp
                    )
                }
            )
            DropdownMenu(
                expanded = resExpanded,
                onDismissRequest = { resExpanded = false }
            ) {
                resOptions.forEachIndexed { index, option ->
                    DropdownMenuItem(
                        text = { Text(option) },
                        onClick = {
                            onResolutionFilterSelected(index)
                            resExpanded = false
                        }
                    )
                }
            }
        }

        // 4. Aspect Ratio Dropdown Chip
        Box {
            FilterChip(
                selected = aspectRatioFilter != 0,
                onClick = { aspectExpanded = true },
                leadingIcon = {
                    Icon(
                        imageVector = Icons.Default.AspectRatio,
                        contentDescription = "Aspect Ratio",
                        modifier = Modifier.size(16.dp)
                    )
                },
                label = {
                    Text(
                        text = aspectOptions[aspectRatioFilter],
                        fontSize = 12.sp
                    )
                }
            )
            DropdownMenu(
                expanded = aspectExpanded,
                onDismissRequest = { aspectExpanded = false }
            ) {
                aspectOptions.forEachIndexed { index, option ->
                    DropdownMenuItem(
                        text = { Text(option) },
                        onClick = {
                            onAspectRatioFilterSelected(index)
                            aspectExpanded = false
                        }
                    )
                }
            }
        }
    }
}
