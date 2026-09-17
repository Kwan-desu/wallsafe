package com.wallsafe.feature.settings

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.automirrored.filled.ArrowForwardIos
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalUriHandler
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.wallsafe.core.ui.components.LegalDialog
import com.wallsafe.core.ui.components.LegalDocument
import com.wallsafe.feature.settings.components.SettingsListItem
import com.wallsafe.feature.settings.components.SettingsSection

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun LegalAndPrivacyScreen(
    onBack: () -> Unit,
    modifier: Modifier = Modifier
) {
    BackHandler { onBack() }

    val uriHandler = LocalUriHandler.current
    var activeLegalDialog by remember { mutableStateOf<LegalDocument?>(null) }
    var showPermissionsDialog by remember { mutableStateOf(false) }

    if (showPermissionsDialog) {
        com.wallsafe.feature.settings.components.PermissionsDialog(
            onDismiss = { showPermissionsDialog = false }
        )
    }

    activeLegalDialog?.let { doc ->
        LegalDialog(
            document = doc,
            onDismiss = { activeLegalDialog = null }
        )
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        text = "Legal & Privacy",
                        fontWeight = FontWeight.Bold
                    )
                },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(
                            imageVector = Icons.AutoMirrored.Filled.ArrowBack,
                            contentDescription = "Back"
                        )
                    }
                }
            )
        },
        modifier = modifier
    ) { paddingValues ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues),
            contentAlignment = Alignment.TopCenter
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .widthIn(max = 800.dp)
                    .verticalScroll(rememberScrollState())
                    .padding(vertical = 8.dp)
            ) {
                // Privacy Guarantee Banner Card
                Card(
                    colors = CardDefaults.cardColors(
                        containerColor = MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.5f)
                    ),
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(horizontal = 16.dp, vertical = 8.dp)
                ) {
                    Column(modifier = Modifier.padding(16.dp)) {
                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.spacedBy(10.dp)
                        ) {
                            Icon(
                                imageVector = Icons.Default.Security,
                                contentDescription = null,
                                tint = MaterialTheme.colorScheme.primary
                            )
                            Text(
                                text = "Zero Tracking Commitment",
                                style = MaterialTheme.typography.titleMedium,
                                fontWeight = FontWeight.Bold,
                                color = MaterialTheme.colorScheme.onPrimaryContainer
                            )
                        }
                        Spacer(modifier = Modifier.height(6.dp))
                        Text(
                            text = "WallSafe does not collect, record, profile, or sell your personal data. All favorites, downloads, and preferences remain strictly on your device.",
                            style = MaterialTheme.typography.bodySmall,
                            lineHeight = 18.sp,
                            color = MaterialTheme.colorScheme.onPrimaryContainer.copy(alpha = 0.85f)
                        )
                    }
                }

                SettingsSection(title = "Agreements & Policies") {
                    SettingsListItem(
                        title = "Privacy Policy",
                        subtitle = "Detailed disclosure of zero data collection",
                        icon = Icons.Default.PrivacyTip,
                        trailingContent = {
                            Icon(
                                imageVector = Icons.AutoMirrored.Filled.ArrowForwardIos,
                                contentDescription = null,
                                modifier = Modifier.size(14.dp),
                                tint = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.6f)
                            )
                        },
                        onClick = { activeLegalDialog = LegalDocument.PRIVACY_POLICY }
                    )
                    SettingsListItem(
                        title = "Terms of Service",
                        subtitle = "User agreement & acceptable use",
                        icon = Icons.Default.Gavel,
                        trailingContent = {
                            Icon(
                                imageVector = Icons.AutoMirrored.Filled.ArrowForwardIos,
                                contentDescription = null,
                                modifier = Modifier.size(14.dp),
                                tint = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.6f)
                            )
                        },
                        onClick = { activeLegalDialog = LegalDocument.TERMS_OF_SERVICE }
                    )
                    SettingsListItem(
                        title = "End User License Agreement (EULA)",
                        subtitle = "Google Play compliant standard license",
                        icon = Icons.Default.VerifiedUser,
                        trailingContent = {
                            Icon(
                                imageVector = Icons.AutoMirrored.Filled.ArrowForwardIos,
                                contentDescription = null,
                                modifier = Modifier.size(14.dp),
                                tint = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.6f)
                            )
                        },
                        onClick = { activeLegalDialog = LegalDocument.EULA }
                    )
                    SettingsListItem(
                        title = "Device Permissions Manager",
                        subtitle = "Live status & access control for Storage, Location & Notifications",
                        icon = Icons.Default.Security,
                        trailingContent = {
                            Icon(
                                imageVector = Icons.AutoMirrored.Filled.ArrowForwardIos,
                                contentDescription = null,
                                modifier = Modifier.size(14.dp),
                                tint = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.6f)
                            )
                        },
                        onClick = { showPermissionsDialog = true }
                    )
                    SettingsListItem(
                        title = "Permissions & Data Safety Policy",
                        subtitle = "Written disclosure of device permissions & privacy",
                        icon = Icons.Default.Policy,
                        trailingContent = {
                            Icon(
                                imageVector = Icons.AutoMirrored.Filled.ArrowForwardIos,
                                contentDescription = null,
                                modifier = Modifier.size(14.dp),
                                tint = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.6f)
                            )
                        },
                        onClick = { activeLegalDialog = LegalDocument.DATA_SAFETY }
                    )
                }

                SettingsSection(title = "Online Resources") {
                    SettingsListItem(
                        title = "View Online Privacy Policy",
                        subtitle = "Hosted public policy for Google Play",
                        icon = Icons.Default.OpenInBrowser,
                        onClick = {
                            uriHandler.openUri("https://kwan-desu.github.io/wallsafe/privacy_policy.html")
                        }
                    )
                    SettingsListItem(
                        title = "GitHub Repository",
                        subtitle = "Review source code and licenses",
                        icon = Icons.Default.Code,
                        onClick = {
                            uriHandler.openUri("https://github.com/Kwan-desu/wallsafe")
                        }
                    )
                }
            }
        }
    }
}
