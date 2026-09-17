package com.wallsafe.feature.settings

data class SettingsUiState(
    val themeMode: String = "SYSTEM",
    val isDynamicColor: Boolean = true,
    val cardDensity: String = "NORMAL",
    val ratingMode: String = "SFW",
    val isDiscretionBlur: Boolean = false,
    val tagBlacklist: List<String> = emptyList(),
    val cacheSizeBytes: Long = 0L,
    val isLoading: Boolean = false,
    val showHomeHero: Boolean = true,
    val showHomeTrending: Boolean = true,
    val showHomeFranchises: Boolean = true,
    val isPanicModeEnabled: Boolean = false,
    val panicTriggerType: String = "wifi",
    val panicSafeSsid: String = "",
    val panicWifiSsids: Set<String> = emptySet(),
    val panicWifiMode: String = "whitelist",
    val panicSafeLat: Double = 0.0,
    val panicSafeLng: Double = 0.0,
    val panicSafeRadiusMeters: Float = 500f,
    val panicWallpaperUri: String = "",
    val normalWallpaperUri: String = "",
    val isPanicAutoRestore: Boolean = true,
    val isPanicActive: Boolean = false,
    val userName: String = "",
    val isGreetingNameEnabled: Boolean = true
)

sealed interface SettingsIntent {
    data class SetUserName(val name: String) : SettingsIntent
    data class ToggleGreetingName(val enabled: Boolean) : SettingsIntent
    data class SetThemeMode(val mode: String) : SettingsIntent
    data object ToggleDynamicColor : SettingsIntent
    data class SetCardDensity(val density: String) : SettingsIntent
    data class SetRatingMode(val mode: String) : SettingsIntent
    data object ToggleDiscretionBlur : SettingsIntent
    data class UpdateTagBlacklist(val tags: List<String>) : SettingsIntent
    data object ClearCache : SettingsIntent
    data object ToggleHomeHero : SettingsIntent
    data object ToggleHomeTrending : SettingsIntent
    data object ToggleHomeFranchises : SettingsIntent
    data class SetPanicModeEnabled(val enabled: Boolean) : SettingsIntent
    data class SetPanicTriggerType(val type: String) : SettingsIntent
    data class SetPanicSafeSsid(val ssid: String) : SettingsIntent
    data class AddPanicWifiSsid(val ssid: String) : SettingsIntent
    data class RemovePanicWifiSsid(val ssid: String) : SettingsIntent
    data class SetPanicWifiMode(val mode: String) : SettingsIntent
    data class SetPanicSafeLocation(val lat: Double, val lng: Double, val radius: Float) : SettingsIntent
    data class SetPanicWallpaperUri(val uri: String) : SettingsIntent
    data class SetNormalWallpaperUri(val uri: String) : SettingsIntent
    data object CaptureCurrentAsNormalWallpaper : SettingsIntent
    data class SetPanicAutoRestore(val autoRestore: Boolean) : SettingsIntent
    data object TestPanicMode : SettingsIntent
    data object RestorePanicMode : SettingsIntent
}
