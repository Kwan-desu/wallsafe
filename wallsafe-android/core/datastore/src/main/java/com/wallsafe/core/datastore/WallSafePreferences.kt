package com.wallsafe.core.datastore

import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.*
import com.wallsafe.core.model.ContentRatingMode
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.map
import javax.inject.Inject

class WallSafePreferences @Inject constructor(
    private val dataStore: DataStore<Preferences>
) {
    val themeMode: Flow<String> = dataStore.data.map { it[THEME_MODE] ?: "system" }.distinctUntilChanged()
    val isDynamicColorEnabled: Flow<Boolean> = dataStore.data.map { it[DYNAMIC_COLOR] ?: true }.distinctUntilChanged()
    val ratingMode: Flow<ContentRatingMode> = dataStore.data.map { prefs ->
        val mode = prefs[RATING_MODE] ?: ContentRatingMode.SfwOnly.name
        try { ContentRatingMode.valueOf(mode) } catch (e: Exception) { ContentRatingMode.SfwOnly }
    }.distinctUntilChanged()
    val isDiscretionBlurEnabled: Flow<Boolean> = dataStore.data.map { it[DISCRETION_BLUR] ?: true }.distinctUntilChanged()
    val tagBlacklist: Flow<String> = dataStore.data.map { it[TAG_BLACKLIST] ?: "guro,scat,loli,shota" }.distinctUntilChanged()
    val cardDensity: Flow<String> = dataStore.data.map { it[CARD_DENSITY] ?: "comfortable" }.distinctUntilChanged()
    val showHomeHero: Flow<Boolean> = dataStore.data.map { it[SHOW_HOME_HERO] ?: true }.distinctUntilChanged()
    val showHomeTrending: Flow<Boolean> = dataStore.data.map { it[SHOW_HOME_TRENDING] ?: true }.distinctUntilChanged()
    val showHomeFranchises: Flow<Boolean> = dataStore.data.map { it[SHOW_HOME_FRANCHISES] ?: true }.distinctUntilChanged()
    val filterProfiles: Flow<String> = dataStore.data.map { it[FILTER_PROFILES] ?: "{}" }.distinctUntilChanged()
    val hasAgreedToTerms: Flow<Boolean> = dataStore.data.map { it[HAS_AGREED_TO_TERMS] ?: false }.distinctUntilChanged()
    val userName: Flow<String> = dataStore.data.map { it[USER_NAME] ?: "" }.distinctUntilChanged()
    val isGreetingNameEnabled: Flow<Boolean> = dataStore.data.map { it[IS_GREETING_NAME_ENABLED] ?: true }.distinctUntilChanged()
    val hasPromptedForName: Flow<Boolean> = dataStore.data.map { it[HAS_PROMPTED_FOR_NAME] ?: false }.distinctUntilChanged()
    val appliedWallpapersCount: Flow<Int> = dataStore.data.map { it[APPLIED_WALLPAPERS_COUNT] ?: 15 }.distinctUntilChanged()

    suspend fun setUserName(name: String): Unit { dataStore.edit { it[USER_NAME] = name } }
    suspend fun setIsGreetingNameEnabled(enabled: Boolean): Unit { dataStore.edit { it[IS_GREETING_NAME_ENABLED] = enabled } }
    suspend fun setHasPromptedForName(prompted: Boolean): Unit { dataStore.edit { it[HAS_PROMPTED_FOR_NAME] = prompted } }
    suspend fun saveInitialUserName(name: String): Unit {
        dataStore.edit {
            it[HAS_PROMPTED_FOR_NAME] = true
            if (name.isNotBlank()) {
                it[USER_NAME] = name
            }
        }
    }
    suspend fun incrementAppliedCount(): Unit { dataStore.edit { it[APPLIED_WALLPAPERS_COUNT] = (it[APPLIED_WALLPAPERS_COUNT] ?: 15) + 1 } }

    // Panic Mode Preferences
    val isPanicModeEnabled: Flow<Boolean> = dataStore.data.map { it[PANIC_MODE_ENABLED] ?: false }
    val panicTriggerType: Flow<String> = dataStore.data.map { it[PANIC_TRIGGER_TYPE] ?: "wifi" }
    val panicSafeSsid: Flow<String> = dataStore.data.map { it[PANIC_SAFE_SSID] ?: "" }
    val panicWifiSsids: Flow<Set<String>> = dataStore.data.map { prefs ->
        val set = prefs[PANIC_WIFI_SSIDS] ?: emptySet()
        val legacy = prefs[PANIC_SAFE_SSID] ?: ""
        if (set.isEmpty() && legacy.isNotBlank()) {
            setOf(legacy)
        } else {
            set
        }
    }
    val panicWifiMode: Flow<String> = dataStore.data.map { it[PANIC_WIFI_MODE] ?: "whitelist" }
    val isPanicActive: Flow<Boolean> = dataStore.data.map { it[IS_PANIC_ACTIVE] ?: false }
    val normalWallpaperUri: Flow<String> = dataStore.data.map { it[NORMAL_WALLPAPER_URI] ?: "" }
    val panicSafeLat: Flow<Double> = dataStore.data.map { it[PANIC_SAFE_LAT] ?: 0.0 }
    val panicSafeLng: Flow<Double> = dataStore.data.map { it[PANIC_SAFE_LNG] ?: 0.0 }
    val panicSafeRadiusMeters: Flow<Float> = dataStore.data.map { it[PANIC_SAFE_RADIUS] ?: 500f }
    val panicWallpaperUri: Flow<String> = dataStore.data.map { it[PANIC_WALLPAPER_URI] ?: "" }
    val isPanicAutoRestore: Flow<Boolean> = dataStore.data.map { it[PANIC_AUTO_RESTORE] ?: true }

    suspend fun setThemeMode(mode: String): Unit { dataStore.edit { it[THEME_MODE] = mode } }
    suspend fun setDynamicColorEnabled(enabled: Boolean): Unit { dataStore.edit { it[DYNAMIC_COLOR] = enabled } }
    suspend fun setRatingMode(mode: ContentRatingMode): Unit { dataStore.edit { it[RATING_MODE] = mode.name } }
    suspend fun setDiscretionBlurEnabled(enabled: Boolean): Unit { dataStore.edit { it[DISCRETION_BLUR] = enabled } }
    suspend fun setTagBlacklist(tags: String): Unit { dataStore.edit { it[TAG_BLACKLIST] = tags } }

    suspend fun addTagToBlacklist(tag: String) {
        dataStore.edit { prefs ->
            val current = prefs[TAG_BLACKLIST] ?: "guro,scat,loli,shota"
            val list = current.split(",").map { it.trim() }.filter { it.isNotBlank() }.toMutableList()
            if (!list.any { it.equals(tag.trim(), ignoreCase = true) }) {
                list.add(tag.trim().lowercase())
                prefs[TAG_BLACKLIST] = list.joinToString(",")
            }
        }
    }

    suspend fun removeTagFromBlacklist(tag: String) {
        dataStore.edit { prefs ->
            val current = prefs[TAG_BLACKLIST] ?: "guro,scat,loli,shota"
            val list = current.split(",").map { it.trim() }.filter { it.isNotBlank() }.toMutableList()
            list.removeAll { it.equals(tag.trim(), ignoreCase = true) }
            prefs[TAG_BLACKLIST] = list.joinToString(",")
        }
    }

    val appDisguise: Flow<String> = dataStore.data.map { it[APP_DISGUISE] ?: "default" }
    suspend fun setAppDisguise(disguise: String): Unit { dataStore.edit { it[APP_DISGUISE] = disguise } }

    val customSourcesJson: Flow<String> = dataStore.data.map { it[CUSTOM_SOURCES] ?: "[]" }
    suspend fun setCustomSourcesJson(json: String): Unit { dataStore.edit { it[CUSTOM_SOURCES] = json } }

    val useSystemWallpaperCropper: Flow<Boolean> = dataStore.data.map { it[USE_SYSTEM_WALLPAPER_CROPPER] ?: true }
    suspend fun setUseSystemWallpaperCropper(enabled: Boolean): Unit { dataStore.edit { it[USE_SYSTEM_WALLPAPER_CROPPER] = enabled } }
    suspend fun setCardDensity(density: String): Unit { dataStore.edit { it[CARD_DENSITY] = density } }
    suspend fun setShowHomeHero(show: Boolean): Unit { dataStore.edit { it[SHOW_HOME_HERO] = show } }
    suspend fun setShowHomeTrending(show: Boolean): Unit { dataStore.edit { it[SHOW_HOME_TRENDING] = show } }
    suspend fun setShowHomeFranchises(show: Boolean): Unit { dataStore.edit { it[SHOW_HOME_FRANCHISES] = show } }
    suspend fun setFilterProfiles(profilesJson: String): Unit { dataStore.edit { it[FILTER_PROFILES] = profilesJson } }
    suspend fun setHasAgreedToTerms(agreed: Boolean): Unit { dataStore.edit { it[HAS_AGREED_TO_TERMS] = agreed } }

    suspend fun setPanicModeEnabled(enabled: Boolean): Unit { dataStore.edit { it[PANIC_MODE_ENABLED] = enabled } }
    suspend fun setPanicTriggerType(type: String): Unit { dataStore.edit { it[PANIC_TRIGGER_TYPE] = type } }
    suspend fun setPanicSafeSsid(ssid: String): Unit { dataStore.edit { it[PANIC_SAFE_SSID] = ssid } }
    suspend fun setPanicWifiSsids(ssids: Set<String>): Unit { dataStore.edit { it[PANIC_WIFI_SSIDS] = ssids } }
    suspend fun addPanicWifiSsid(ssid: String): Unit {
        dataStore.edit { prefs ->
            val current = (prefs[PANIC_WIFI_SSIDS] ?: emptySet()).toMutableSet()
            val legacy = prefs[PANIC_SAFE_SSID] ?: ""
            if (legacy.isNotBlank()) current.add(legacy)
            current.add(ssid.trim())
            prefs[PANIC_WIFI_SSIDS] = current
        }
    }
    suspend fun removePanicWifiSsid(ssid: String): Unit {
        dataStore.edit { prefs ->
            val current = (prefs[PANIC_WIFI_SSIDS] ?: emptySet()).toMutableSet()
            current.remove(ssid.trim())
            prefs[PANIC_WIFI_SSIDS] = current
            if (prefs[PANIC_SAFE_SSID]?.equals(ssid.trim(), ignoreCase = true) == true) {
                prefs[PANIC_SAFE_SSID] = ""
            }
        }
    }
    suspend fun setPanicWifiMode(mode: String): Unit { dataStore.edit { it[PANIC_WIFI_MODE] = mode } }
    suspend fun setIsPanicActive(active: Boolean): Unit { dataStore.edit { it[IS_PANIC_ACTIVE] = active } }
    suspend fun setNormalWallpaperUri(uri: String): Unit { dataStore.edit { it[NORMAL_WALLPAPER_URI] = uri } }
    suspend fun setPanicSafeLocation(lat: Double, lng: Double, radius: Float): Unit {
        dataStore.edit {
            it[PANIC_SAFE_LAT] = lat
            it[PANIC_SAFE_LNG] = lng
            it[PANIC_SAFE_RADIUS] = radius
        }
    }
    suspend fun setPanicWallpaperUri(uri: String): Unit { dataStore.edit { it[PANIC_WALLPAPER_URI] = uri } }
    suspend fun setPanicAutoRestore(autoRestore: Boolean): Unit { dataStore.edit { it[PANIC_AUTO_RESTORE] = autoRestore } }

    val isAutoUpdateCheckEnabled: Flow<Boolean> = dataStore.data.map { it[AUTO_UPDATE_CHECK_ENABLED] ?: true }
    suspend fun setAutoUpdateCheckEnabled(enabled: Boolean): Unit { dataStore.edit { it[AUTO_UPDATE_CHECK_ENABLED] = enabled } }

    val recentSearches: Flow<List<String>> = dataStore.data.map { prefs ->
        val raw = prefs[RECENT_SEARCHES] ?: ""
        if (raw.isBlank()) emptyList()
        else raw.split("\n").filter { it.isNotBlank() }
    }.distinctUntilChanged()

    suspend fun addRecentSearch(query: String) {
        val clean = query.trim()
        if (clean.isBlank()) return
        dataStore.edit { prefs ->
            val raw = prefs[RECENT_SEARCHES] ?: ""
            val list = if (raw.isBlank()) mutableListOf() else raw.split("\n").filter { it.isNotBlank() }.toMutableList()
            list.removeAll { it.equals(clean, ignoreCase = true) }
            list.add(0, clean)
            val trimmed = list.take(20)
            prefs[RECENT_SEARCHES] = trimmed.joinToString("\n")
        }
    }

    suspend fun removeRecentSearch(query: String) {
        dataStore.edit { prefs ->
            val raw = prefs[RECENT_SEARCHES] ?: ""
            if (raw.isNotBlank()) {
                val list = raw.split("\n").filter { it.isNotBlank() }.toMutableList()
                list.removeAll { it.equals(query.trim(), ignoreCase = true) }
                prefs[RECENT_SEARCHES] = list.joinToString("\n")
            }
        }
    }

    suspend fun clearRecentSearches() {
        dataStore.edit { prefs ->
            prefs.remove(RECENT_SEARCHES)
        }
    }

    companion object {
        val RECENT_SEARCHES = stringPreferencesKey("recent_searches")
        val AUTO_UPDATE_CHECK_ENABLED = booleanPreferencesKey("auto_update_check_enabled")
        val HAS_AGREED_TO_TERMS = booleanPreferencesKey("has_agreed_to_terms")
        val USER_NAME = stringPreferencesKey("user_name")
        val IS_GREETING_NAME_ENABLED = booleanPreferencesKey("is_greeting_name_enabled")
        val HAS_PROMPTED_FOR_NAME = booleanPreferencesKey("has_prompted_for_name")
        val APPLIED_WALLPAPERS_COUNT = intPreferencesKey("applied_wallpapers_count")
        val THEME_MODE = stringPreferencesKey("theme_mode")
        val DYNAMIC_COLOR = booleanPreferencesKey("dynamic_color")
        val RATING_MODE = stringPreferencesKey("rating_mode")
        val DISCRETION_BLUR = booleanPreferencesKey("discretion_blur")
        val TAG_BLACKLIST = stringPreferencesKey("tag_blacklist")
        val CARD_DENSITY = stringPreferencesKey("card_density")
        
        val SHOW_HOME_HERO = booleanPreferencesKey("show_home_hero")
        val SHOW_HOME_TRENDING = booleanPreferencesKey("show_home_trending")
        val SHOW_HOME_FRANCHISES = booleanPreferencesKey("show_home_franchises")
        
        val FILTER_PROFILES = stringPreferencesKey("filter_profiles")

        val APP_DISGUISE = stringPreferencesKey("app_disguise")
        val CUSTOM_SOURCES = stringPreferencesKey("custom_sources")
        val USE_SYSTEM_WALLPAPER_CROPPER = booleanPreferencesKey("use_system_wallpaper_cropper")

        val PANIC_MODE_ENABLED = booleanPreferencesKey("panic_mode_enabled")
        val PANIC_TRIGGER_TYPE = stringPreferencesKey("panic_trigger_type")
        val PANIC_SAFE_SSID = stringPreferencesKey("panic_safe_ssid")
        val PANIC_WIFI_SSIDS = stringSetPreferencesKey("panic_wifi_ssids")
        val PANIC_WIFI_MODE = stringPreferencesKey("panic_wifi_mode")
        val IS_PANIC_ACTIVE = booleanPreferencesKey("is_panic_active")
        val NORMAL_WALLPAPER_URI = stringPreferencesKey("normal_wallpaper_uri")
        val PANIC_SAFE_LAT = doublePreferencesKey("panic_safe_lat")
        val PANIC_SAFE_LNG = doublePreferencesKey("panic_safe_lng")
        val PANIC_SAFE_RADIUS = floatPreferencesKey("panic_safe_radius")
        val PANIC_WALLPAPER_URI = stringPreferencesKey("panic_wallpaper_uri")
        val PANIC_AUTO_RESTORE = booleanPreferencesKey("panic_auto_restore")
    }
}
