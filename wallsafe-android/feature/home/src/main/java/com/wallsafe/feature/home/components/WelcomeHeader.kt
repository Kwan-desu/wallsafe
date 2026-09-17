package com.wallsafe.feature.home.components

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowDownward
import androidx.compose.material.icons.filled.DarkMode
import androidx.compose.material.icons.filled.Favorite
import androidx.compose.material.icons.filled.History
import androidx.compose.material.icons.outlined.StarBorder
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
    val isSystemDark = isSystemInDarkTheme()
    val isDark = when (themeMode.lowercase()) {
        "dark" -> true
        "light" -> false
        else -> isSystemDark
    }

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
                        fontSize = 28.sp,
                        letterSpacing = (-0.5).sp
                    ),
                    color = if (isDark) Color(0xFFF2F2F5) else Color(0xFF1E1D24)
                )
            }

            // Sleek Crescent Moon Theme Toggle Button
            IconButton(
                onClick = onToggleTheme,
                modifier = Modifier.size(40.dp)
            ) {
                Icon(
                    imageVector = Icons.Default.DarkMode,
                    contentDescription = "Toggle Dark Mode",
                    tint = if (isDark) Color(0xFFB388FF) else Color(0xFF6E6B7B),
                    modifier = Modifier.size(24.dp)
                )
            }
        }

        // 2. Overview Statistics Dashboard Strip (Favorites, Downloads, Applied, Top pick)
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
                    isDark = isDark,
                    modifier = Modifier.weight(1f)
                )
                OverviewStatsCard(
                    icon = Icons.Filled.ArrowDownward,
                    value = downloadsCount.toString(),
                    label = "Downloads",
                    onClick = onDownloadsClick,
                    isDark = isDark,
                    modifier = Modifier.weight(1f)
                )
                OverviewStatsCard(
                    icon = Icons.Filled.History,
                    value = appliedCount.toString(),
                    label = "Applied",
                    onClick = onAppliedClick,
                    isDark = isDark,
                    modifier = Modifier.weight(1f)
                )
                OverviewStatsCard(
                    icon = Icons.Outlined.StarBorder,
                    value = topPickTitle,
                    label = "Top pick",
                    onClick = onTopPickClick,
                    isDark = isDark,
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
                        isDark = isDark,
                        modifier = Modifier.weight(1f)
                    )
                    OverviewStatsCard(
                        icon = Icons.Filled.ArrowDownward,
                        value = downloadsCount.toString(),
                        label = "Downloads",
                        onClick = onDownloadsClick,
                        isDark = isDark,
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
                        isDark = isDark,
                        modifier = Modifier.weight(1f)
                    )
                    OverviewStatsCard(
                        icon = Icons.Outlined.StarBorder,
                        value = topPickTitle,
                        label = "Top pick",
                        onClick = onTopPickClick,
                        isDark = isDark,
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
    isDark: Boolean,
    modifier: Modifier = Modifier
) {
    val cardBg = if (isDark) Color(0xFF1B1D27) else Color.White
    val iconBoxBg = if (isDark) Color(0xFF252736) else Color(0xFFEDE9FE)
    val iconColor = Color(0xFF8B80F8)
    val textColor = if (isDark) Color(0xFFF3F4F8) else Color(0xFF1E1D24)
    val labelColor = if (isDark) Color(0xFF8E90A2) else Color(0xFF6B7280)
    val borderColor = if (isDark) Color(0xFF2A2C3A) else Color(0xFFE5E7EB)

    Surface(
        onClick = onClick,
        shape = RoundedCornerShape(16.dp),
        color = cardBg,
        border = BorderStroke(1.dp, borderColor),
        shadowElevation = if (isDark) 0.dp else 2.dp,
        modifier = modifier.height(68.dp)
    ) {
        Row(
            modifier = Modifier
                .fillMaxSize()
                .padding(horizontal = 14.dp, vertical = 10.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            Box(
                modifier = Modifier
                    .size(42.dp)
                    .clip(RoundedCornerShape(12.dp))
                    .background(iconBoxBg),
                contentAlignment = Alignment.Center
            ) {
                Icon(
                    imageVector = icon,
                    contentDescription = label,
                    tint = iconColor,
                    modifier = Modifier.size(20.dp)
                )
            }

            Column(
                verticalArrangement = Arrangement.Center
            ) {
                Text(
                    text = value,
                    style = MaterialTheme.typography.titleMedium.copy(
                        fontWeight = FontWeight.Bold,
                        fontSize = 17.sp,
                        letterSpacing = (-0.3).sp
                    ),
                    color = textColor,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis
                )
                Text(
                    text = label,
                    style = MaterialTheme.typography.bodySmall.copy(
                        fontSize = 11.sp,
                        fontWeight = FontWeight.Medium
                    ),
                    color = labelColor,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis
                )
            }
        }
    }
}
