package com.wallsafe.feature.settings

import android.content.Context
import android.net.Uri
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.wallsafe.core.common.AppDisguise
import com.wallsafe.core.common.AppDisguiseManager
import com.wallsafe.core.data.repository.SettingsRepository
import com.wallsafe.core.model.ContentRatingMode
import com.wallsafe.core.panic.LocationPanicHelper
import com.wallsafe.core.panic.PanicManager
import com.wallsafe.core.panic.PanicTriggerHandler
import com.wallsafe.core.panic.WifiPanicMonitor
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import com.wallsafe.core.data.repository.UpdateRepository
import com.wallsafe.core.model.AppUpdateInfo
import java.io.File
import javax.inject.Inject

@HiltViewModel
class SettingsViewModel @Inject constructor(
    private val settingsRepository: SettingsRepository,
    private val updateRepository: UpdateRepository,
    private val panicTriggerHandler: PanicTriggerHandler,
    private val panicManager: PanicManager,
    private val wifiPanicMonitor: WifiPanicMonitor,
    private val locationPanicHelper: LocationPanicHelper,
    private val appDisguiseManager: AppDisguiseManager,
    @ApplicationContext private val context: Context
) : ViewModel() {

    fun getCurrentSsid(): String? = wifiPanicMonitor.getCurrentSsid()
    fun getCurrentLocation(): Pair<Double, Double>? = locationPanicHelper.getLastKnownLocation()
    fun hasLocationPermission(): Boolean = locationPanicHelper.hasLocationPermission()

    private val _uiState = MutableStateFlow(SettingsUiState())
    val uiState: StateFlow<SettingsUiState> = _uiState.asStateFlow()

    init {
        val version = try {
            context.packageManager.getPackageInfo(context.packageName, 0).versionName ?: "1.0.7"
        } catch (e: Exception) {
            "1.0.7"
        }
        _uiState.update { it.copy(currentAppVersion = version) }
        observeSettings()
    }

    private fun observeSettings() {
        viewModelScope.launch {
            settingsRepository.isDiscretionBlurEnabled.collect { enabled ->
                _uiState.update { it.copy(isDiscretionBlur = enabled) }
            }
        }
        viewModelScope.launch {
            settingsRepository.themeMode.collect { mode ->
                _uiState.update { it.copy(themeMode = mode) }
            }
        }
        viewModelScope.launch {
            settingsRepository.isDynamicColorEnabled.collect { dynamic ->
                _uiState.update { it.copy(isDynamicColor = dynamic) }
            }
        }
        viewModelScope.launch {
            settingsRepository.ratingMode.collect { mode ->
                _uiState.update { it.copy(ratingMode = mode.name) }
            }
        }
        viewModelScope.launch {
            settingsRepository.cardDensity.collect { density ->
                _uiState.update { it.copy(cardDensity = density) }
            }
        }
        viewModelScope.launch {
            settingsRepository.showHomeHero.collect { show ->
                _uiState.update { it.copy(showHomeHero = show) }
            }
        }
        viewModelScope.launch {
            settingsRepository.showHomeTrending.collect { show ->
                _uiState.update { it.copy(showHomeTrending = show) }
            }
        }
        viewModelScope.launch {
            settingsRepository.showHomeFranchises.collect { show ->
                _uiState.update { it.copy(showHomeFranchises = show) }
            }
        }
        viewModelScope.launch {
            settingsRepository.userName.collect { name ->
                _uiState.update { it.copy(userName = name) }
            }
        }
        viewModelScope.launch {
            settingsRepository.isGreetingNameEnabled.collect { enabled ->
                _uiState.update { it.copy(isGreetingNameEnabled = enabled) }
            }
        }

        // Panic Mode Observation
        viewModelScope.launch {
            settingsRepository.isPanicModeEnabled.collect { enabled ->
                _uiState.update { it.copy(isPanicModeEnabled = enabled) }
            }
        }
        viewModelScope.launch {
            settingsRepository.panicTriggerType.collect { type ->
                _uiState.update { it.copy(panicTriggerType = type) }
            }
        }
        viewModelScope.launch {
            settingsRepository.panicSafeSsid.collect { ssid ->
                _uiState.update { it.copy(panicSafeSsid = ssid) }
            }
        }
        viewModelScope.launch {
            settingsRepository.panicWifiSsids.collect { ssids ->
                _uiState.update { it.copy(panicWifiSsids = ssids) }
            }
        }
        viewModelScope.launch {
            settingsRepository.panicWifiMode.collect { mode ->
                _uiState.update { it.copy(panicWifiMode = mode) }
            }
        }
        viewModelScope.launch {
            settingsRepository.isPanicActive.collect { active ->
                _uiState.update { it.copy(isPanicActive = active) }
            }
        }
        viewModelScope.launch {
            settingsRepository.normalWallpaperUri.collect { uri ->
                _uiState.update { it.copy(normalWallpaperUri = uri) }
            }
        }
        viewModelScope.launch {
            settingsRepository.panicSafeLat.collect { lat ->
                _uiState.update { it.copy(panicSafeLat = lat) }
            }
        }
        viewModelScope.launch {
            settingsRepository.panicSafeLng.collect { lng ->
                _uiState.update { it.copy(panicSafeLng = lng) }
            }
        }
        viewModelScope.launch {
            settingsRepository.panicSafeRadiusMeters.collect { radius ->
                _uiState.update { it.copy(panicSafeRadiusMeters = radius) }
            }
        }
        viewModelScope.launch {
            settingsRepository.panicWallpaperUri.collect { uri ->
                _uiState.update { it.copy(panicWallpaperUri = uri) }
            }
        }
        viewModelScope.launch {
            settingsRepository.isPanicAutoRestore.collect { autoRestore ->
                _uiState.update { it.copy(isPanicAutoRestore = autoRestore) }
            }
        }
        viewModelScope.launch {
            settingsRepository.isAutoUpdateCheckEnabled.collect { enabled ->
                _uiState.update { it.copy(isAutoUpdateEnabled = enabled) }
            }
        }
        viewModelScope.launch {
            settingsRepository.tagBlacklist.collect { bl ->
                val list = bl.split(",").map { it.trim() }.filter { it.isNotBlank() }
                _uiState.update { it.copy(tagBlacklist = list) }
            }
        }
        viewModelScope.launch {
            settingsRepository.appDisguise.collect { disguise ->
                _uiState.update { it.copy(appDisguise = disguise) }
            }
        }
        viewModelScope.launch {
            settingsRepository.useSystemWallpaperCropper.collect { enabled ->
                _uiState.update { it.copy(useSystemWallpaperCropper = enabled) }
            }
        }
        viewModelScope.launch {
            settingsRepository.customSourcesJson.collect { json ->
                _uiState.update { it.copy(customSourcesJson = json) }
            }
        }
    }

    fun handleIntent(intent: SettingsIntent) {
        when (intent) {
            is SettingsIntent.SetUserName -> {
                viewModelScope.launch { settingsRepository.setUserName(intent.name) }
            }
            is SettingsIntent.ToggleGreetingName -> {
                viewModelScope.launch { settingsRepository.setIsGreetingNameEnabled(intent.enabled) }
            }
            is SettingsIntent.SetThemeMode -> {
                viewModelScope.launch { settingsRepository.setThemeMode(intent.mode) }
            }
            is SettingsIntent.ToggleDynamicColor -> {
                val next = !_uiState.value.isDynamicColor
                viewModelScope.launch { settingsRepository.setDynamicColorEnabled(next) }
            }
            is SettingsIntent.SetCardDensity -> {
                viewModelScope.launch { settingsRepository.setCardDensity(intent.density) }
            }
            is SettingsIntent.SetRatingMode -> {
                val mode = try { ContentRatingMode.valueOf(intent.mode) } catch (e: Exception) { ContentRatingMode.SfwOnly }
                viewModelScope.launch { settingsRepository.setRatingMode(mode) }
            }
            is SettingsIntent.ToggleDiscretionBlur -> {
                val next = !_uiState.value.isDiscretionBlur
                viewModelScope.launch { settingsRepository.setDiscretionBlurEnabled(next) }
            }
            is SettingsIntent.UpdateTagBlacklist -> {
                viewModelScope.launch { settingsRepository.setTagBlacklist(intent.tags.joinToString(",")) }
            }
            is SettingsIntent.ClearCache -> {
                _uiState.update { it.copy(cacheSizeBytes = 0L) }
            }
            is SettingsIntent.ToggleHomeHero -> {
                val next = !_uiState.value.showHomeHero
                viewModelScope.launch { settingsRepository.setShowHomeHero(next) }
            }
            is SettingsIntent.ToggleHomeTrending -> {
                val next = !_uiState.value.showHomeTrending
                viewModelScope.launch { settingsRepository.setShowHomeTrending(next) }
            }
            is SettingsIntent.ToggleHomeFranchises -> {
                val next = !_uiState.value.showHomeFranchises
                viewModelScope.launch { settingsRepository.setShowHomeFranchises(next) }
            }
            is SettingsIntent.SetPanicModeEnabled -> {
                viewModelScope.launch { settingsRepository.setPanicModeEnabled(intent.enabled) }
            }
            is SettingsIntent.SetPanicTriggerType -> {
                viewModelScope.launch { settingsRepository.setPanicTriggerType(intent.type) }
            }
            is SettingsIntent.SetPanicSafeSsid -> {
                viewModelScope.launch {
                    settingsRepository.setPanicSafeSsid(intent.ssid)
                    if (intent.ssid.isNotBlank()) {
                        settingsRepository.addPanicWifiSsid(intent.ssid)
                    }
                }
            }
            is SettingsIntent.AddPanicWifiSsid -> {
                viewModelScope.launch { settingsRepository.addPanicWifiSsid(intent.ssid) }
            }
            is SettingsIntent.RemovePanicWifiSsid -> {
                viewModelScope.launch { settingsRepository.removePanicWifiSsid(intent.ssid) }
            }
            is SettingsIntent.SetPanicWifiMode -> {
                viewModelScope.launch { settingsRepository.setPanicWifiMode(intent.mode) }
            }
            is SettingsIntent.SetPanicSafeLocation -> {
                viewModelScope.launch { settingsRepository.setPanicSafeLocation(intent.lat, intent.lng, intent.radius) }
            }
            is SettingsIntent.SetPanicWallpaperUri -> {
                viewModelScope.launch {
                    val uri = intent.uri
                    if (uri.isNotBlank()) {
                        val parsed = try { Uri.parse(uri) } catch (e: Exception) { null }
                        if (parsed != null && (parsed.scheme == "content" || parsed.scheme == "file")) {
                            val saved = panicManager.saveCustomPanicWallpaper(context, parsed)
                            settingsRepository.setPanicWallpaperUri(saved)
                        } else {
                            settingsRepository.setPanicWallpaperUri(uri)
                        }
                    } else {
                        settingsRepository.setPanicWallpaperUri("")
                    }
                }
            }
            is SettingsIntent.SetNormalWallpaperUri -> {
                viewModelScope.launch {
                    val uri = intent.uri
                    if (uri.isNotBlank()) {
                        val parsed = try { Uri.parse(uri) } catch (e: Exception) { null }
                        if (parsed != null && (parsed.scheme == "content" || parsed.scheme == "file")) {
                            val saved = panicManager.saveCustomNormalWallpaper(context, parsed)
                            settingsRepository.setNormalWallpaperUri(saved)
                        } else {
                            settingsRepository.setNormalWallpaperUri(uri)
                        }
                    } else {
                        settingsRepository.setNormalWallpaperUri("")
                    }
                }
            }
            is SettingsIntent.CaptureCurrentAsNormalWallpaper -> {
                viewModelScope.launch {
                    panicManager.captureCurrentAsNormalWallpaper(context)
                }
            }
            is SettingsIntent.SetPanicAutoRestore -> {
                viewModelScope.launch { settingsRepository.setPanicAutoRestore(intent.autoRestore) }
            }
            is SettingsIntent.TestPanicMode -> {
                viewModelScope.launch {
                    panicTriggerHandler.testPanic(context)
                }
            }
            is SettingsIntent.RestorePanicMode -> {
                viewModelScope.launch {
                    panicTriggerHandler.restorePanic(context)
                }
            }
            is SettingsIntent.ToggleAutoUpdate -> {
                viewModelScope.launch { settingsRepository.setAutoUpdateCheckEnabled(intent.enabled) }
            }
            is SettingsIntent.CheckForUpdates -> {
                checkForUpdates(isUserInitiated = true)
            }
            is SettingsIntent.StartUpdateDownload -> {
                val updateInfo = _uiState.value.updateInfo ?: return
                updateRepository.openDownloadInBrowser(updateInfo)
                _uiState.update { it.copy(showUpdateDialog = false) }
            }
            is SettingsIntent.InstallUpdate -> {
                val updateInfo = _uiState.value.updateInfo ?: return
                updateRepository.openDownloadInBrowser(updateInfo)
                _uiState.update { it.copy(showUpdateDialog = false) }
            }
            is SettingsIntent.OpenInstallPermissionSettings -> {
                val updateInfo = _uiState.value.updateInfo
                if (updateInfo != null) {
                    updateRepository.openDownloadInBrowser(updateInfo)
                }
                _uiState.update { it.copy(showUpdateDialog = false) }
            }
            is SettingsIntent.DismissUpdateDialog -> {
                _uiState.update { it.copy(showUpdateDialog = false) }
            }
            is SettingsIntent.ClearUpdateMessage -> {
                _uiState.update { it.copy(updateCheckMessage = null) }
            }
            is SettingsIntent.SetAppDisguise -> {
                viewModelScope.launch {
                    settingsRepository.setAppDisguise(intent.disguise)
                    val enumVal = AppDisguise.fromKey(intent.disguise)
                    appDisguiseManager.setDisguise(enumVal)
                }
            }
            is SettingsIntent.ToggleSystemWallpaperCropper -> {
                viewModelScope.launch { settingsRepository.setUseSystemWallpaperCropper(intent.enabled) }
            }
            is SettingsIntent.AddCustomSource -> {
                viewModelScope.launch {
                    val currentJson = _uiState.value.customSourcesJson
                    val arr = try { org.json.JSONArray(currentJson) } catch (e: Exception) { org.json.JSONArray() }
                    val newObj = org.json.JSONObject().apply {
                        put("id", intent.url.trim())
                        put("name", intent.name.trim())
                        put("url", intent.url.trim())
                    }
                    arr.put(newObj)
                    settingsRepository.setCustomSourcesJson(arr.toString())
                }
            }
            is SettingsIntent.RemoveCustomSource -> {
                viewModelScope.launch {
                    val currentJson = _uiState.value.customSourcesJson
                    val arr = try { org.json.JSONArray(currentJson) } catch (e: Exception) { org.json.JSONArray() }
                    val newArr = org.json.JSONArray()
                    for (i in 0 until arr.length()) {
                        val obj = arr.getJSONObject(i)
                        if (obj.optString("id") != intent.id) {
                            newArr.put(obj)
                        }
                    }
                    settingsRepository.setCustomSourcesJson(newArr.toString())
                }
            }
            is SettingsIntent.AddBlacklistTag -> {
                viewModelScope.launch { settingsRepository.addTagToBlacklist(intent.tag) }
            }
            is SettingsIntent.RemoveBlacklistTag -> {
                viewModelScope.launch { settingsRepository.removeTagFromBlacklist(intent.tag) }
            }
        }
    }

    fun checkForUpdates(isUserInitiated: Boolean) {
        viewModelScope.launch {
            _uiState.update { it.copy(isCheckingForUpdate = true, updateCheckMessage = null) }
            val currentVersion = _uiState.value.currentAppVersion
            val result = updateRepository.checkForUpdate(currentVersion)
            result.onSuccess { info ->
                val cached = updateRepository.getCachedApk(info)
                _uiState.update {
                    it.copy(
                        isCheckingForUpdate = false,
                        updateInfo = info,
                        showUpdateDialog = info.isUpdateAvailable,
                        isUpdateDownloaded = cached != null,
                        needsInstallPermission = !updateRepository.canRequestPackageInstalls(),
                        updateCheckMessage = if (info.isUpdateAvailable) {
                            "Update available: ${info.versionName}"
                        } else if (isUserInitiated) {
                            "WallSafe is up to date (v$currentVersion)"
                        } else null
                    )
                }
            }.onFailure { error ->
                _uiState.update {
                    it.copy(
                        isCheckingForUpdate = false,
                        updateCheckMessage = if (isUserInitiated) {
                            "Could not check for updates: ${error.localizedMessage ?: "Unknown error"}"
                        } else null
                    )
                }
            }
        }
    }

    private fun startDownload(updateInfo: AppUpdateInfo) {
        viewModelScope.launch {
            _uiState.update {
                it.copy(
                    isDownloadingUpdate = true,
                    updateDownloadProgress = 0f,
                    updateDownloadedBytes = 0L,
                    updateDownloadTotalBytes = updateInfo.apkSize,
                    updateErrorMessage = null
                )
            }

            val result = updateRepository.downloadApk(updateInfo) { progress, downloaded, total ->
                _uiState.update {
                    it.copy(
                        updateDownloadProgress = progress,
                        updateDownloadedBytes = downloaded,
                        updateDownloadTotalBytes = total
                    )
                }
            }

            result.onSuccess { file ->
                val needsPerm = !updateRepository.canRequestPackageInstalls()
                _uiState.update {
                    it.copy(
                        isDownloadingUpdate = false,
                        isUpdateDownloaded = true,
                        needsInstallPermission = needsPerm
                    )
                }
                if (!needsPerm) {
                    updateRepository.installApk(file)
                }
            }.onFailure { error ->
                _uiState.update {
                    it.copy(
                        isDownloadingUpdate = false,
                        updateErrorMessage = "Download failed: ${error.localizedMessage ?: "Unknown error"}"
                    )
                }
            }
        }
    }
}
