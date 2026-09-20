package com.wallsafe.feature.home.components

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowDownward
import androidx.compose.material.icons.filled.AutoAwesome
import androidx.compose.material.icons.filled.DarkMode
import androidx.compose.material.icons.filled.Favorite
import androidx.compose.material.icons.filled.History
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

@Composable
fun WelcomeHeader(
    userName: String,
    themeMode: String,
    favoritesCount: Int,
    downloadsCount: Int,
    appliedCount: Int,
    topPickTitle: String,
    onToggleTheme: () -> Unit,
    onSetUserName: (String) -> Unit,
    onFavoritesClick: () -> Unit,
    onDownloadsClick: () -> Unit,
    onAppliedClick: () -> Unit,
    onTopPickClick: () -> Unit,
    isTabletOrLandscape: Boolean = false,
    isGreetingNameEnabled: Boolean = true,
    modifier: Modifier = Modifier
) {
    var showNameDialog by remember { mutableStateOf(false) }

    if (showNameDialog) {
        var inputName by remember { mutableStateOf(userName) }
        AlertDialog(
            onDismissRequest = { showNameDialog = false },
            title = { Text("What should we call you?") },
            text = {
                OutlinedTextField(
                    value = inputName,
                    onValueChange = { inputName = it },
                    label = { Text("Your Name") },
                    placeholder = { Text("e.g. Alex") },
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth()
                )
            },
            confirmButton = {
                Button(
                    onClick = {
                        onSetUserName(inputName.trim())
                        showNameDialog = false
                    }
                ) {
                    Text("Save")
                }
            },
            dismissButton = {
                TextButton(onClick = { showNameDialog = false }) {
                    Text("Cancel")
                }
            }
        )
    }

    val greetingText = if (isGreetingNameEnabled && userName.isNotBlank()) {
        "Hi, $userName 👋"
    } else {
        "Welcome 👋"
    }

    Column(
        modifier = modifier.fillMaxWidth(),
        verticalArrangement = Arrangement.spacedBy(16.dp)
    ) {
        // 1. Top Greeting Row + Theme Toggle Crescent Moon
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = 4.dp, bottom = 2.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(8.dp),
                modifier = Modifier
                    .clip(RoundedCornerShape(12.dp))
                    .clickable { showNameDialog = true }
                    .padding(vertical = 4.dp, horizontal = 4.dp)
            ) {
                Text(
                    text = greetingText,
                    style = MaterialTheme.typography.headlineMedium.copy(
                        fontWeight = FontWeight.Bold,
                        letterSpacing = (-0.5).sp
                    ),
                    color = MaterialTheme.colorScheme.onBackground
                )
            }

            // Material 3 Styled Crescent Moon Theme Toggle Button
            FilledTonalIconButton(
                onClick = onToggleTheme,
                colors = IconButtonDefaults.filledTonalIconButtonColors(
                    containerColor = MaterialTheme.colorScheme.surfaceContainerHigh,
                    contentColor = MaterialTheme.colorScheme.primary
                ),
                modifier = Modifier.size(42.dp)
            ) {
                Icon(
                    imageVector = Icons.Default.DarkMode,
                    contentDescription = "Toggle Dark Mode",
                    modifier = Modifier.size(22.dp)
                )
            }
        }

        // 2. Material You Overview Statistics Dashboard Strip (Favorites, Downloads, Applied, Top pick)
        if (isTabletOrLandscape) {
            // Tablet / Landscape: All 4 in a wide row
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(10.dp)
            ) {
                OverviewStatsCard(
                    icon = Icons.Filled.Favorite,
                    value = favoritesCount.toString(),
                    label = "Favorites",
                    onClick = onFavoritesClick,
                    iconContainerColor = MaterialTheme.colorScheme.primaryContainer,
                    iconColor = MaterialTheme.colorScheme.onPrimaryContainer,
                    modifier = Modifier.weight(1f)
                )
                OverviewStatsCard(
                    icon = Icons.Filled.ArrowDownward,
                    value = downloadsCount.toString(),
                    label = "Downloads",
                    onClick = onDownloadsClick,
                    iconContainerColor = MaterialTheme.colorScheme.secondaryContainer,
                    iconColor = MaterialTheme.colorScheme.onSecondaryContainer,
                    modifier = Modifier.weight(1f)
                )
                OverviewStatsCard(
                    icon = Icons.Filled.History,
                    value = appliedCount.toString(),
                    label = "Applied",
                    onClick = onAppliedClick,
                    iconContainerColor = MaterialTheme.colorScheme.tertiaryContainer,
                    iconColor = MaterialTheme.colorScheme.onTertiaryContainer,
                    modifier = Modifier.weight(1f)
                )
                OverviewStatsCard(
                    icon = Icons.Default.AutoAwesome,
                    value = topPickTitle,
                    label = "Top pick",
                    onClick = onTopPickClick,
                    iconContainerColor = MaterialTheme.colorScheme.surfaceContainerHighest,
                    iconColor = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.weight(1.3f)
                )
            }
        } else {
            // Portrait Phone: 2x2 grid
            Column(
                modifier = Modifier.fillMaxWidth(),
                verticalArrangement = Arrangement.spacedBy(10.dp)
            ) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(10.dp)
                ) {
                    OverviewStatsCard(
                        icon = Icons.Filled.Favorite,
                        value = favoritesCount.toString(),
                        label = "Favorites",
                        onClick = onFavoritesClick,
                        iconContainerColor = MaterialTheme.colorScheme.primaryContainer,
                        iconColor = MaterialTheme.colorScheme.onPrimaryContainer,
                        modifier = Modifier.weight(1f)
                    )
                    OverviewStatsCard(
                        icon = Icons.Filled.ArrowDownward,
                        value = downloadsCount.toString(),
                        label = "Downloads",
                        onClick = onDownloadsClick,
                        iconContainerColor = MaterialTheme.colorScheme.secondaryContainer,
                        iconColor = MaterialTheme.colorScheme.onSecondaryContainer,
                        modifier = Modifier.weight(1f)
                    )
                }
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(10.dp)
                ) {
                    OverviewStatsCard(
                        icon = Icons.Filled.History,
                        value = appliedCount.toString(),
                        label = "Applied",
                        onClick = onAppliedClick,
                        iconContainerColor = MaterialTheme.colorScheme.tertiaryContainer,
                        iconColor = MaterialTheme.colorScheme.onTertiaryContainer,
                        modifier = Modifier.weight(1f)
                    )
                    OverviewStatsCard(
                        icon = Icons.Default.AutoAwesome,
                        value = topPickTitle,
                        label = "Top pick",
                        onClick = onTopPickClick,
                        iconContainerColor = MaterialTheme.colorScheme.surfaceContainerHighest,
                        iconColor = MaterialTheme.colorScheme.primary,
                        modifier = Modifier.weight(1f)
                    )
                }
            }
        }
    }
}

@Composable
fun OverviewStatsCard(
    icon: ImageVector,
    value: String,
    label: String,
    onClick: () -> Unit,
    iconContainerColor: Color,
    iconColor: Color,
    modifier: Modifier = Modifier
) {
    OutlinedCard(
        onClick = onClick,
        shape = RoundedCornerShape(20.dp),
        colors = CardDefaults.outlinedCardColors(
            containerColor = MaterialTheme.colorScheme.surfaceContainerLow
        ),
        border = BorderStroke(
            width = 1.dp,
            color = MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.5f)
        ),
        modifier = modifier.height(72.dp)
    ) {
        Row(
            modifier = Modifier
                .fillMaxSize()
                .padding(horizontal = 12.dp, vertical = 10.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            Box(
                modifier = Modifier
                    .size(42.dp)
                    .clip(RoundedCornerShape(13.dp))
                    .background(iconContainerColor),
                contentAlignment = Alignment.Center
            ) {
                Icon(
                    imageVector = icon,
                    contentDescription = label,
                    tint = iconColor,
                    modifier = Modifier.size(22.dp)
                )
            }

            Column(
                modifier = Modifier.weight(1f, fill = false),
                verticalArrangement = Arrangement.Center
            ) {
                Text(
                    text = value,
                    style = MaterialTheme.typography.titleMedium.copy(
                        fontWeight = FontWeight.Bold,
                        letterSpacing = (-0.2).sp
                    ),
                    color = MaterialTheme.colorScheme.onSurface,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis
                )
                Text(
                    text = label,
                    style = MaterialTheme.typography.labelSmall.copy(
                        fontWeight = FontWeight.Medium
                    ),
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis
                )
            }
        }
    }
}
