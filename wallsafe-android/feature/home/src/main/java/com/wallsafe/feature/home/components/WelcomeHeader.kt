package com.wallsafe.feature.home.components

import androidx.compose.animation.animateColorAsState
import androidx.compose.animation.core.tween
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.DarkMode
import androidx.compose.material.icons.filled.Edit
import androidx.compose.material.icons.filled.LightMode
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.*
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import kotlin.math.PI
import kotlin.math.cos
import kotlin.math.sin

enum class GeometricBadgeShape {
    TRIANGLE,
    CIRCLE,
    HEXAGON,
    STAR
}

@Composable
fun WelcomeHeader(
    userName: String,
    themeMode: String,
    selectedFilter: String,
    onToggleTheme: () -> Unit,
    onSetFilter: (String) -> Unit,
    onSetUserName: (String) -> Unit,
    onDiscoverClick: () -> Unit,
    onSeriesClick: () -> Unit,
    onFavoritesClick: () -> Unit,
    onDownloadsClick: () -> Unit,
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
        // 1. Top Greeting Row + Theme Toggle Moon
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = 8.dp, bottom = 4.dp),
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

            // Sleek Crescent Moon / Theme Toggle Icon Button
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

        // 2. Segmented Pill Filter Bar: [ 🙂 All | 👨 Male | 👩 Fem. ]
        SegmentedFilterBar(
            selectedFilter = selectedFilter,
            onSelectFilter = onSetFilter,
            isDark = isDark
        )

        // 3. 2x2 Soft Pastel Gradient Cards Grid
        if (isTabletOrLandscape) {
            // Tablet / Landscape: 4 cards in a row
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(14.dp)
            ) {
                WelcomeCard(
                    title = "Discover\nFeed",
                    shape = GeometricBadgeShape.TRIANGLE,
                    lightGradient = listOf(Color(0xFFE4DCF9), Color(0xFFD6C8F6)),
                    darkGradient = listOf(Color(0xFF2A273A), Color(0xFF201D2E)),
                    lightBlob = Color(0xFFC7B6F0),
                    darkBlob = Color(0xFF3B3554),
                    titleLight = Color(0xFF2D264B),
                    titleDark = Color(0xFFDDD7F5),
                    onClick = onDiscoverClick,
                    isDark = isDark,
                    modifier = Modifier.weight(1f)
                )
                WelcomeCard(
                    title = "Popular\nSeries",
                    shape = GeometricBadgeShape.CIRCLE,
                    lightGradient = listOf(Color(0xFFFFD4E2), Color(0xFFFFBCD2)),
                    darkGradient = listOf(Color(0xFF3A2430), Color(0xFF2C1924)),
                    lightBlob = Color(0xFFFFA8C3),
                    darkBlob = Color(0xFF522E42),
                    titleLight = Color(0xFF4B2332),
                    titleDark = Color(0xFFFFD4E2),
                    onClick = onSeriesClick,
                    isDark = isDark,
                    modifier = Modifier.weight(1f)
                )
                WelcomeCard(
                    title = "My\nFavorites",
                    shape = GeometricBadgeShape.HEXAGON,
                    lightGradient = listOf(Color(0xFFFFD8D0), Color(0xFFFFC2B6)),
                    darkGradient = listOf(Color(0xFF382725), Color(0xFF2B1B19)),
                    lightBlob = Color(0xFFFFB0A1),
                    darkBlob = Color(0xFF503330),
                    titleLight = Color(0xFF4B2721),
                    titleDark = Color(0xFFFFD7D0),
                    onClick = onFavoritesClick,
                    isDark = isDark,
                    modifier = Modifier.weight(1f)
                )
                WelcomeCard(
                    title = "Offline\nDownloads",
                    shape = GeometricBadgeShape.STAR,
                    lightGradient = listOf(Color(0xFFFFF2D4), Color(0xFFFFE5BC)),
                    darkGradient = listOf(Color(0xFF383222), Color(0xFF2A2416)),
                    lightBlob = Color(0xFFFFD99E),
                    darkBlob = Color(0xFF4F452C),
                    titleLight = Color(0xFF4B3B1C),
                    titleDark = Color(0xFFFFF0D2),
                    onClick = onDownloadsClick,
                    isDark = isDark,
                    modifier = Modifier.weight(1f)
                )
            }
        } else {
            // Portrait Phone: 2x2 Grid (2 rows of 2 cards)
            Column(
                modifier = Modifier.fillMaxWidth(),
                verticalArrangement = Arrangement.spacedBy(14.dp)
            ) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(14.dp)
                ) {
                    WelcomeCard(
                        title = "Discover\nFeed",
                        shape = GeometricBadgeShape.TRIANGLE,
                        lightGradient = listOf(Color(0xFFE4DCF9), Color(0xFFD6C8F6)),
                        darkGradient = listOf(Color(0xFF2A273A), Color(0xFF201D2E)),
                        lightBlob = Color(0xFFC7B6F0),
                        darkBlob = Color(0xFF3B3554),
                        titleLight = Color(0xFF2D264B),
                        titleDark = Color(0xFFDDD7F5),
                        onClick = onDiscoverClick,
                        isDark = isDark,
                        modifier = Modifier.weight(1f)
                    )
                    WelcomeCard(
                        title = "Popular\nSeries",
                        shape = GeometricBadgeShape.CIRCLE,
                        lightGradient = listOf(Color(0xFFFFD4E2), Color(0xFFFFBCD2)),
                        darkGradient = listOf(Color(0xFF3A2430), Color(0xFF2C1924)),
                        lightBlob = Color(0xFFFFA8C3),
                        darkBlob = Color(0xFF522E42),
                        titleLight = Color(0xFF4B2332),
                        titleDark = Color(0xFFFFD4E2),
                        onClick = onSeriesClick,
                        isDark = isDark,
                        modifier = Modifier.weight(1f)
                    )
                }

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(14.dp)
                ) {
                    WelcomeCard(
                        title = "My\nFavorites",
                        shape = GeometricBadgeShape.HEXAGON,
                        lightGradient = listOf(Color(0xFFFFD8D0), Color(0xFFFFC2B6)),
                        darkGradient = listOf(Color(0xFF382725), Color(0xFF2B1B19)),
                        lightBlob = Color(0xFFFFB0A1),
                        darkBlob = Color(0xFF503330),
                        titleLight = Color(0xFF4B2721),
                        titleDark = Color(0xFFFFD7D0),
                        onClick = onFavoritesClick,
                        isDark = isDark,
                        modifier = Modifier.weight(1f)
                    )
                    WelcomeCard(
                        title = "Offline\nDownloads",
                        shape = GeometricBadgeShape.STAR,
                        lightGradient = listOf(Color(0xFFFFF2D4), Color(0xFFFFE5BC)),
                        darkGradient = listOf(Color(0xFF383222), Color(0xFF2A2416)),
                        lightBlob = Color(0xFFFFD99E),
                        darkBlob = Color(0xFF4F452C),
                        titleLight = Color(0xFF4B3B1C),
                        titleDark = Color(0xFFFFF0D2),
                        onClick = onDownloadsClick,
                        isDark = isDark,
                        modifier = Modifier.weight(1f)
                    )
                }
            }
        }
    }
}

@Composable
private fun SegmentedFilterBar(
    selectedFilter: String,
    onSelectFilter: (String) -> Unit,
    isDark: Boolean,
    modifier: Modifier = Modifier
) {
    val containerBg = if (isDark) Color(0xFF1E2026) else Color(0xFFF2F3F7)

    Surface(
        shape = RoundedCornerShape(50),
        color = containerBg,
        modifier = modifier
            .fillMaxWidth()
            .height(48.dp)
    ) {
        Row(
            modifier = Modifier
                .fillMaxSize()
                .padding(4.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            FilterTabItem(
                label = "✨ All",
                isSelected = selectedFilter == "all",
                isDark = isDark,
                onClick = { onSelectFilter("all") },
                modifier = Modifier.weight(1f)
            )
            FilterTabItem(
                label = "📱 Portrait",
                isSelected = selectedFilter == "portrait",
                isDark = isDark,
                onClick = { onSelectFilter("portrait") },
                modifier = Modifier.weight(1f)
            )
            FilterTabItem(
                label = "💻 Landscape",
                isSelected = selectedFilter == "landscape",
                isDark = isDark,
                onClick = { onSelectFilter("landscape") },
                modifier = Modifier.weight(1f)
            )
        }
    }
}

@Composable
private fun FilterTabItem(
    label: String,
    isSelected: Boolean,
    isDark: Boolean,
    onClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    val activeBg = if (isDark) Color(0xFF2C2D35) else Color.White
    val activeText = if (isDark) Color(0xFFF2F2F5) else Color(0xFF1E1D24)
    val inactiveText = if (isDark) Color(0xFF8A8C98) else Color(0xFF7A7886)

    Surface(
        onClick = onClick,
        shape = RoundedCornerShape(50),
        color = if (isSelected) activeBg else Color.Transparent,
        shadowElevation = if (isSelected) 3.dp else 0.dp,
        modifier = modifier.fillMaxHeight()
    ) {
        Box(
            modifier = Modifier.fillMaxSize(),
            contentAlignment = Alignment.Center
        ) {
            Text(
                text = label,
                style = MaterialTheme.typography.bodyMedium.copy(
                    fontWeight = if (isSelected) FontWeight.Bold else FontWeight.Medium,
                    fontSize = 14.sp
                ),
                color = if (isSelected) activeText else inactiveText
            )
        }
    }
}

@Composable
private fun WelcomeCard(
    title: String,
    shape: GeometricBadgeShape,
    lightGradient: List<Color>,
    darkGradient: List<Color>,
    lightBlob: Color,
    darkBlob: Color,
    titleLight: Color,
    titleDark: Color,
    onClick: () -> Unit,
    isDark: Boolean,
    modifier: Modifier = Modifier
) {
    val gradient = if (isDark) darkGradient else lightGradient
    val blobColor = if (isDark) darkBlob.copy(alpha = 0.45f) else lightBlob.copy(alpha = 0.45f)
    val titleColor = if (isDark) titleDark else titleLight
    val badgeBg = if (isDark) Color.White.copy(alpha = 0.08f) else Color.White.copy(alpha = 0.45f)
    val iconColor = titleColor

    Card(
        onClick = onClick,
        shape = RoundedCornerShape(26.dp),
        colors = CardDefaults.cardColors(containerColor = Color.Transparent),
        modifier = modifier
            .height(138.dp)
            .shadow(
                elevation = if (isDark) 0.dp else 2.dp,
                shape = RoundedCornerShape(26.dp),
                spotColor = Color.Black.copy(alpha = 0.06f)
            )
    ) {
        Box(
            modifier = Modifier
                .fillMaxSize()
                .background(Brush.linearGradient(gradient))
        ) {
            // Organic abstract background wave blob (matching reference artwork)
            Canvas(modifier = Modifier.fillMaxSize()) {
                val path = Path().apply {
                    val w = size.width
                    val h = size.height
                    moveTo(w * 0.40f, h)
                    cubicTo(
                        w * 0.45f, h * 0.50f,
                        w * 0.70f, h * 0.35f,
                        w, h * 0.55f
                    )
                    lineTo(w, h)
                    close()
                }
                drawPath(path, color = blobColor)
            }

            // Card Title (Top Left)
            Text(
                text = title,
                style = MaterialTheme.typography.titleMedium.copy(
                    fontWeight = FontWeight.Bold,
                    fontSize = 16.sp,
                    lineHeight = 21.sp,
                    letterSpacing = (-0.2).sp
                ),
                color = titleColor,
                modifier = Modifier
                    .align(Alignment.TopStart)
                    .padding(18.dp)
            )

            // Geometric Shape Badge (Bottom Right)
            Surface(
                shape = RoundedCornerShape(10.dp),
                color = badgeBg,
                border = BorderStroke(
                    1.dp,
                    if (isDark) Color.White.copy(alpha = 0.15f) else Color.Black.copy(alpha = 0.08f)
                ),
                modifier = Modifier
                    .align(Alignment.BottomEnd)
                    .padding(14.dp)
                    .size(34.dp)
            ) {
                Box(
                    modifier = Modifier.fillMaxSize(),
                    contentAlignment = Alignment.Center
                ) {
                    GeometricShapeIcon(
                        shape = shape,
                        color = iconColor,
                        modifier = Modifier.size(16.dp)
                    )
                }
            }
        }
    }
}

@Composable
private fun GeometricShapeIcon(
    shape: GeometricBadgeShape,
    color: Color,
    modifier: Modifier = Modifier
) {
    Canvas(modifier = modifier) {
        val w = size.width
        val h = size.height
        val strokeWidth = 1.8.dp.toPx()

        when (shape) {
            GeometricBadgeShape.TRIANGLE -> {
                val path = Path().apply {
                    moveTo(w / 2f, 1f)
                    lineTo(w - 1f, h - 1.5f)
                    lineTo(1f, h - 1.5f)
                    close()
                }
                drawPath(
                    path = path,
                    color = color,
                    style = Stroke(width = strokeWidth, cap = StrokeCap.Round, join = StrokeJoin.Round)
                )
            }
            GeometricBadgeShape.CIRCLE -> {
                drawCircle(
                    color = color,
                    radius = (w / 2f) - 1.5f,
                    style = Stroke(width = strokeWidth)
                )
            }
            GeometricBadgeShape.HEXAGON -> {
                val cx = w / 2f
                val cy = h / 2f
                val r = (w / 2f) - 1.5f
                val path = Path().apply {
                    for (i in 0 until 6) {
                        val angle = (i * 60 - 30) * PI / 180f
                        val x = cx + r * cos(angle).toFloat()
                        val y = cy + r * sin(angle).toFloat()
                        if (i == 0) moveTo(x, y) else lineTo(x, y)
                    }
                    close()
                }
                drawPath(
                    path = path,
                    color = color,
                    style = Stroke(width = strokeWidth, cap = StrokeCap.Round, join = StrokeJoin.Round)
                )
            }
            GeometricBadgeShape.STAR -> {
                val cx = w / 2f
                val cy = h / 2f
                val outerR = (w / 2f) - 1f
                val innerR = outerR * 0.48f
                val path = Path().apply {
                    for (i in 0 until 10) {
                        val angle = (i * 36 - 90) * PI / 180f
                        val r = if (i % 2 == 0) outerR else innerR
                        val x = cx + r * cos(angle).toFloat()
                        val y = cy + r * sin(angle).toFloat()
                        if (i == 0) moveTo(x, y) else lineTo(x, y)
                    }
                    close()
                }
                drawPath(
                    path = path,
                    color = color,
                    style = Stroke(width = strokeWidth, cap = StrokeCap.Round, join = StrokeJoin.Round)
                )
            }
        }
    }
}
