package com.wallsafe.core.panic

import android.content.Context
import android.net.ConnectivityManager
import android.net.Network
import android.net.NetworkCapabilities
import android.net.NetworkRequest
import android.net.wifi.WifiManager
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.launch
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class WifiPanicMonitor @Inject constructor(
    @ApplicationContext private val context: Context,
    private val triggerHandler: PanicTriggerHandler
) {
    private val scope = CoroutineScope(Dispatchers.IO + SupervisorJob())
    private var isMonitoring = false

    private val connectivityManager = context.getSystemService(Context.CONNECTIVITY_SERVICE) as? ConnectivityManager
    private val wifiManager = context.applicationContext.getSystemService(Context.WIFI_SERVICE) as? WifiManager

    private val networkCallback = object : ConnectivityManager.NetworkCallback() {
        override fun onAvailable(network: Network) {
            checkNetworkState()
        }

        override fun onLost(network: Network) {
            checkNetworkState()
        }

        override fun onCapabilitiesChanged(network: Network, networkCapabilities: NetworkCapabilities) {
            checkNetworkState()
        }
    }

    fun startMonitoring() {
        if (isMonitoring || connectivityManager == null) return
        try {
            val request = NetworkRequest.Builder()
                .addCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET)
                .build()
            connectivityManager.registerNetworkCallback(request, networkCallback)
            isMonitoring = true
        } catch (e: Exception) {
            e.printStackTrace()
        }
    }

    fun stopMonitoring() {
        if (!isMonitoring || connectivityManager == null) return
        try {
            connectivityManager.unregisterNetworkCallback(networkCallback)
            isMonitoring = false
        } catch (e: Exception) {
            e.printStackTrace()
        }
    }

    fun checkNetworkState() {
        scope.launch {
            try {
                val isWifi = isConnectedToWifi()
                val currentSsid = if (isWifi) getCurrentSsid() else null
                triggerHandler.onWifiChanged(context, currentSsid, isWifi)
            } catch (e: Exception) {
                e.printStackTrace()
            }
        }
    }

    fun getCurrentSsid(): String? {
        return try {
            val connectionInfo = wifiManager?.connectionInfo
            val ssid = connectionInfo?.ssid
            if (ssid != null && ssid != "<unknown ssid>") {
                ssid.trim('"', ' ')
            } else null
        } catch (e: Exception) {
            null
        }
    }

    fun isConnectedToWifi(): Boolean {
        val cm = connectivityManager ?: return false
        val activeNetwork = cm.activeNetwork ?: return false
        val capabilities = cm.getNetworkCapabilities(activeNetwork) ?: return false
        return capabilities.hasTransport(NetworkCapabilities.TRANSPORT_WIFI)
    }
}
