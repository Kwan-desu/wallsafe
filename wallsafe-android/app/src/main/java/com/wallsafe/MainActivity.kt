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
import com.wallsafe.core.ui.components.FirstLaunchConsentDialog
import com.wallsafe.core.ui.components.LegalDialog
import com.wallsafe.core.ui.components.LegalDocument
import com.wallsafe.core.ui.theme.WallSafeTheme
import dagger.hilt.android.AndroidEntryPoint
import kotlinx.coroutines.launch
import javax.inject.Inject

import androidx.compose.runtime.saveable.rememberSaveable

@AndroidEntryPoint
class MainActivity : ComponentActivity() {

    @Inject
    lateinit var settingsRepository: SettingsRepository

    @OptIn(ExperimentalMaterial3WindowSizeClassApi::class)
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            val windowSizeClass = calculateWindowSizeClass(this)
            val themeMode by settingsRepository.themeMode.collectAsStateWithLifecycle(initialValue = "system")
            val isDynamicColor by settingsRepository.isDynamicColorEnabled.collectAsStateWithLifecycle(initialValue = true)
            val hasAgreedToTerms by settingsRepository.hasAgreedToTerms.collectAsStateWithLifecycle(initialValue = null)
            val scope = rememberCoroutineScope()
            var activeLegalDoc by remember { mutableStateOf<LegalDocument?>(null) }
            var hasRequestedPermissions by rememberSaveable { mutableStateOf(false) }

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
