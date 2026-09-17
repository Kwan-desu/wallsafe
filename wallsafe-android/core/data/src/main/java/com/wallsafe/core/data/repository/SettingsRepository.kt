package com.wallsafe.core.data.repository

import com.wallsafe.core.datastore.WallSafePreferences
import com.wallsafe.core.model.ContentRatingMode
import kotlinx.coroutines.flow.Flow
import javax.inject.Inject
import javax.inject.Singleton

interface SettingsRepository {
    val themeMode: Flow<String>
    val isDynamicColorEnabled: Flow<Boolean>
    val ratingMode: Flow<ContentRatingMode>
    val isDiscretionBlurEnabled: Flow<Boolean>
    val tagBlacklist: Flow<String>
    val cardDensity: Flow<String>
    val showHomeHero: Flow<Boolean>
    val showHomeTrending: Flow<Boolean>
    val showHomeFranchises: Flow<Boolean>
    val hasAgreedToTerms: Flow<Boolean>
    val userName: Flow<String>
    val isGreetingNameEnabled: Flow<Boolean>
    val hasPromptedForName: Flow<Boolean>
    val appliedWallpapersCount: Flow<Int>

    val isPanicModeEnabled: Flow<Boolean>
    val panicTriggerType: Flow<String>
    val panicSafeSsid: Flow<String>
    val panicWifiSsids: Flow<Set<String>>
    val panicWifiMode: Flow<String>
    val isPanicActive: Flow<Boolean>
    val normalWallpaperUri: Flow<String>
    val panicSafeLat: Flow<Double>
    val panicSafeLng: Flow<Double>
    val panicSafeRadiusMeters: Flow<Float>
    val panicWallpaperUri: Flow<String>
    val isPanicAutoRestore: Flow<Boolean>

    suspend fun setThemeMode(mode: String)
    suspend fun setDynamicColorEnabled(enabled: Boolean)
    suspend fun setRatingMode(mode: ContentRatingMode)
    suspend fun setDiscretionBlurEnabled(enabled: Boolean)
    suspend fun setTagBlacklist(tags: String)
    suspend fun setCardDensity(density: String)
    suspend fun setShowHomeHero(show: Boolean)
    suspend fun setShowHomeTrending(show: Boolean)
    suspend fun setShowHomeFranchises(show: Boolean)
    suspend fun setHasAgreedToTerms(agreed: Boolean)
    suspend fun setUserName(name: String)
    suspend fun setIsGreetingNameEnabled(enabled: Boolean)
    suspend fun setHasPromptedForName(prompted: Boolean)
    suspend fun incrementAppliedCount()

    suspend fun setPanicModeEnabled(enabled: Boolean)
    suspend fun setPanicTriggerType(type: String)
    suspend fun setPanicSafeSsid(ssid: String)
    suspend fun setPanicWifiSsids(ssids: Set<String>)
    suspend fun addPanicWifiSsid(ssid: String)
    suspend fun removePanicWifiSsid(ssid: String)
    suspend fun setPanicWifiMode(mode: String)
    suspend fun setIsPanicActive(active: Boolean)
    suspend fun setNormalWallpaperUri(uri: String)
    suspend fun setPanicSafeLocation(lat: Double, lng: Double, radius: Float)
    suspend fun setPanicWallpaperUri(uri: String)
    suspend fun setPanicAutoRestore(autoRestore: Boolean)
}

@Singleton
class SettingsRepositoryImpl @Inject constructor(
    private val preferences: WallSafePreferences
) : SettingsRepository {
    override val themeMode: Flow<String> = preferences.themeMode
    override val isDynamicColorEnabled: Flow<Boolean> = preferences.isDynamicColorEnabled
    override val ratingMode: Flow<ContentRatingMode> = preferences.ratingMode
    override val isDiscretionBlurEnabled: Flow<Boolean> = preferences.isDiscretionBlurEnabled
    override val tagBlacklist: Flow<String> = preferences.tagBlacklist
    override val cardDensity: Flow<String> = preferences.cardDensity
    override val showHomeHero: Flow<Boolean> = preferences.showHomeHero
    override val showHomeTrending: Flow<Boolean> = preferences.showHomeTrending
    override val showHomeFranchises: Flow<Boolean> = preferences.showHomeFranchises
    override val hasAgreedToTerms: Flow<Boolean> = preferences.hasAgreedToTerms
    override val userName: Flow<String> = preferences.userName
    override val isGreetingNameEnabled: Flow<Boolean> = preferences.isGreetingNameEnabled
    override val hasPromptedForName: Flow<Boolean> = preferences.hasPromptedForName
    override val appliedWallpapersCount: Flow<Int> = preferences.appliedWallpapersCount

    override val isPanicModeEnabled: Flow<Boolean> = preferences.isPanicModeEnabled
    override val panicTriggerType: Flow<String> = preferences.panicTriggerType
    override val panicSafeSsid: Flow<String> = preferences.panicSafeSsid
    override val panicWifiSsids: Flow<Set<String>> = preferences.panicWifiSsids
    override val panicWifiMode: Flow<String> = preferences.panicWifiMode
    override val isPanicActive: Flow<Boolean> = preferences.isPanicActive
    override val normalWallpaperUri: Flow<String> = preferences.normalWallpaperUri
    override val panicSafeLat: Flow<Double> = preferences.panicSafeLat
    override val panicSafeLng: Flow<Double> = preferences.panicSafeLng
    override val panicSafeRadiusMeters: Flow<Float> = preferences.panicSafeRadiusMeters
    override val panicWallpaperUri: Flow<String> = preferences.panicWallpaperUri
    override val isPanicAutoRestore: Flow<Boolean> = preferences.isPanicAutoRestore

    override suspend fun setThemeMode(mode: String) { preferences.setThemeMode(mode) }
    override suspend fun setDynamicColorEnabled(enabled: Boolean) { preferences.setDynamicColorEnabled(enabled) }
    override suspend fun setRatingMode(mode: ContentRatingMode) { preferences.setRatingMode(mode) }
    override suspend fun setDiscretionBlurEnabled(enabled: Boolean) { preferences.setDiscretionBlurEnabled(enabled) }
    override suspend fun setTagBlacklist(tags: String) { preferences.setTagBlacklist(tags) }
    override suspend fun setCardDensity(density: String) { preferences.setCardDensity(density) }
    override suspend fun setShowHomeHero(show: Boolean) { preferences.setShowHomeHero(show) }
    override suspend fun setShowHomeTrending(show: Boolean) { preferences.setShowHomeTrending(show) }
    override suspend fun setShowHomeFranchises(show: Boolean) { preferences.setShowHomeFranchises(show) }
    override suspend fun setHasAgreedToTerms(agreed: Boolean) { preferences.setHasAgreedToTerms(agreed) }
    override suspend fun setUserName(name: String) { preferences.setUserName(name) }
    override suspend fun setIsGreetingNameEnabled(enabled: Boolean) { preferences.setIsGreetingNameEnabled(enabled) }
    override suspend fun setHasPromptedForName(prompted: Boolean) { preferences.setHasPromptedForName(prompted) }
    override suspend fun incrementAppliedCount() { preferences.incrementAppliedCount() }

    override suspend fun setPanicModeEnabled(enabled: Boolean) { preferences.setPanicModeEnabled(enabled) }
    override suspend fun setPanicTriggerType(type: String) { preferences.setPanicTriggerType(type) }
    override suspend fun setPanicSafeSsid(ssid: String) { preferences.setPanicSafeSsid(ssid) }
    override suspend fun setPanicWifiSsids(ssids: Set<String>) { preferences.setPanicWifiSsids(ssids) }
    override suspend fun addPanicWifiSsid(ssid: String) { preferences.addPanicWifiSsid(ssid) }
    override suspend fun removePanicWifiSsid(ssid: String) { preferences.removePanicWifiSsid(ssid) }
    override suspend fun setPanicWifiMode(mode: String) { preferences.setPanicWifiMode(mode) }
    override suspend fun setIsPanicActive(active: Boolean) { preferences.setIsPanicActive(active) }
    override suspend fun setNormalWallpaperUri(uri: String) { preferences.setNormalWallpaperUri(uri) }
    override suspend fun setPanicSafeLocation(lat: Double, lng: Double, radius: Float) { preferences.setPanicSafeLocation(lat, lng, radius) }
    override suspend fun setPanicWallpaperUri(uri: String) { preferences.setPanicWallpaperUri(uri) }
    override suspend fun setPanicAutoRestore(autoRestore: Boolean) { preferences.setPanicAutoRestore(autoRestore) }
}
