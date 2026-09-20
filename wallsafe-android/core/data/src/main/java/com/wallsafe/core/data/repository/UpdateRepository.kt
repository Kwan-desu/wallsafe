package com.wallsafe.core.data.repository

import android.content.Context
import android.content.Intent
import android.net.Uri
import android.os.Build
import android.provider.Settings
import androidx.core.content.FileProvider
import com.wallsafe.core.model.AppUpdateInfo
import com.wallsafe.core.network.GitHubApiService
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.OkHttpClient
import okhttp3.Request
import java.io.File
import java.io.FileOutputStream
import java.io.IOException
import javax.inject.Inject
import javax.inject.Singleton

interface UpdateRepository {
    suspend fun checkForUpdate(currentVersionName: String): Result<AppUpdateInfo>
    suspend fun downloadApk(
        updateInfo: AppUpdateInfo,
        onProgress: (progress: Float, bytesDownloaded: Long, totalBytes: Long) -> Unit
    ): Result<File>
    fun canRequestPackageInstalls(): Boolean
    fun openInstallPermissionSettings()
    fun installApk(apkFile: File): Result<Unit>
    fun getCachedApk(updateInfo: AppUpdateInfo): File?
}

@Singleton
class UpdateRepositoryImpl @Inject constructor(
    @ApplicationContext private val context: Context,
    private val gitHubApiService: GitHubApiService,
    private val okHttpClient: OkHttpClient
) : UpdateRepository {

    companion object {
        const val GITHUB_OWNER = "Kwan-desu"
        const val GITHUB_REPO = "wallsafe"
        // Token enables in-app updates while the repository is private
        const val AUTH_TOKEN = "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"
    }

    override suspend fun checkForUpdate(currentVersionName: String): Result<AppUpdateInfo> = withContext(Dispatchers.IO) {
        try {
            val authHeader = "Bearer $AUTH_TOKEN"
            val releases = gitHubApiService.getReleases(
                owner = GITHUB_OWNER,
                repo = GITHUB_REPO,
                authorization = authHeader
            )

            // Find the latest non-draft release containing an Android APK asset
            val targetRelease = releases.firstOrNull { release ->
                !release.draft && release.assets.any { it.name.endsWith(".apk", ignoreCase = true) }
            } ?: return@withContext Result.failure(IllegalStateException("No Android releases found"))

            val apkAsset = targetRelease.assets.firstOrNull { it.name.endsWith(".apk", ignoreCase = true) }
                ?: return@withContext Result.failure(IllegalStateException("No APK asset found in release"))

            val remoteTag = targetRelease.tagName
            val isNewer = isNewerVersion(remoteTag, currentVersionName)

            val updateInfo = AppUpdateInfo(
                versionName = remoteTag,
                releaseName = targetRelease.name ?: remoteTag,
                releaseNotes = targetRelease.body.orEmpty(),
                downloadUrl = apkAsset.browserDownloadUrl,
                apkFileName = apkAsset.name,
                apkSize = apkAsset.size,
                isUpdateAvailable = isNewer,
                assetId = apkAsset.id
            )

            Result.success(updateInfo)
        } catch (e: Exception) {
            Result.failure(e)
        }
    }

    override suspend fun downloadApk(
        updateInfo: AppUpdateInfo,
        onProgress: (progress: Float, bytesDownloaded: Long, totalBytes: Long) -> Unit
    ): Result<File> = withContext(Dispatchers.IO) {
        try {
            val updatesDir = File(context.cacheDir, "updates").apply { mkdirs() }
            val apkFile = File(updatesDir, updateInfo.apkFileName)
            if (apkFile.exists()) {
                apkFile.delete()
            }

            val downloadUrl = if (updateInfo.assetId > 0) {
                "https://api.github.com/repos/$GITHUB_OWNER/$GITHUB_REPO/releases/assets/${updateInfo.assetId}"
            } else {
                updateInfo.downloadUrl
            }

            val request = Request.Builder()
                .url(downloadUrl)
                .header("Accept", "application/octet-stream")
                .header("Authorization", "Bearer $AUTH_TOKEN")
                .build()

            val response = okHttpClient.newCall(request).execute()
            if (!response.isSuccessful) {
                return@withContext Result.failure(IOException("Server returned HTTP ${response.code}"))
            }

            val body = response.body ?: return@withContext Result.failure(IOException("Empty response body"))
            val totalBytes = if (updateInfo.apkSize > 0) updateInfo.apkSize else body.contentLength()

            body.byteStream().use { input ->
                FileOutputStream(apkFile).use { output ->
                    val buffer = ByteArray(8192)
                    var bytesReadTotal = 0L
                    var read: Int
                    while (input.read(buffer).also { read = it } != -1) {
                        output.write(buffer, 0, read)
                        bytesReadTotal += read
                        val progress = if (totalBytes > 0) {
                            (bytesReadTotal.toFloat() / totalBytes.toFloat()).coerceIn(0f, 1f)
                        } else {
                            -1f
                        }
                        onProgress(progress, bytesReadTotal, totalBytes)
                    }
                    output.flush()
                }
            }

            Result.success(apkFile)
        } catch (e: Exception) {
            Result.failure(e)
        }
    }

    override fun getCachedApk(updateInfo: AppUpdateInfo): File? {
        val updatesDir = File(context.cacheDir, "updates")
        val file = File(updatesDir, updateInfo.apkFileName)
        return if (file.exists() && file.length() > 0 && (updateInfo.apkSize == 0L || file.length() == updateInfo.apkSize)) {
            file
        } else {
            null
        }
    }

    override fun canRequestPackageInstalls(): Boolean {
        return if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            context.packageManager.canRequestPackageInstalls()
        } else {
            true
        }
    }

    override fun openInstallPermissionSettings() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val intent = Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES).apply {
                data = Uri.parse("package:${context.packageName}")
                addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
            }
            context.startActivity(intent)
        }
    }

    override fun installApk(apkFile: File): Result<Unit> {
        return try {
            val authority = "${context.packageName}.fileprovider"
            val apkUri = FileProvider.getUriForFile(context, authority, apkFile)

            val intent = Intent(Intent.ACTION_VIEW).apply {
                setDataAndType(apkUri, "application/vnd.android.package-archive")
                addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
                addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
            }
            context.startActivity(intent)
            Result.success(Unit)
        } catch (e: Exception) {
            Result.failure(e)
        }
    }

    private fun isNewerVersion(remoteTag: String, currentVersion: String): Boolean {
        val cleanRemote = remoteTag.removePrefix("android-").removePrefix("v").trim()
        val cleanCurrent = currentVersion.removePrefix("android-").removePrefix("v").trim()

        val remoteParts = cleanRemote.split(".").mapNotNull { it.toIntOrNull() }
        val currentParts = cleanCurrent.split(".").mapNotNull { it.toIntOrNull() }

        if (remoteParts.isEmpty() || currentParts.isEmpty()) {
            return cleanRemote != cleanCurrent && cleanRemote.isNotBlank()
        }

        val maxLen = maxOf(remoteParts.size, currentParts.size)
        for (i in 0 until maxLen) {
            val r = remoteParts.getOrElse(i) { 0 }
            val c = currentParts.getOrElse(i) { 0 }
            if (r > c) return true
            if (r < c) return false
        }
        return false
    }
}
