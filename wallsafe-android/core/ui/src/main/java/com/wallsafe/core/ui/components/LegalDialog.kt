package com.wallsafe.core.ui.components

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Gavel
import androidx.compose.material.icons.filled.PrivacyTip
import androidx.compose.material.icons.filled.Security
import androidx.compose.material.icons.filled.VerifiedUser
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalUriHandler
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties

enum class LegalDocument(val title: String) {
    PRIVACY_POLICY("Privacy Policy"),
    TERMS_OF_SERVICE("Terms of Service"),
    EULA("End User License Agreement"),
    DATA_SAFETY("Permissions & Data Safety")
}

@Composable
fun LegalDialog(
    document: LegalDocument,
    onDismiss: () -> Unit
) {
    val uriHandler = LocalUriHandler.current

    AlertDialog(
        onDismissRequest = onDismiss,
        title = {
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(10.dp)
            ) {
                Icon(
                    imageVector = when (document) {
                        LegalDocument.PRIVACY_POLICY -> Icons.Default.PrivacyTip
                        LegalDocument.TERMS_OF_SERVICE -> Icons.Default.Gavel
                        LegalDocument.EULA -> Icons.Default.VerifiedUser
                        LegalDocument.DATA_SAFETY -> Icons.Default.Security
                    },
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.primary
                )
                Text(
                    text = document.title,
                    style = MaterialTheme.typography.titleLarge,
                    fontWeight = FontWeight.Bold
                )
            }
        },
        text = {
            val scrollState = rememberScrollState()
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .heightIn(max = 420.dp)
                    .verticalScroll(scrollState)
            ) {
                Text(
                    text = getDocumentContent(document),
                    style = MaterialTheme.typography.bodyMedium,
                    lineHeight = 22.sp,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
        },
        confirmButton = {
            Button(onClick = onDismiss) {
                Text("Close")
            }
        },
        dismissButton = {
            TextButton(
                onClick = {
                    uriHandler.openUri("https://github.com/Kwan-desu/wallsafe")
                }
            ) {
                Text("Online Link")
            }
        },
        properties = DialogProperties(usePlatformDefaultWidth = false),
        modifier = Modifier.padding(24.dp)
    )
}

@Composable
fun FirstLaunchConsentDialog(
    onAccept: () -> Unit,
    onViewPrivacy: () -> Unit,
    onViewTerms: () -> Unit
) {
    AlertDialog(
        onDismissRequest = { /* Require explicit user action */ },
        icon = {
            Icon(
                imageVector = Icons.Default.Security,
                contentDescription = "Security",
                tint = MaterialTheme.colorScheme.primary,
                modifier = Modifier.size(36.dp)
            )
        },
        title = {
            Text(
                text = "Welcome to WallSafe",
                style = MaterialTheme.typography.headlineSmall,
                fontWeight = FontWeight.Bold
            )
        },
        text = {
            Column(
                verticalArrangement = Arrangement.spacedBy(10.dp)
            ) {
                Text(
                    text = "WallSafe is a privacy-first anime wallpaper manager designed with discretion and local-first architecture.",
                    style = MaterialTheme.typography.bodyMedium
                )
                Text(
                    text = "🔒 Zero Personal Data Collection:\nWe do not collect, track, profile, or sell your personal data. All favorites, downloads, and preferences stay 100% on your device.",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
                Text(
                    text = "📱 Device Permissions:\nNext, WallSafe will request permissions for Storage/Photos (to save wallpapers), Notifications (for download alerts), and Location (for Panic Mode Wi-Fi & Safe Zone automation).",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
                Text(
                    text = "By continuing, you agree to our Terms of Service & EULA and acknowledge our Privacy Policy.",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.outline
                )
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween
                ) {
                    TextButton(onClick = onViewPrivacy) {
                        Text("Privacy Policy", fontSize = 12.sp)
                    }
                    TextButton(onClick = onViewTerms) {
                        Text("Terms & EULA", fontSize = 12.sp)
                    }
                }
            }
        },
        confirmButton = {
            Button(
                onClick = onAccept,
                modifier = Modifier.fillMaxWidth()
            ) {
                Text("Agree & Continue")
            }
        }
    )
}

private fun getDocumentContent(document: LegalDocument): String {
    return when (document) {
        LegalDocument.PRIVACY_POLICY -> """
WallSafe Privacy Policy
Effective Date: September 14, 2026

1. Zero Data Collection
WallSafe does not collect, transmit, store, sell, or profile your personal data. There are no accounts, no telemetry SDKs, and no tracking beacons.

2. On-Device Storage
Your favorites, collections, downloaded images, and settings are stored locally on your device in secure app storage.

3. Third-Party APIs
WallSafe queries public community image repositories (such as Yande.re and Konachan) to search for wallpapers. Only generic HTTP queries are transmitted over encrypted HTTPS; no personal identifiers are sent.

4. Device Permissions
• INTERNET: To fetch public wallpapers.
• SET_WALLPAPER: To set wallpapers on your device.
• VIBRATE: For haptic feedback.
• POST_NOTIFICATIONS: For download notifications.

5. Data Deletion
Because all data is stored locally, clearing app cache or uninstalling the app permanently erases all data.

Contact: support@wallsafe.app
        """.trimIndent()

        LegalDocument.TERMS_OF_SERVICE -> """
WallSafe Terms of Service
Effective Date: September 14, 2026

1. Service Overview
WallSafe is a personal, client-side utility for discovering, previewing, and managing anime wallpapers.

2. Intellectual Property Rights
WallSafe does not own or host the artwork indexed by public third-party services. All copyrights belong to their respective creators and copyright owners.

3. User Conduct
You agree not to use the application for illegal purposes, or to disrupt third-party server infrastructure.

4. Disclaimer of Warranties
The application is provided "AS IS" without warranties of any kind. Developers are not liable for any damages resulting from app use.

Contact: support@wallsafe.app
        """.trimIndent()

        LegalDocument.EULA -> """
End User License Agreement (EULA)
Effective Date: September 14, 2026

1. Scope of License
WallSafe grants you a personal, revocable, non-exclusive license to use this app on your Android devices in accordance with Google Play Terms of Service.

2. Restrictions
You may not re-sell, bundle with malware, or redistribute modified copies containing tracking telemetry.

3. Maintenance
Google LLC has no obligation to provide maintenance or support for this application.

4. Termination
You may terminate this license anytime by uninstalling the application.

Contact: support@wallsafe.app
        """.trimIndent()

        LegalDocument.DATA_SAFETY -> """
Permissions & Data Safety Declaration

• Data Collected: NONE
• Data Shared: NONE
• Encryption in Transit: YES (HTTPS/TLS)
• Independent Deletion: YES (Local on-device storage)

Permission Justifications:
• INTERNET & NETWORK STATE: To browse and download wallpapers.
• ACCESS WIFI STATE: To detect network connectivity changes for Panic Mode.
• ACCESS FINE/COARSE LOCATION (Optional): Used strictly locally on-device to read Wi-Fi network names (SSID) or safe zone distance when Panic Mode is enabled by the user. Location is never stored, tracked, or sent to any server.
• SET WALLPAPER: To apply wallpapers directly from the app or widget.
• VIBRATE: Haptic confirmation when toggling favorites or setting wallpapers.
• POST NOTIFICATIONS: To notify when wallpaper downloads complete.
• STORAGE (Android 9 and below): To save downloaded wallpapers to device storage.
        """.trimIndent()
    }
}
