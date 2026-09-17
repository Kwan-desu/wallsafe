package com.wallsafe.core.panic

import android.Manifest
import android.content.Context
import android.content.pm.PackageManager
import android.location.Location
import android.location.LocationManager
import androidx.core.content.ContextCompat
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.launch
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class LocationPanicHelper @Inject constructor(
    @ApplicationContext private val context: Context,
    private val triggerHandler: PanicTriggerHandler
) {
    private val scope = CoroutineScope(Dispatchers.IO + SupervisorJob())
    private val locationManager = context.getSystemService(Context.LOCATION_SERVICE) as? LocationManager

    fun hasLocationPermission(): Boolean {
        val fine = ContextCompat.checkSelfPermission(context, Manifest.permission.ACCESS_FINE_LOCATION)
        val coarse = ContextCompat.checkSelfPermission(context, Manifest.permission.ACCESS_COARSE_LOCATION)
        return fine == PackageManager.PERMISSION_GRANTED || coarse == PackageManager.PERMISSION_GRANTED
    }

    fun getLastKnownLocation(): Pair<Double, Double>? {
        if (!hasLocationPermission() || locationManager == null) return null

        try {
            var bestLocation: Location? = null
            val providers = locationManager.getProviders(true)
            for (provider in providers) {
                val loc = locationManager.getLastKnownLocation(provider) ?: continue
                if (bestLocation == null || loc.accuracy < bestLocation.accuracy) {
                    bestLocation = loc
                }
            }
            return bestLocation?.let { Pair(it.latitude, it.longitude) }
        } catch (e: SecurityException) {
            e.printStackTrace()
            return null
        }
    }

    fun checkCurrentLocation() {
        scope.launch {
            val coords = getLastKnownLocation() ?: return@launch
            triggerHandler.onLocationChanged(context, coords.first, coords.second)
        }
    }
}
