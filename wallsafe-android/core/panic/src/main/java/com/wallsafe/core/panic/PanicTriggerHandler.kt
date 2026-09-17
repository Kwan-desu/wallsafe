package com.wallsafe.core.panic

import android.content.Context
import android.location.Location
import com.wallsafe.core.datastore.WallSafePreferences
import kotlinx.coroutines.flow.first
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class PanicTriggerHandler @Inject constructor(
    private val panicManager: PanicManager,
    private val preferences: WallSafePreferences
) {
    suspend fun onWifiChanged(context: Context, currentSsid: String?, isConnectedToWifi: Boolean) {
        val isEnabled = preferences.isPanicModeEnabled.first()
        if (!isEnabled) return

        val triggerType = preferences.panicTriggerType.first()
        val autoRestore = preferences.isPanicAutoRestore.first()
        val customUri = preferences.panicWallpaperUri.first()

        when (triggerType) {
            "wifi" -> {
                val wifiMode = preferences.panicWifiMode.first() // "whitelist" or "blacklist"
                val configuredSsids = preferences.panicWifiSsids.first()
                if (configuredSsids.isEmpty()) return

                val cleanCurrent = currentSsid?.trim('"', ' ') ?: ""
                val matchesConfigured = isConnectedToWifi && cleanCurrent.isNotBlank() && configuredSsids.any {
                    it.trim('"', ' ').equals(cleanCurrent, ignoreCase = true)
                }

                if (wifiMode == "blacklist") {
                    // Blacklist: Connecting to any blacklisted Wi-Fi triggers panic
                    if (matchesConfigured) {
                        panicManager.triggerPanic(context, customUri)
                    } else if (autoRestore && panicManager.isPanicActive()) {
                        panicManager.restorePanic(context)
                    }
                } else {
                    // Whitelist: Must be on safe list. Any other Wi-Fi or disconnection triggers panic
                    if (matchesConfigured) {
                        if (autoRestore && panicManager.isPanicActive()) {
                            panicManager.restorePanic(context)
                        }
                    } else {
                        panicManager.triggerPanic(context, customUri)
                    }
                }
            }
            "wifi_disconnect" -> {
                if (!isConnectedToWifi) {
                    panicManager.triggerPanic(context, customUri)
                } else if (autoRestore && panicManager.isPanicActive()) {
                    panicManager.restorePanic(context)
                }
            }
        }
    }

    suspend fun onLocationChanged(context: Context, currentLat: Double, currentLng: Double) {
        val isEnabled = preferences.isPanicModeEnabled.first()
        if (!isEnabled) return

        val triggerType = preferences.panicTriggerType.first()
        if (triggerType != "location") return

        val safeLat = preferences.panicSafeLat.first()
        val safeLng = preferences.panicSafeLng.first()
        val radius = preferences.panicSafeRadiusMeters.first()
        val autoRestore = preferences.isPanicAutoRestore.first()
        val customUri = preferences.panicWallpaperUri.first()

        if (safeLat == 0.0 && safeLng == 0.0) return

        val results = FloatArray(1)
        Location.distanceBetween(currentLat, currentLng, safeLat, safeLng, results)
        val distanceMeters = results[0]

        if (distanceMeters > radius) {
            panicManager.triggerPanic(context, customUri)
        } else if (autoRestore && panicManager.isPanicActive()) {
            panicManager.restorePanic(context)
        }
    }

    suspend fun testPanic(context: Context): Boolean {
        val customUri = preferences.panicWallpaperUri.first()
        return panicManager.triggerPanic(context, customUri)
    }

    suspend fun restorePanic(context: Context): Boolean {
        return panicManager.restorePanic(context)
    }

    suspend fun isPanicActive(): Boolean = panicManager.isPanicActive()
    fun isPanicActiveSync(): Boolean = panicManager.isPanicActiveSync()
}
