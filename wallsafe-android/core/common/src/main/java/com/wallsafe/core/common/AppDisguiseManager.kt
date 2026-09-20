package com.wallsafe.core.common

import android.content.ComponentName
import android.content.Context
import android.content.pm.PackageManager
import dagger.hilt.android.qualifiers.ApplicationContext
import javax.inject.Inject
import javax.inject.Singleton

enum class AppDisguise(val key: String, val title: String, val aliasName: String) {
    DEFAULT("default", "WallSafe (Default)", "com.wallsafe.MainActivityAliasDefault"),
    CALCULATOR("calculator", "Calculator", "com.wallsafe.MainActivityAliasCalculator"),
    NOTES("notes", "Notes", "com.wallsafe.MainActivityAliasNotes");

    companion object {
        fun fromKey(key: String): AppDisguise = entries.find { it.key.equals(key, ignoreCase = true) } ?: DEFAULT
    }
}

@Singleton
class AppDisguiseManager @Inject constructor(
    @ApplicationContext private val context: Context
) {
    fun setDisguise(disguise: AppDisguise) {
        val pm = context.packageManager
        for (item in AppDisguise.entries) {
            val component = ComponentName(context.packageName, item.aliasName)
            val state = if (item == disguise) {
                PackageManager.COMPONENT_ENABLED_STATE_ENABLED
            } else {
                PackageManager.COMPONENT_ENABLED_STATE_DISABLED
            }
            try {
                pm.setComponentEnabledSetting(
                    component,
                    state,
                    PackageManager.DONT_KILL_APP
                )
            } catch (e: Exception) {
                e.printStackTrace()
            }
        }
    }
}
