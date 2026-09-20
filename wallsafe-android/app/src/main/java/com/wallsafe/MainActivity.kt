package com.wallsafe

import android.Manifest
import android.content.pm.PackageManager
import android.os.Build
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContracts
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.windowsizeclass.ExperimentalMaterial3WindowSizeClassApi
import androidx.compose.material3.windowsizeclass.calculateWindowSizeClass
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.core.content.ContextCompat
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.wallsafe.core.data.repository.SettingsRepository
import com.wallsafe.core.data.repository.UpdateRepository
import com.wallsafe.core.model.AppUpdateInfo
import com.wallsafe.core.ui.components.AppUpdateDialog
import com.wallsafe.core.ui.components.FirstLaunchConsentDialog
import com.wallsafe.core.ui.components.LegalDialog
import com.wallsafe.core.ui.components.LegalDocument
import com.wallsafe.core.ui.theme.WallSafeTheme
import dagger.hilt.android.AndroidEntryPoint
import kotlinx.coroutines.launch
import javax.inject.Inject

import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableLongStateOf
import androidx.compose.runtime.saveable.rememberSaveable

@AndroidEntryPoint
class MainActivity : ComponentActivity() {

    @Inject
    lateinit var settingsRepository: SettingsRepository

    @Inject
    lateinit var updateRepository: UpdateRepository

    @OptIn(ExperimentalMaterial3WindowSizeClassApi::class)
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            val windowSizeClass = calculateWindowSizeClass(this)
            val themeMode by settingsRepository.themeMode.collectAsStateWithLifecycle(initialValue = "system")
            val isDynamicColor by settingsRepository.isDynamicColorEnabled.collectAsStateWithLifecycle(initialValue = true)
            val hasAgreedToTerms by settingsRepository.hasAgreedToTerms.collectAsStateWithLifecycle(initialValue = null)
            val isAutoUpdateEnabled by settingsRepository.isAutoUpdateCheckEnabled.collectAsStateWithLifecycle(initialValue = true)
            val scope = rememberCoroutineScope()
            var activeLegalDoc by remember { mutableStateOf<LegalDocument?>(null) }
            var hasRequestedPermissions by rememberSaveable { mutableStateOf(false) }

            var updateInfo by remember { mutableStateOf<AppUpdateInfo?>(null) }
            var showUpdateDialog by remember { mutableStateOf(false) }
            var isDownloadingUpdate by remember { mutableStateOf(false) }
            var updateDownloadProgress by remember { mutableFloatStateOf(0f) }
            var updateDownloadedBytes by remember { mutableLongStateOf(0L) }
            var updateDownloadTotalBytes by remember { mutableLongStateOf(0L) }
            var isUpdateDownloaded by remember { mutableStateOf(false) }
            var needsInstallPermission by remember { mutableStateOf(false) }
            var updateErrorMessage by remember { mutableStateOf<String?>(null) }

            val permissionsLauncher = rememberLauncherForActivityResult(
                contract = ActivityResultContracts.RequestMultiplePermissions()
            ) { /* permission results */ }

            fun getMissingPermissions(): List<String> {
                val list = mutableListOf<String>()

                // Storage / Media Photos
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
                    if (ContextCompat.checkSelfPermission(this@MainActivity, Manifest.permission.READ_MEDIA_IMAGES) != PackageManager.PERMISSION_GRANTED) {
                        list.add(Manifest.permission.READ_MEDIA_IMAGES)
                    }
                    if (ContextCompat.checkSelfPermission(this@MainActivity, Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED) {
                        list.add(Manifest.permission.POST_NOTIFICATIONS)
                    }
                } else {
                    if (ContextCompat.checkSelfPermission(this@MainActivity, Manifest.permission.READ_EXTERNAL_STORAGE) != PackageManager.PERMISSION_GRANTED) {
                        list.add(Manifest.permission.READ_EXTERNAL_STORAGE)
                    }
                    if (Build.VERSION.SDK_INT <= Build.VERSION_CODES.Q &&
                        ContextCompat.checkSelfPermission(this@MainActivity, Manifest.permission.WRITE_EXTERNAL_STORAGE) != PackageManager.PERMISSION_GRANTED
                    ) {
                        list.add(Manifest.permission.WRITE_EXTERNAL_STORAGE)
                    }
                }

                // Location permissions (for Panic Mode Wi-Fi SSID reading & Safe Zone GPS)
                if (ContextCompat.checkSelfPermission(this@MainActivity, Manifest.permission.ACCESS_FINE_LOCATION) != PackageManager.PERMISSION_GRANTED) {
                    list.add(Manifest.permission.ACCESS_FINE_LOCATION)
                }
                if (ContextCompat.checkSelfPermission(this@MainActivity, Manifest.permission.ACCESS_COARSE_LOCATION) != PackageManager.PERMISSION_GRANTED) {
                    list.add(Manifest.permission.ACCESS_COARSE_LOCATION)
                }

                return list
            }

            LaunchedEffect(hasAgreedToTerms) {
                if (hasAgreedToTerms == true && !hasRequestedPermissions) {
                    hasRequestedPermissions = true
                    val missing = getMissingPermissions()
                    if (missing.isNotEmpty()) {
                        permissionsLauncher.launch(missing.toTypedArray())
                    }
                }
            }

            LaunchedEffect(hasAgreedToTerms, isAutoUpdateEnabled) {
                if (hasAgreedToTerms == true && isAutoUpdateEnabled) {
                    val currentVersion = try {
                        packageManager.getPackageInfo(packageName, 0).versionName ?: "1.0.7"
                    } catch (e: Exception) {
                        "1.0.7"
                    }
                    val result = updateRepository.checkForUpdate(currentVersion)
                    result.onSuccess { info ->
                        if (info.isUpdateAvailable) {
                            updateInfo = info
                            val cached = updateRepository.getCachedApk(info)
                            isUpdateDownloaded = cached != null
                            needsInstallPermission = !updateRepository.canRequestPackageInstalls()
                            showUpdateDialog = true
                        }
                    }
                }
            }

            val darkTheme = when (themeMode.lowercase()) {
                "light" -> false
                "dark" -> true
                else -> isSystemInDarkTheme()
            }

            WallSafeTheme(
                darkTheme = darkTheme,
                dynamicColor = isDynamicColor
            ) {
                WallSafeApp(windowSizeClass = windowSizeClass)

                if (showUpdateDialog && updateInfo != null) {
                    AppUpdateDialog(
                        updateInfo = updateInfo!!,
                        isDownloading = isDownloadingUpdate,
                        downloadProgress = updateDownloadProgress,
                        downloadedBytes = updateDownloadedBytes,
                        totalBytes = updateDownloadTotalBytes,
                        isDownloaded = isUpdateDownloaded,
                        needsPermission = needsInstallPermission,
                        errorMessage = updateErrorMessage,
                        onStartDownload = {
                            scope.launch {
                                isDownloadingUpdate = true
                                updateDownloadProgress = 0f
                                updateErrorMessage = null
                                val result = updateRepository.downloadApk(updateInfo!!) { p, d, t ->
                                    updateDownloadProgress = p
                                    updateDownloadedBytes = d
                                    updateDownloadTotalBytes = t
                                }
                                result.onSuccess { file ->
                                    isDownloadingUpdate = false
                                    isUpdateDownloaded = true
                                    needsInstallPermission = !updateRepository.canRequestPackageInstalls()
                                    if (!needsInstallPermission) {
                                        updateRepository.installApk(file)
                                    }
                                }.onFailure { err ->
                                    isDownloadingUpdate = false
                                    updateErrorMessage = "Download failed: ${err.localizedMessage ?: "Network error"}"
                                }
                            }
                        },
                        onInstall = {
                            val cached = updateRepository.getCachedApk(updateInfo!!)
                            if (cached != null) {
                                if (!updateRepository.canRequestPackageInstalls()) {
                                    needsInstallPermission = true
                                } else {
                                    updateRepository.installApk(cached)
                                }
                            }
                        },
                        onGrantPermission = {
                            updateRepository.openInstallPermissionSettings()
                        },
                        onDismiss = {
                            showUpdateDialog = false
                        }
                    )
                }

                if (hasAgreedToTerms == false) {
                    FirstLaunchConsentDialog(
                        onAccept = {
                            scope.launch {
                                settingsRepository.setHasAgreedToTerms(true)
                            }
                            hasRequestedPermissions = true
                            val missing = getMissingPermissions()
                            if (missing.isNotEmpty()) {
                                permissionsLauncher.launch(missing.toTypedArray())
                            }
                        },
                        onViewPrivacy = {
                            activeLegalDoc = LegalDocument.PRIVACY_POLICY
                        },
                        onViewTerms = {
                            activeLegalDoc = LegalDocument.TERMS_OF_SERVICE
                        }
                    )
                }

                activeLegalDoc?.let { doc ->
                    LegalDialog(
                        document = doc,
                        onDismiss = { activeLegalDoc = null }
                    )
                }
            }
        }
    }
}
