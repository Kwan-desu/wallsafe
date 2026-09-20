package com.wallsafe.core.model

import kotlinx.serialization.Serializable

@Serializable
data class AppUpdateInfo(
    val versionName: String = "",
    val releaseName: String = "",
    val releaseNotes: String = "",
    val downloadUrl: String = "",
    val apkFileName: String = "",
    val apkSize: Long = 0L,
    val isUpdateAvailable: Boolean = false,
    val assetId: Long = 0L
)
