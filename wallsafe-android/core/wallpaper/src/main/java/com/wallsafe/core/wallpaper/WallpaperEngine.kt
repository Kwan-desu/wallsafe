package com.wallsafe.core.wallpaper

import android.app.WallpaperManager
import android.content.Context
import android.graphics.Bitmap
import android.graphics.drawable.Drawable
import android.net.Uri
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.net.HttpURLConnection
import java.net.URL
import javax.inject.Inject
import javax.inject.Singleton

import android.graphics.BitmapFactory
import java.io.File
import java.io.FileOutputStream

@Singleton
class WallpaperEngine @Inject constructor() {

    companion object {
        const val LAST_APPLIED_WALLPAPER_FILE = "last_applied_wallpaper.png"
    }

    suspend fun applyWallpaper(context: Context, imageUrl: String, flags: Int = WallpaperManager.FLAG_SYSTEM): Boolean = withContext(Dispatchers.IO) {
        try {
            val url = URL(imageUrl)
            val connection = url.openConnection() as HttpURLConnection
            connection.setRequestProperty("User-Agent", "WallSafe/1.0 (Android)")
            connection.connectTimeout = 15000
            connection.readTimeout = 30000
            connection.connect()
            val bytes = connection.inputStream.use { it.readBytes() }
            val bitmap = BitmapFactory.decodeByteArray(bytes, 0, bytes.size)
            if (bitmap != null) {
                saveWallpaperBackup(context, bitmap)
                val wallpaperManager = WallpaperManager.getInstance(context)
                wallpaperManager.setBitmap(bitmap, null, true, flags)
                true
            } else {
                false
            }
        } catch (e: Exception) {
            e.printStackTrace()
            false
        }
    }

    suspend fun applyWallpaperFromUri(context: Context, uri: Uri, flags: Int = WallpaperManager.FLAG_SYSTEM, saveAsBackup: Boolean = true): Boolean = withContext(Dispatchers.IO) {
        try {
            val inputStream = context.contentResolver.openInputStream(uri)
            if (inputStream != null) {
                val bitmap = inputStream.use { BitmapFactory.decodeStream(it) }
                if (bitmap != null) {
                    if (saveAsBackup) {
                        saveWallpaperBackup(context, bitmap)
                    }
                    val wallpaperManager = WallpaperManager.getInstance(context)
                    wallpaperManager.setBitmap(bitmap, null, true, flags)
                    true
                } else false
            } else {
                false
            }
        } catch (e: Exception) {
            e.printStackTrace()
            false
        }
    }

    suspend fun applyWallpaperFromBitmap(context: Context, bitmap: Bitmap, flags: Int = WallpaperManager.FLAG_SYSTEM, saveAsBackup: Boolean = false): Boolean = withContext(Dispatchers.IO) {
        try {
            if (saveAsBackup) {
                saveWallpaperBackup(context, bitmap)
            }
            val wallpaperManager = WallpaperManager.getInstance(context)
            wallpaperManager.setBitmap(bitmap, null, true, flags)
            true
        } catch (e: Exception) {
            e.printStackTrace()
            false
        }
    }

    fun saveWallpaperBackup(context: Context, bitmap: Bitmap, filename: String = LAST_APPLIED_WALLPAPER_FILE) {
        try {
            val file = File(context.filesDir, filename)
            FileOutputStream(file).use { out ->
                bitmap.compress(Bitmap.CompressFormat.PNG, 95, out)
            }
        } catch (e: Exception) {
            e.printStackTrace()
        }
    }

    fun getCurrentWallpaperDrawable(context: Context): Drawable? {
        val wallpaperManager = WallpaperManager.getInstance(context)
        return try {
            wallpaperManager.drawable
        } catch (e: SecurityException) {
            null
        }
    }

    suspend fun prepareWallpaperUriForSystem(context: Context, imageUrl: String): Uri? = withContext(Dispatchers.IO) {
        try {
            val url = URL(imageUrl)
            val connection = url.openConnection() as HttpURLConnection
            connection.setRequestProperty("User-Agent", "WallSafe/1.0 (Android)")
            connection.connectTimeout = 15000
            connection.readTimeout = 30000
            connection.connect()
            val bytes = connection.inputStream.use { it.readBytes() }
            val wallpaperDir = File(context.cacheDir, "wallpaper_system").apply { mkdirs() }
            val extension = imageUrl.substringAfterLast('.', "jpg").substringBefore('?')
            val file = File(wallpaperDir, "wallpaper_to_set.$extension")
            FileOutputStream(file).use { it.write(bytes) }
            val authority = "${context.packageName}.fileprovider"
            androidx.core.content.FileProvider.getUriForFile(context, authority, file)
        } catch (e: Exception) {
            e.printStackTrace()
            null
        }
    }

    fun openSystemWallpaperChooser(context: Context, imageUri: Uri): Boolean {
        return try {
            val wm = WallpaperManager.getInstance(context)
            val cropIntent = try {
                wm.getCropAndSetWallpaperIntent(imageUri)
            } catch (e: Exception) {
                null
            }

            val attachIntent = android.content.Intent(android.content.Intent.ACTION_ATTACH_DATA).apply {
                addCategory(android.content.Intent.CATEGORY_DEFAULT)
                setDataAndType(imageUri, "image/*")
                putExtra("mimeType", "image/*")
                addFlags(android.content.Intent.FLAG_GRANT_READ_URI_PERMISSION)
                addFlags(android.content.Intent.FLAG_ACTIVITY_NEW_TASK)
            }

            val targetIntent = cropIntent ?: attachIntent
            targetIntent.addFlags(android.content.Intent.FLAG_GRANT_READ_URI_PERMISSION)

            val resInfoList = context.packageManager.queryIntentActivities(
                targetIntent,
                android.content.pm.PackageManager.MATCH_DEFAULT_ONLY
            )
            for (resolveInfo in resInfoList) {
                val packageName = resolveInfo.activityInfo.packageName
                try {
                    context.grantUriPermission(packageName, imageUri, android.content.Intent.FLAG_GRANT_READ_URI_PERMISSION)
                } catch (e: Exception) {
                    // Ignore per-package grant failures
                }
            }

            val chooser = android.content.Intent.createChooser(targetIntent, "Set as Wallpaper").apply {
                addFlags(android.content.Intent.FLAG_GRANT_READ_URI_PERMISSION)
                addFlags(android.content.Intent.FLAG_ACTIVITY_NEW_TASK)
            }
            context.startActivity(chooser)
            true
        } catch (e: Exception) {
            e.printStackTrace()
            false
        }
    }
}
