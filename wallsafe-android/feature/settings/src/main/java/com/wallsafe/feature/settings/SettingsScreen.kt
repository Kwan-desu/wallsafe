package com.wallsafe.feature.settings

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.selection.selectable
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowForwardIos
import androidx.compose.material.icons.automirrored.filled.TrendingUp
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalUriHandler
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.wallsafe.core.ui.components.AppUpdateDialog
import com.wallsafe.feature.settings.components.SettingsListItem
import com.wallsafe.feature.settings.components.SettingsSection

@Composable
fun SettingsRoute(
    viewModel: SettingsViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()

    SettingsScreen(
        uiState = uiState,
        onIntent = viewModel::handleIntent,
        onGetCurrentSsid = viewModel::getCurrentSsid,
        onGetCurrentLocation = viewModel::getCurrentLocation,
        hasLocationPermission = viewModel::hasLocationPermission
    )
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SettingsScreen(
    uiState: SettingsUiState,
    onIntent: (SettingsIntent) -> Unit,
    onGetCurrentSsid: () -> String? = { null },
    onGetCurrentLocation: () -> Pair<Double, Double>? = { null },
    hasLocationPermission: () -> Boolean = { false }
) {
    var showLegalSubpage by rememberSaveable { mutableStateOf(false) }
    var showPanicSubpage by rememberSaveable { mutableStateOf(false) }
    var showPermissionsDialog by rememberSaveable { mutableStateOf(false) }

    if (showPermissionsDialog) {
        com.wallsafe.feature.settings.components.PermissionsDialog(
            onDismiss = { showPermissionsDialog = false }
        )
    }

    if (showLegalSubpage) {
        LegalAndPrivacyScreen(
            onBack = { showLegalSubpage = false }
        )
        return
    }

    if (showPanicSubpage) {
        PanicModeConfigScreen(
            uiState = uiState,
            onIntent = onIntent,
            onGetCurrentSsid = onGetCurrentSsid,
            onGetCurrentLocation = onGetCurrentLocation,
            hasLocationPermission = hasLocationPermission,
            onBack = { showPanicSubpage = false }
        )
        return
    }

    val scrollState = rememberScrollState()
    val snackbarHostState = remember { SnackbarHostState() }
    val uriHandler = LocalUriHandler.current
    var showThemeDialog by remember { mutableStateOf(false) }
    var showEditNameDialog by remember { mutableStateOf(false) }

    LaunchedEffect(uiState.updateCheckMessage) {
        uiState.updateCheckMessage?.let { msg ->
            snackbarHostState.showSnackbar(msg)
            onIntent(SettingsIntent.ClearUpdateMessage)
        }
    }

    if (uiState.showUpdateDialog && uiState.updateInfo != null) {
        AppUpdateDialog(
            updateInfo = uiState.updateInfo,
            isDownloading = uiState.isDownloadingUpdate,
            downloadProgress = uiState.updateDownloadProgress,
            downloadedBytes = uiState.updateDownloadedBytes,
            totalBytes = uiState.updateDownloadTotalBytes,
            isDownloaded = uiState.isUpdateDownloaded,
            needsPermission = uiState.needsInstallPermission,
            errorMessage = uiState.updateErrorMessage,
            onStartDownload = { onIntent(SettingsIntent.StartUpdateDownload) },
            onInstall = { onIntent(SettingsIntent.InstallUpdate) },
            onGrantPermission = { onIntent(SettingsIntent.OpenInstallPermissionSettings) },
            onDismiss = { onIntent(SettingsIntent.DismissUpdateDialog) }
        )
    }

    if (showEditNameDialog) {
        var inputName by remember { mutableStateOf(uiState.userName) }
        AlertDialog(
            onDismissRequest = { showEditNameDialog = false },
            title = { Text("Set your name") },
            text = {
                OutlinedTextField(
                    value = inputName,
                    onValueChange = { inputName = it },
                    label = { Text("Display Name") },
                    placeholder = { Text("e.g. Alex") },
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth()
                )
            },
            confirmButton = {
                Button(
                    onClick = {
                        onIntent(SettingsIntent.SetUserName(inputName.trim()))
                        showEditNameDialog = false
                    }
                ) {
                    Text("Save")
                }
            },
            dismissButton = {
                TextButton(onClick = { showEditNameDialog = false }) {
                    Text("Cancel")
                }
            }
        )
    }

    if (showThemeDialog) {
        val themeOptions = listOf(
            "system" to "System default",
            "light" to "Light",
            "dark" to "Dark"
        )
        AlertDialog(
            onDismissRequest = { showThemeDialog = false },
            title = { Text("Choose theme") },
            text = {
                Column {
                    themeOptions.forEach { (mode, label) ->
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .selectable(
                                    selected = uiState.themeMode.equals(mode, ignoreCase = true),
                                    onClick = {
                                        onIntent(SettingsIntent.SetThemeMode(mode))
                                        showThemeDialog = false
                                    },
                                    role = Role.RadioButton
                                )
                                .padding(vertical = 12.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            RadioButton(
                                selected = uiState.themeMode.equals(mode, ignoreCase = true),
                                onClick = null
                            )
                            Spacer(modifier = Modifier.width(12.dp))
                            Text(text = label, style = MaterialTheme.typography.bodyLarge)
                        }
                    }
                }
            },
            confirmButton = {
                TextButton(onClick = { showThemeDialog = false }) {
                    Text("Cancel")
                }
            }
        )
    }

    Scaffold(
        topBar = {
            LargeTopAppBar(title = { Text("Settings") })
        },
        snackbarHost = { SnackbarHost(hostState = snackbarHostState) }
    ) { paddingValues ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues),
            contentAlignment = Alignment.TopCenter
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .widthIn(max = 800.dp)
                    .verticalScroll(scrollState)
            ) {
            SettingsSection(title = "Appearance") {
                val currentThemeLabel = when (uiState.themeMode.lowercase()) {
                    "light" -> "Light"
                    "dark" -> "Dark"
                    else -> "System default"
                }
                SettingsListItem(
                    title = "Theme",
                    subtitle = currentThemeLabel,
                    icon = Icons.Default.Brightness4,
                    onClick = { showThemeDialog = true }
                )
                SettingsListItem(
                    title = "Dynamic Color",
                    subtitle = "Use system wallpaper colors",
                    icon = Icons.Default.Palette,
                    trailingContent = {
                        Switch(
                            checked = uiState.isDynamicColor,
                            onCheckedChange = { onIntent(SettingsIntent.ToggleDynamicColor) }
                        )
                    }
                )
            }

            SettingsSection(title = "Home Screen") {
                SettingsListItem(
                    title = "Personalized Greeting",
                    subtitle = if (uiState.isGreetingNameEnabled) "Show custom name on home banner" else "Disabled",
                    icon = Icons.Default.Person,
                    trailingContent = {
                        Switch(
                            checked = uiState.isGreetingNameEnabled,
                            onCheckedChange = { onIntent(SettingsIntent.ToggleGreetingName(it)) }
                        )
                    }
                )
                if (uiState.isGreetingNameEnabled) {
                    SettingsListItem(
                        title = "Your Name",
                        subtitle = if (uiState.userName.isNotBlank()) uiState.userName else "Not set (tap to configure)",
                        icon = Icons.Default.Edit,
                        onClick = { showEditNameDialog = true }
                    )
                }
                SettingsListItem(
                    title = "Discover Banner",
                    subtitle = "Curated hero wallpaper header",
                    icon = Icons.Default.ViewCarousel,
                    trailingContent = {
                        Switch(
                            checked = uiState.showHomeHero,
                            onCheckedChange = { onIntent(SettingsIntent.ToggleHomeHero) }
                        )
                    }
                )
                SettingsListItem(
                    title = "Trending Now",
                    subtitle = "Horizontal trending wallpapers row",
                    icon = Icons.AutoMirrored.Filled.TrendingUp,
                    trailingContent = {
                        Switch(
                            checked = uiState.showHomeTrending,
                            onCheckedChange = { onIntent(SettingsIntent.ToggleHomeTrending) }
                        )
                    }
                )
                SettingsListItem(
                    title = "Popular Franchises",
                    subtitle = "Franchise tags with image previews",
                    icon = Icons.Default.AutoAwesome,
                    trailingContent = {
                        Switch(
                            checked = uiState.showHomeFranchises,
                            onCheckedChange = { onIntent(SettingsIntent.ToggleHomeFranchises) }
                        )
                    }
                )
            }

            SettingsSection(title = "Content & Safety") {
                SettingsListItem(
                    title = "Discretion Blur",
                    subtitle = "Blur suggestive content in feeds",
                    icon = Icons.Default.VisibilityOff,
                    trailingContent = {
                        Switch(
                            checked = uiState.isDiscretionBlur,
                            onCheckedChange = { onIntent(SettingsIntent.ToggleDiscretionBlur) }
                        )
                    }
                )

                val panicSubtitle = if (!uiState.isPanicModeEnabled) {
                    "Disabled"
                } else {
                    when (uiState.panicTriggerType) {
                        "wifi" -> if (uiState.panicSafeSsid.isNotBlank()) "Safe Wi-Fi: ${uiState.panicSafeSsid}" else "Wi-Fi Trigger Active"
                        "location" -> "Location Safe Zone (${uiState.panicSafeRadiusMeters.toInt()}m)"
                        else -> "Cellular Disconnect Trigger"
                    }
                }
                SettingsListItem(
                    title = "Panic Mode",
                    subtitle = panicSubtitle,
                    icon = Icons.Default.Security,
                    trailingContent = {
                        Icon(
                            imageVector = Icons.AutoMirrored.Filled.ArrowForwardIos,
                            contentDescription = "Configure Panic Mode",
                            modifier = Modifier.size(14.dp),
                            tint = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.6f)
                        )
                    },
                    onClick = { showPanicSubpage = true }
                )
            }

            SettingsSection(title = "Storage") {
                SettingsListItem(
                    title = "Clear Cache",
                    subtitle = "${uiState.cacheSizeBytes / (1024 * 1024)} MB used",
                    icon = Icons.Default.Delete,
                    onClick = {
                        onIntent(SettingsIntent.ClearCache)
                    }
                )
            }

            SettingsSection(title = "Permissions") {
                SettingsListItem(
                    title = "App Permissions",
                    subtitle = "Manage Storage, Location & Notifications",
                    icon = Icons.Default.Security,
                    trailingContent = {
                        Icon(
                            imageVector = Icons.AutoMirrored.Filled.ArrowForwardIos,
                            contentDescription = "Manage Permissions",
                            modifier = Modifier.size(14.dp),
                            tint = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.6f)
                        )
                    },
                    onClick = { showPermissionsDialog = true }
                )
            }

            SettingsSection(title = "Legal & Privacy") {
                SettingsListItem(
                    title = "Legal & Privacy Information",
                    subtitle = "Privacy policy, terms of service, EULA, and permissions",
                    icon = Icons.Default.PrivacyTip,
                    trailingContent = {
                        Icon(
                            imageVector = Icons.AutoMirrored.Filled.ArrowForwardIos,
                            contentDescription = "Navigate to Legal & Privacy",
                            modifier = Modifier.size(14.dp),
                            tint = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.6f)
                        )
                    },
                    onClick = { showLegalSubpage = true }
                )
            }

            SettingsSection(title = "App Updates") {
                SettingsListItem(
                    title = "Auto-Check for Updates",
                    subtitle = "Automatically check for new releases when launching",
                    icon = Icons.Default.Sync,
                    trailingContent = {
                        Switch(
                            checked = uiState.isAutoUpdateEnabled,
                            onCheckedChange = { onIntent(SettingsIntent.ToggleAutoUpdate(it)) }
                        )
                    }
                )
                SettingsListItem(
                    title = "Check for Updates",
                    subtitle = if (uiState.isCheckingForUpdate) {
                        "Checking GitHub for new releases..."
                    } else {
                        "Current version: v${uiState.currentAppVersion}"
                    },
                    icon = Icons.Default.SystemUpdate,
                    trailingContent = {
                        if (uiState.isCheckingForUpdate) {
                            CircularProgressIndicator(
                                modifier = Modifier.size(20.dp),
                                strokeWidth = 2.dp
                            )
                        } else {
                            Icon(
                                imageVector = Icons.AutoMirrored.Filled.ArrowForwardIos,
                                contentDescription = null,
                                modifier = Modifier.size(14.dp),
                                tint = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.6f)
                            )
                        }
                    },
                    onClick = {
                        if (!uiState.isCheckingForUpdate) {
                            onIntent(SettingsIntent.CheckForUpdates)
                        }
                    }
                )
            }

            SettingsSection(title = "About") {
                SettingsListItem(
                    title = "App Version",
                    subtitle = "WallSafe v${uiState.currentAppVersion} • Tap to check updates",
                    icon = Icons.Default.Info,
                    onClick = { onIntent(SettingsIntent.CheckForUpdates) }
                )
                SettingsListItem(
                    title = "Source Code",
                    subtitle = "github.com/Kwan-desu/wallsafe",
                    icon = Icons.Default.Code,
                    onClick = { uriHandler.openUri("https://github.com/Kwan-desu/wallsafe") }
                )
            }
        }
    }
}
}
