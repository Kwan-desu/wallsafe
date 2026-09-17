package com.wallsafe.feature.settings

import android.Manifest
import android.content.Intent
import android.net.Uri
import android.widget.Toast
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalClipboardManager
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import coil3.compose.AsyncImage
import com.wallsafe.feature.settings.components.SettingsListItem
import com.wallsafe.feature.settings.components.SettingsSection
import java.util.Locale

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun PanicModeConfigScreen(
    uiState: SettingsUiState,
    onIntent: (SettingsIntent) -> Unit,
    onGetCurrentSsid: () -> String?,
    onGetCurrentLocation: () -> Pair<Double, Double>?,
    hasLocationPermission: () -> Boolean,
    onBack: () -> Unit
) {
    val context = LocalContext.current
    val scrollState = rememberScrollState()

    var newSsidInput by remember { mutableStateOf("") }
    var showManualLocationDialog by remember { mutableStateOf(false) }

    val locationPermissionLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.RequestMultiplePermissions()
    ) { permissions ->
        val fineGranted = permissions[Manifest.permission.ACCESS_FINE_LOCATION] ?: false
        val coarseGranted = permissions[Manifest.permission.ACCESS_COARSE_LOCATION] ?: false
        if (fineGranted || coarseGranted) {
            Toast.makeText(context, "Location permission granted", Toast.LENGTH_SHORT).show()
        } else {
            Toast.makeText(context, "Location permission is required for SSID/GPS detection", Toast.LENGTH_LONG).show()
        }
    }

    val panicImagePickerLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.GetContent()
    ) { uri ->
        if (uri != null) {
            onIntent(SettingsIntent.SetPanicWallpaperUri(uri.toString()))
            Toast.makeText(context, "Custom SFW panic wallpaper saved", Toast.LENGTH_SHORT).show()
        }
    }

    val normalImagePickerLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.GetContent()
    ) { uri ->
        if (uri != null) {
            onIntent(SettingsIntent.SetNormalWallpaperUri(uri.toString()))
            Toast.makeText(context, "Normal restoration wallpaper saved", Toast.LENGTH_SHORT).show()
        }
    }

    if (showManualLocationDialog) {
        ManualLocationDialog(
            currentLat = uiState.panicSafeLat,
            currentLng = uiState.panicSafeLng,
            radius = uiState.panicSafeRadiusMeters,
            onSave = { lat, lng ->
                onIntent(SettingsIntent.SetPanicSafeLocation(lat, lng, uiState.panicSafeRadiusMeters))
                showManualLocationDialog = false
                Toast.makeText(context, "Safe location updated manually", Toast.LENGTH_SHORT).show()
            },
            onDismiss = { showManualLocationDialog = false }
        )
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Panic Mode") },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(
                            imageVector = Icons.AutoMirrored.Filled.ArrowBack,
                            contentDescription = "Back"
                        )
                    }
                }
            )
        }
    ) { paddingValues ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
                .verticalScroll(scrollState)
                .padding(horizontal = 20.dp, vertical = 12.dp),
            verticalArrangement = Arrangement.spacedBy(18.dp)
        ) {
            // Overview Info Card
            Card(
                shape = RoundedCornerShape(16.dp),
                colors = CardDefaults.cardColors(
                    containerColor = MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.5f)
                ),
                modifier = Modifier.fillMaxWidth()
            ) {
                Row(
                    modifier = Modifier.padding(16.dp),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(14.dp)
                ) {
                    Icon(
                        imageVector = Icons.Default.Security,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.primary,
                        modifier = Modifier.size(32.dp)
                    )
                    Column {
                        Text(
                            text = "Automatic Privacy Shield",
                            style = MaterialTheme.typography.titleMedium,
                            fontWeight = FontWeight.Bold
                        )
                        Spacer(modifier = Modifier.height(2.dp))
                        Text(
                            text = "Automatically switches your wallpaper to a discreet, safe SFW wallpaper when leaving your trusted Wi-Fi or location, and restores your old wallpaper when returning.",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }
            }

            // Live State Status Banner
            if (uiState.isPanicModeEnabled) {
                Card(
                    shape = RoundedCornerShape(16.dp),
                    colors = CardDefaults.cardColors(
                        containerColor = if (uiState.isPanicActive) {
                            MaterialTheme.colorScheme.errorContainer.copy(alpha = 0.6f)
                        } else {
                            MaterialTheme.colorScheme.secondaryContainer.copy(alpha = 0.5f)
                        }
                    ),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(14.dp),
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.SpaceBetween
                    ) {
                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.spacedBy(10.dp),
                            modifier = Modifier.weight(1f)
                        ) {
                            Icon(
                                imageVector = if (uiState.isPanicActive) Icons.Default.Warning else Icons.Default.CheckCircle,
                                contentDescription = null,
                                tint = if (uiState.isPanicActive) MaterialTheme.colorScheme.error else MaterialTheme.colorScheme.secondary
                            )
                            Column {
                                Text(
                                    text = if (uiState.isPanicActive) "Panic Mode is ACTIVE" else "Normal Wallpaper Active",
                                    fontWeight = FontWeight.Bold,
                                    style = MaterialTheme.typography.titleSmall
                                )
                                Text(
                                    text = if (uiState.isPanicActive) "Discreet SFW wallpaper is applied" else "Monitoring triggers in background",
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                            }
                        }

                        if (uiState.isPanicActive) {
                            Button(
                                onClick = {
                                    onIntent(SettingsIntent.RestorePanicMode)
                                    Toast.makeText(context, "Restoring normal wallpaper...", Toast.LENGTH_SHORT).show()
                                },
                                colors = ButtonDefaults.buttonColors(containerColor = MaterialTheme.colorScheme.primary)
                            ) {
                                Text("Restore")
                            }
                        }
                    }
                }
            }

            // 1. Master Toggle
            Card(
                shape = RoundedCornerShape(16.dp),
                colors = CardDefaults.cardColors(
                    containerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.45f)
                ),
                modifier = Modifier.fillMaxWidth()
            ) {
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(16.dp),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Column(modifier = Modifier.weight(1f)) {
                        Text(
                            text = "Enable Panic Mode",
                            style = MaterialTheme.typography.titleMedium,
                            fontWeight = FontWeight.SemiBold
                        )
                        Text(
                            text = if (uiState.isPanicModeEnabled) "Active and monitoring triggers" else "Disabled",
                            style = MaterialTheme.typography.bodySmall,
                            color = if (uiState.isPanicModeEnabled) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                    Switch(
                        checked = uiState.isPanicModeEnabled,
                        onCheckedChange = { onIntent(SettingsIntent.SetPanicModeEnabled(it)) }
                    )
                }
            }

            if (uiState.isPanicModeEnabled) {
                // 2. Trigger Method Selection
                SettingsSection(title = "Trigger Method") {
                    val triggerOptions = listOf(
                        "wifi" to "Wi-Fi Networks (Multiple SSIDs)",
                        "location" to "Safe Location (GPS Safe Zone)",
                        "wifi_disconnect" to "Cellular Mode (Any Wi-Fi Disconnect)"
                    )

                    triggerOptions.forEach { (type, label) ->
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(vertical = 4.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            RadioButton(
                                selected = uiState.panicTriggerType == type,
                                onClick = { onIntent(SettingsIntent.SetPanicTriggerType(type)) }
                            )
                            Spacer(modifier = Modifier.width(8.dp))
                            Text(
                                text = label,
                                style = MaterialTheme.typography.bodyLarge,
                                fontWeight = if (uiState.panicTriggerType == type) FontWeight.Bold else FontWeight.Normal
                            )
                        }
                    }

                    Spacer(modifier = Modifier.height(10.dp))

                    // Sub-configuration depending on chosen trigger
                    when (uiState.panicTriggerType) {
                        "wifi" -> {
                            Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                                // Whitelist vs Blacklist Mode
                                Text(
                                    text = "Wi-Fi Filtering Mode:",
                                    style = MaterialTheme.typography.titleSmall,
                                    fontWeight = FontWeight.SemiBold
                                )

                                Row(
                                    modifier = Modifier.fillMaxWidth(),
                                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                                ) {
                                    FilterChip(
                                        selected = uiState.panicWifiMode == "whitelist",
                                        onClick = { onIntent(SettingsIntent.SetPanicWifiMode("whitelist")) },
                                        label = { Text("Whitelist (Safe Networks)") },
                                        leadingIcon = if (uiState.panicWifiMode == "whitelist") {
                                            { Icon(Icons.Default.Check, contentDescription = null, modifier = Modifier.size(16.dp)) }
                                        } else null,
                                        modifier = Modifier.weight(1f)
                                    )
                                    FilterChip(
                                        selected = uiState.panicWifiMode == "blacklist",
                                        onClick = { onIntent(SettingsIntent.SetPanicWifiMode("blacklist")) },
                                        label = { Text("Blacklist (Unsafe)") },
                                        leadingIcon = if (uiState.panicWifiMode == "blacklist") {
                                            { Icon(Icons.Default.Check, contentDescription = null, modifier = Modifier.size(16.dp)) }
                                        } else null,
                                        modifier = Modifier.weight(1f)
                                    )
                                }

                                Text(
                                    text = if (uiState.panicWifiMode == "whitelist") {
                                        "🛡️ Whitelist Mode: Normal wallpaper is maintained ONLY while connected to networks in this list. Disconnecting or connecting to an unknown Wi-Fi will trigger Panic Mode."
                                    } else {
                                        "🚫 Blacklist Mode: Panic Mode triggers whenever your device connects to ANY network in this list (e.g., School, Work, Public hot-spots)."
                                    },
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )

                                // Add SSID Input
                                Row(
                                    modifier = Modifier.fillMaxWidth(),
                                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                                    verticalAlignment = Alignment.CenterVertically
                                ) {
                                    OutlinedTextField(
                                        value = newSsidInput,
                                        onValueChange = { newSsidInput = it },
                                        label = { Text("Wi-Fi SSID") },
                                        placeholder = { Text("e.g. Home_5G") },
                                        singleLine = true,
                                        modifier = Modifier.weight(1f)
                                    )

                                    Button(
                                        onClick = {
                                            val trimmed = newSsidInput.trim('"', ' ')
                                            if (trimmed.isNotBlank()) {
                                                onIntent(SettingsIntent.AddPanicWifiSsid(trimmed))
                                                newSsidInput = ""
                                                Toast.makeText(context, "Added $trimmed", Toast.LENGTH_SHORT).show()
                                            }
                                        },
                                        enabled = newSsidInput.isNotBlank()
                                    ) {
                                        Icon(Icons.Default.Add, contentDescription = null)
                                        Spacer(modifier = Modifier.width(4.dp))
                                        Text("Add")
                                    }
                                }

                                // Quick Add Current Network Button
                                FilledTonalButton(
                                    onClick = {
                                        if (!hasLocationPermission()) {
                                            locationPermissionLauncher.launch(
                                                arrayOf(Manifest.permission.ACCESS_FINE_LOCATION, Manifest.permission.ACCESS_COARSE_LOCATION)
                                            )
                                        } else {
                                            val currentSsid = onGetCurrentSsid()
                                            if (!currentSsid.isNullOrBlank()) {
                                                onIntent(SettingsIntent.AddPanicWifiSsid(currentSsid))
                                                Toast.makeText(context, "Added current Wi-Fi: $currentSsid", Toast.LENGTH_SHORT).show()
                                            } else {
                                                Toast.makeText(context, "Could not detect Wi-Fi name. Ensure Wi-Fi & Location are enabled.", Toast.LENGTH_LONG).show()
                                            }
                                        }
                                    },
                                    modifier = Modifier.fillMaxWidth()
                                ) {
                                    Icon(Icons.Default.Wifi, contentDescription = null)
                                    Spacer(modifier = Modifier.width(8.dp))
                                    Text("Add Currently Connected Wi-Fi")
                                }

                                // Display Configured SSIDs
                                val configuredSsids = uiState.panicWifiSsids.toList()
                                if (configuredSsids.isNotEmpty()) {
                                    Text(
                                        text = "Configured Networks (${configuredSsids.size}):",
                                        style = MaterialTheme.typography.titleSmall,
                                        fontWeight = FontWeight.SemiBold
                                    )

                                    Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                                        configuredSsids.forEach { ssid ->
                                            Surface(
                                                shape = RoundedCornerShape(10.dp),
                                                color = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.6f),
                                                modifier = Modifier.fillMaxWidth()
                                            ) {
                                                Row(
                                                    modifier = Modifier
                                                        .fillMaxWidth()
                                                        .padding(horizontal = 12.dp, vertical = 8.dp),
                                                    verticalAlignment = Alignment.CenterVertically,
                                                    horizontalArrangement = Arrangement.SpaceBetween
                                                ) {
                                                    Row(
                                                        verticalAlignment = Alignment.CenterVertically,
                                                        horizontalArrangement = Arrangement.spacedBy(10.dp)
                                                    ) {
                                                        Icon(
                                                            imageVector = Icons.Default.Wifi,
                                                            contentDescription = null,
                                                            tint = MaterialTheme.colorScheme.primary,
                                                            modifier = Modifier.size(20.dp)
                                                        )
                                                        Text(
                                                            text = ssid,
                                                            style = MaterialTheme.typography.bodyMedium,
                                                            fontWeight = FontWeight.Medium
                                                        )
                                                    }

                                                    IconButton(
                                                        onClick = {
                                                            onIntent(SettingsIntent.RemovePanicWifiSsid(ssid))
                                                            Toast.makeText(context, "Removed $ssid", Toast.LENGTH_SHORT).show()
                                                        },
                                                        modifier = Modifier.size(32.dp)
                                                    ) {
                                                        Icon(
                                                            imageVector = Icons.Default.Delete,
                                                            contentDescription = "Remove SSID",
                                                            tint = MaterialTheme.colorScheme.error,
                                                            modifier = Modifier.size(18.dp)
                                                        )
                                                    }
                                                }
                                            }
                                        }
                                    }
                                } else {
                                    Text(
                                        text = "No Wi-Fi networks added yet. Add your home Wi-Fi to keep your wallpaper safe.",
                                        style = MaterialTheme.typography.bodySmall,
                                        color = MaterialTheme.colorScheme.error
                                    )
                                }
                            }
                        }

                        "location" -> {
                            Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                                val hasCoords = uiState.panicSafeLat != 0.0 && uiState.panicSafeLng != 0.0
                                val coordsText = if (hasCoords) {
                                    String.format(Locale.US, "%.5f, %.5f", uiState.panicSafeLat, uiState.panicSafeLng)
                                } else {
                                    "Not configured"
                                }

                                Surface(
                                    shape = RoundedCornerShape(12.dp),
                                    color = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f),
                                    modifier = Modifier.fillMaxWidth()
                                ) {
                                    Row(
                                        modifier = Modifier.padding(14.dp),
                                        verticalAlignment = Alignment.CenterVertically,
                                        horizontalArrangement = Arrangement.spacedBy(12.dp)
                                    ) {
                                        Icon(
                                            imageVector = Icons.Default.LocationOn,
                                            contentDescription = null,
                                            tint = MaterialTheme.colorScheme.primary
                                        )
                                        Column {
                                            Text(
                                                text = "Safe Zone Center",
                                                style = MaterialTheme.typography.labelMedium,
                                                color = MaterialTheme.colorScheme.onSurfaceVariant
                                            )
                                            Text(
                                                text = coordsText,
                                                style = MaterialTheme.typography.titleSmall,
                                                fontWeight = FontWeight.Bold
                                            )
                                        }
                                    }
                                }

                                Row(
                                    modifier = Modifier.fillMaxWidth(),
                                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                                ) {
                                    // Automatic GPS Detect Button
                                    Button(
                                        onClick = {
                                            if (!hasLocationPermission()) {
                                                locationPermissionLauncher.launch(
                                                    arrayOf(Manifest.permission.ACCESS_FINE_LOCATION, Manifest.permission.ACCESS_COARSE_LOCATION)
                                                )
                                            } else {
                                                val coords = onGetCurrentLocation()
                                                if (coords != null) {
                                                    onIntent(SettingsIntent.SetPanicSafeLocation(coords.first, coords.second, uiState.panicSafeRadiusMeters))
                                                    Toast.makeText(context, "Safe location updated to current GPS", Toast.LENGTH_SHORT).show()
                                                } else {
                                                    Toast.makeText(context, "Could not fetch GPS. Please ensure device Location is ON.", Toast.LENGTH_LONG).show()
                                                }
                                            }
                                        },
                                        modifier = Modifier.weight(1f)
                                    ) {
                                        Icon(Icons.Default.MyLocation, contentDescription = null, modifier = Modifier.size(18.dp))
                                        Spacer(modifier = Modifier.width(6.dp))
                                        Text("Auto GPS")
                                    }

                                    // Manual / Google Maps Button
                                    OutlinedButton(
                                        onClick = { showManualLocationDialog = true },
                                        modifier = Modifier.weight(1f)
                                    ) {
                                        Icon(Icons.Default.Map, contentDescription = null, modifier = Modifier.size(18.dp))
                                        Spacer(modifier = Modifier.width(6.dp))
                                        Text("Google Maps")
                                    }
                                }

                                Text(
                                    text = "Safe Radius: ${uiState.panicSafeRadiusMeters.toInt()}m",
                                    style = MaterialTheme.typography.bodyMedium,
                                    fontWeight = FontWeight.Medium
                                )

                                Row(
                                    modifier = Modifier.fillMaxWidth(),
                                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                                ) {
                                    listOf(100f, 250f, 500f, 1000f, 2000f).forEach { radius ->
                                        FilterChip(
                                            selected = uiState.panicSafeRadiusMeters == radius,
                                            onClick = {
                                                onIntent(SettingsIntent.SetPanicSafeLocation(uiState.panicSafeLat, uiState.panicSafeLng, radius))
                                            },
                                            label = { Text("${radius.toInt()}m") }
                                        )
                                    }
                                }
                            }
                        }

                        "wifi_disconnect" -> {
                            Card(
                                shape = RoundedCornerShape(12.dp),
                                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant)
                            ) {
                                Column(modifier = Modifier.padding(14.dp)) {
                                    Text(
                                        text = "Zero Permissions Required",
                                        fontWeight = FontWeight.Bold,
                                        style = MaterialTheme.typography.bodyMedium
                                    )
                                    Spacer(modifier = Modifier.height(4.dp))
                                    Text(
                                        text = "Panic wallpaper is applied whenever your device disconnects from any Wi-Fi network and switches to Mobile Data (stepping outside your house).",
                                        style = MaterialTheme.typography.bodySmall,
                                        color = MaterialTheme.colorScheme.onSurfaceVariant
                                    )
                                }
                            }
                        }
                    }
                }

                // 3. SFW Panic Wallpaper Preview & Selection
                SettingsSection(title = "SFW Panic Wallpaper (Discreet Target)") {
                    val hasCustomPanic = uiState.panicWallpaperUri.isNotBlank()

                    // Live Wallpaper Preview Card
                    Card(
                        shape = RoundedCornerShape(16.dp),
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(180.dp),
                        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant)
                    ) {
                        Box(modifier = Modifier.fillMaxSize()) {
                            if (hasCustomPanic) {
                                AsyncImage(
                                    model = Uri.parse(uiState.panicWallpaperUri),
                                    contentDescription = "Custom SFW Panic Wallpaper",
                                    contentScale = ContentScale.Crop,
                                    modifier = Modifier.fillMaxSize()
                                )
                            } else {
                                // Default Dark Minimalist Gradient preview
                                Box(
                                    modifier = Modifier
                                        .fillMaxSize()
                                        .background(
                                            Brush.linearGradient(
                                                listOf(
                                                    Color(0xFF12141A),
                                                    Color(0xFF1E2430),
                                                    Color(0xFF0E1014)
                                                )
                                            )
                                        )
                                )
                            }

                            // Overlay Badge
                            Surface(
                                shape = RoundedCornerShape(50),
                                color = Color.Black.copy(alpha = 0.7f),
                                contentColor = Color.White,
                                modifier = Modifier
                                    .align(Alignment.BottomStart)
                                    .padding(12.dp)
                            ) {
                                Row(
                                    modifier = Modifier.padding(horizontal = 10.dp, vertical = 4.dp),
                                    verticalAlignment = Alignment.CenterVertically,
                                    horizontalArrangement = Arrangement.spacedBy(6.dp)
                                ) {
                                    Icon(
                                        imageVector = if (hasCustomPanic) Icons.Default.Image else Icons.Default.Brush,
                                        contentDescription = null,
                                        modifier = Modifier.size(14.dp)
                                    )
                                    Text(
                                        text = if (hasCustomPanic) "Custom Gallery Wallpaper" else "Default Sleek Slate Gradient",
                                        style = MaterialTheme.typography.labelSmall,
                                        fontWeight = FontWeight.Medium
                                    )
                                }
                            }
                        }
                    }

                    Spacer(modifier = Modifier.height(8.dp))

                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.spacedBy(10.dp)
                    ) {
                        FilledTonalButton(
                            onClick = { panicImagePickerLauncher.launch("image/*") },
                            modifier = Modifier.weight(1f)
                        ) {
                            Icon(Icons.Default.Image, contentDescription = null)
                            Spacer(modifier = Modifier.width(6.dp))
                            Text("Pick from Gallery")
                        }

                        if (hasCustomPanic) {
                            OutlinedButton(
                                onClick = {
                                    onIntent(SettingsIntent.SetPanicWallpaperUri(""))
                                    Toast.makeText(context, "Reset to default gradient", Toast.LENGTH_SHORT).show()
                                }
                            ) {
                                Text("Reset")
                            }
                        }
                    }
                }

                // 4. Normal Wallpaper (Restore Target)
                SettingsSection(title = "Normal Wallpaper (Restoration Target)") {
                    val hasCustomNormal = uiState.normalWallpaperUri.isNotBlank()

                    Card(
                        shape = RoundedCornerShape(16.dp),
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(180.dp),
                        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant)
                    ) {
                        Box(modifier = Modifier.fillMaxSize()) {
                            if (hasCustomNormal) {
                                AsyncImage(
                                    model = Uri.parse(uiState.normalWallpaperUri),
                                    contentDescription = "Normal Wallpaper Preview",
                                    contentScale = ContentScale.Crop,
                                    modifier = Modifier.fillMaxSize()
                                )
                            } else {
                                Surface(
                                    color = MaterialTheme.colorScheme.surfaceVariant,
                                    modifier = Modifier.fillMaxSize()
                                ) {
                                    Column(
                                        modifier = Modifier
                                            .fillMaxSize()
                                            .padding(16.dp),
                                        verticalArrangement = Arrangement.Center,
                                        horizontalAlignment = Alignment.CenterHorizontally
                                    ) {
                                        Icon(
                                            imageVector = Icons.Default.Wallpaper,
                                            contentDescription = null,
                                            tint = MaterialTheme.colorScheme.primary,
                                            modifier = Modifier.size(36.dp)
                                        )
                                        Spacer(modifier = Modifier.height(6.dp))
                                        Text(
                                            text = "Automatic Pre-Panic Snapshot",
                                            fontWeight = FontWeight.Bold,
                                            style = MaterialTheme.typography.titleSmall
                                        )
                                        Text(
                                            text = "WallSafe will automatically remember and restore the active wallpaper from before panic mode was triggered.",
                                            style = MaterialTheme.typography.bodySmall,
                                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                                            textAlign = androidx.compose.ui.text.style.TextAlign.Center
                                        )
                                    }
                                }
                            }

                            if (hasCustomNormal) {
                                Surface(
                                    shape = RoundedCornerShape(50),
                                    color = Color.Black.copy(alpha = 0.7f),
                                    contentColor = Color.White,
                                    modifier = Modifier
                                        .align(Alignment.BottomStart)
                                        .padding(12.dp)
                                ) {
                                    Row(
                                        modifier = Modifier.padding(horizontal = 10.dp, vertical = 4.dp),
                                        verticalAlignment = Alignment.CenterVertically,
                                        horizontalArrangement = Arrangement.spacedBy(6.dp)
                                    ) {
                                        Icon(Icons.Default.Check, contentDescription = null, modifier = Modifier.size(14.dp))
                                        Text(
                                            text = "Custom Normal Wallpaper",
                                            style = MaterialTheme.typography.labelSmall,
                                            fontWeight = FontWeight.Medium
                                        )
                                    }
                                }
                            }
                        }
                    }

                    Spacer(modifier = Modifier.height(8.dp))

                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.spacedBy(10.dp)
                    ) {
                        FilledTonalButton(
                            onClick = { normalImagePickerLauncher.launch("image/*") },
                            modifier = Modifier.weight(1f)
                        ) {
                            Icon(Icons.Default.PhotoLibrary, contentDescription = null)
                            Spacer(modifier = Modifier.width(6.dp))
                            Text("Set from Gallery")
                        }

                        OutlinedButton(
                            onClick = {
                                onIntent(SettingsIntent.CaptureCurrentAsNormalWallpaper)
                                Toast.makeText(context, "Captured active wallpaper as restore target", Toast.LENGTH_SHORT).show()
                            }
                        ) {
                            Icon(Icons.Default.CameraAlt, contentDescription = null)
                            Spacer(modifier = Modifier.width(4.dp))
                            Text("Capture Screen")
                        }
                    }

                    SettingsListItem(
                        title = "Auto-Restore Wallpaper",
                        subtitle = "Automatically restore normal wallpaper when returning to safe Wi-Fi / location",
                        icon = Icons.Default.Restore,
                        trailingContent = {
                            Switch(
                                checked = uiState.isPanicAutoRestore,
                                onCheckedChange = { onIntent(SettingsIntent.SetPanicAutoRestore(it)) }
                            )
                        }
                    )
                }

                // 5. Test & Manual Trigger
                SettingsSection(title = "Test & Verification") {
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.spacedBy(12.dp)
                    ) {
                        Button(
                            onClick = {
                                onIntent(SettingsIntent.TestPanicMode)
                                Toast.makeText(context, "Panic Mode triggered! SFW wallpaper applied.", Toast.LENGTH_SHORT).show()
                            },
                            modifier = Modifier.weight(1f),
                            colors = ButtonDefaults.buttonColors(containerColor = MaterialTheme.colorScheme.error)
                        ) {
                            Icon(Icons.Default.Warning, contentDescription = null)
                            Spacer(modifier = Modifier.width(8.dp))
                            Text("Test Panic")
                        }

                        OutlinedButton(
                            onClick = {
                                onIntent(SettingsIntent.RestorePanicMode)
                                Toast.makeText(context, "Restoring normal wallpaper...", Toast.LENGTH_SHORT).show()
                            },
                            modifier = Modifier.weight(1f)
                        ) {
                            Icon(Icons.Default.Refresh, contentDescription = null)
                            Spacer(modifier = Modifier.width(8.dp))
                            Text("Restore Now")
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun ManualLocationDialog(
    currentLat: Double,
    currentLng: Double,
    radius: Float,
    onSave: (Double, Double) -> Unit,
    onDismiss: () -> Unit
) {
    val context = LocalContext.current
    val clipboardManager = LocalClipboardManager.current

    var latText by remember { mutableStateOf(if (currentLat != 0.0) currentLat.toString() else "") }
    var lngText by remember { mutableStateOf(if (currentLng != 0.0) currentLng.toString() else "") }
    var pasteInput by remember { mutableStateOf("") }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = {
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                Icon(Icons.Default.Map, contentDescription = null, tint = MaterialTheme.colorScheme.primary)
                Text("Set Location Coordinates")
            }
        },
        text = {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .verticalScroll(rememberScrollState()),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                Text(
                    text = "Enter coordinates directly, open Google Maps to pick a spot, or paste coordinates copied from Google Maps.",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )

                // Open in Google Maps Button
                Button(
                    onClick = {
                        val latVal = latText.toDoubleOrNull() ?: currentLat
                        val lngVal = lngText.toDoubleOrNull() ?: currentLng
                        val gmmIntentUri = if (latVal != 0.0 && lngVal != 0.0) {
                            Uri.parse("geo:$latVal,$lngVal?q=$latVal,$lngVal(Safe+Zone)")
                        } else {
                            Uri.parse("geo:0,0?q=")
                        }
                        val mapIntent = Intent(Intent.ACTION_VIEW, gmmIntentUri)
                        try {
                            context.startActivity(mapIntent)
                        } catch (e: Exception) {
                            val webUri = Uri.parse("https://www.google.com/maps?q=$latVal,$lngVal")
                            context.startActivity(Intent(Intent.ACTION_VIEW, webUri))
                        }
                    },
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Icon(Icons.Default.OpenInNew, contentDescription = null)
                    Spacer(modifier = Modifier.width(8.dp))
                    Text("Open Google Maps")
                }

                // Paste Coordinates Helper
                OutlinedTextField(
                    value = pasteInput,
                    onValueChange = {
                        pasteInput = it
                        // Parse coordinates from text like "37.4219999, -122.0840575" or google maps urls
                        val match = Regex("""(-?\d+\.\d+)[,\s]+(-?\d+\.\d+)""").find(it)
                        if (match != null) {
                            latText = match.groupValues[1]
                            lngText = match.groupValues[2]
                        }
                    },
                    label = { Text("Paste from Google Maps") },
                    placeholder = { Text("e.g. 37.4220, -122.0841") },
                    trailingIcon = {
                        IconButton(
                            onClick = {
                                val clip = clipboardManager.getText()?.text
                                if (!clip.isNullOrBlank()) {
                                    pasteInput = clip
                                    val match = Regex("""(-?\d+\.\d+)[,\s]+(-?\d+\.\d+)""").find(clip)
                                    if (match != null) {
                                        latText = match.groupValues[1]
                                        lngText = match.groupValues[2]
                                    }
                                }
                            }
                        ) {
                            Icon(Icons.Default.ContentPaste, contentDescription = "Paste Clipboard")
                        }
                    },
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth()
                )

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    OutlinedTextField(
                        value = latText,
                        onValueChange = { latText = it },
                        label = { Text("Latitude") },
                        placeholder = { Text("37.4220") },
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal),
                        singleLine = true,
                        modifier = Modifier.weight(1f)
                    )

                    OutlinedTextField(
                        value = lngText,
                        onValueChange = { lngText = it },
                        label = { Text("Longitude") },
                        placeholder = { Text("-122.0841") },
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal),
                        singleLine = true,
                        modifier = Modifier.weight(1f)
                    )
                }
            }
        },
        confirmButton = {
            Button(
                onClick = {
                    val lat = latText.toDoubleOrNull()
                    val lng = lngText.toDoubleOrNull()
                    if (lat != null && lng != null) {
                        onSave(lat, lng)
                    } else {
                        Toast.makeText(context, "Please enter valid numeric coordinates", Toast.LENGTH_SHORT).show()
                    }
                },
                enabled = latText.toDoubleOrNull() != null && lngText.toDoubleOrNull() != null
            ) {
                Text("Save Location")
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) {
                Text("Cancel")
            }
        },
        properties = DialogProperties(usePlatformDefaultWidth = false),
        modifier = Modifier.padding(20.dp)
    )
}
