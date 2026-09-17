package com.wallsafe.core.panic

import android.app.WallpaperManager
import android.content.Context
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.graphics.Canvas
import android.graphics.LinearGradient
import android.graphics.Paint
import android.graphics.Shader
import android.graphics.drawable.BitmapDrawable
import android.net.Uri
import com.wallsafe.core.datastore.WallSafePreferences
import com.wallsafe.core.wallpaper.WallpaperEngine
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.withContext
import java.io.File
import java.io.FileOutputStream
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class PanicManager @Inject constructor(
    private val wallpaperEngine: WallpaperEngine,
    private val preferences: WallSafePreferences
) {
    private var isPanicActiveMemory: Boolean = false

    companion object {
        const val PRE_PANIC_BACKUP_FILENAME = "pre_panic_wallpaper_backup.png"
        const val CUSTOM_PANIC_WALLPAPER_FILENAME = "custom_panic_wallpaper.png"
        const val CUSTOM_NORMAL_WALLPAPER_FILENAME = "custom_normal_wallpaper.png"
    }

    suspend fun triggerPanic(
        context: Context,
        customWallpaperUri: String? = null
    ): Boolean = withContext(Dispatchers.IO) {
        try {
            val isAlreadyActive = preferences.isPanicActive.first() || isPanicActiveMemory

            // 1. Back up current wallpaper if not already active in panic mode
            if (!isAlreadyActive) {
                backupCurrentWallpaper(context)
            }

            // 2. Apply Custom SFW Wallpaper if provided
            var applied = false
            val panicUriToUse = customWallpaperUri ?: preferences.panicWallpaperUri.first()
            if (panicUriToUse.isNotBlank()) {
                try {
                    val uri = Uri.parse(panicUriToUse)
                    applied = wallpaperEngine.applyWallpaperFromUri(
                        context,
                        uri,
                        WallpaperManager.FLAG_SYSTEM or WallpaperManager.FLAG_LOCK,
                        saveAsBackup = false
                    )
                } catch (e: Exception) {
                    e.printStackTrace()
                    applied = false
                }
            }

            // 3. Fallback: Generate sleek dark minimalist SFW gradient wallpaper
            if (!applied) {
                val panicBitmap = generateDefaultPanicBitmap()
                applied = wallpaperEngine.applyWallpaperFromBitmap(
                    context,
                    panicBitmap,
                    WallpaperManager.FLAG_SYSTEM or WallpaperManager.FLAG_LOCK,
                    saveAsBackup = false
                )
            }

            if (applied) {
                isPanicActiveMemory = true
                preferences.setIsPanicActive(true)
            }
            applied
        } catch (e: Exception) {
            e.printStackTrace()
            false
        }
    }

    suspend fun restorePanic(context: Context): Boolean = withContext(Dispatchers.IO) {
        try {
            var bitmapToRestore: Bitmap? = null

            // 1. Check pre-panic backup file
            val backupFile = File(context.filesDir, PRE_PANIC_BACKUP_FILENAME)
            if (backupFile.exists() && backupFile.length() > 0) {
                bitmapToRestore = BitmapFactory.decodeFile(backupFile.absolutePath)
            }

            // 2. Check custom user-selected normal wallpaper file
            if (bitmapToRestore == null) {
                val customNormalFile = File(context.filesDir, CUSTOM_NORMAL_WALLPAPER_FILENAME)
                if (customNormalFile.exists() && customNormalFile.length() > 0) {
                    bitmapToRestore = BitmapFactory.decodeFile(customNormalFile.absolutePath)
                }
            }

            // 3. Check custom normal wallpaper URI from preferences
            if (bitmapToRestore == null) {
                val normalUriStr = preferences.normalWallpaperUri.first()
                if (normalUriStr.isNotBlank()) {
                    try {
                        context.contentResolver.openInputStream(Uri.parse(normalUriStr))?.use { input ->
                            bitmapToRestore = BitmapFactory.decodeStream(input)
                        }
                    } catch (e: Exception) {
                        e.printStackTrace()
                    }
                }
            }

            // 4. Check last applied wallpaper from WallSafe
            if (bitmapToRestore == null) {
                val lastAppliedFile = File(context.filesDir, WallpaperEngine.LAST_APPLIED_WALLPAPER_FILE)
                if (lastAppliedFile.exists() && lastAppliedFile.length() > 0) {
                    bitmapToRestore = BitmapFactory.decodeFile(lastAppliedFile.absolutePath)
                }
            }

            val finalBitmap = bitmapToRestore
            if (finalBitmap != null) {
                val restored = wallpaperEngine.applyWallpaperFromBitmap(
                    context,
                    finalBitmap,
                    WallpaperManager.FLAG_SYSTEM or WallpaperManager.FLAG_LOCK,
                    saveAsBackup = true
                )
                if (restored) {
                    isPanicActiveMemory = false
                    preferences.setIsPanicActive(false)
                    return@withContext true
                }
            }

            isPanicActiveMemory = false
            preferences.setIsPanicActive(false)
            false
        } catch (e: Exception) {
            e.printStackTrace()
            isPanicActiveMemory = false
            preferences.setIsPanicActive(false)
            false
        }
    }

    suspend fun isPanicActive(): Boolean {
        return preferences.isPanicActive.first() || isPanicActiveMemory
    }

    fun isPanicActiveSync(): Boolean = isPanicActiveMemory

    suspend fun backupCurrentWallpaper(context: Context) = withContext(Dispatchers.IO) {
        try {
            val backupFile = File(context.filesDir, PRE_PANIC_BACKUP_FILENAME)

            // 1. Try system WallpaperManager.drawable
            val drawable = wallpaperEngine.getCurrentWallpaperDrawable(context)
            if (drawable != null) {
                val bitmap = if (drawable is BitmapDrawable && drawable.bitmap != null) {
                    drawable.bitmap
                } else {
                    val width = drawable.intrinsicWidth.coerceAtLeast(1080)
                    val height = drawable.intrinsicHeight.coerceAtLeast(1920)
                    val bmp = Bitmap.createBitmap(width, height, Bitmap.Config.ARGB_8888)
                    val canvas = Canvas(bmp)
                    drawable.setBounds(0, 0, canvas.width, canvas.height)
                    drawable.draw(canvas)
                    bmp
                }
                FileOutputStream(backupFile).use { out ->
                    bitmap.compress(Bitmap.CompressFormat.PNG, 95, out)
                }
                return@withContext
            }

            // 2. Check if custom normal wallpaper is configured
            val customNormalFile = File(context.filesDir, CUSTOM_NORMAL_WALLPAPER_FILENAME)
            if (customNormalFile.exists() && customNormalFile.length() > 0) {
                customNormalFile.copyTo(backupFile, overwrite = true)
                return@withContext
            }

            // 3. Fallback to last wallpaper applied via WallSafe
            val lastApplied = File(context.filesDir, WallpaperEngine.LAST_APPLIED_WALLPAPER_FILE)
            if (lastApplied.exists() && lastApplied.length() > 0) {
                lastApplied.copyTo(backupFile, overwrite = true)
            }
        } catch (e: Exception) {
            e.printStackTrace()
        }
    }

    suspend fun saveCustomPanicWallpaper(context: Context, sourceUri: Uri): String = withContext(Dispatchers.IO) {
        try {
            val targetFile = File(context.filesDir, CUSTOM_PANIC_WALLPAPER_FILENAME)
            context.contentResolver.openInputStream(sourceUri)?.use { input ->
                FileOutputStream(targetFile).use { output ->
                    input.copyTo(output)
                }
            }
            val uriStr = Uri.fromFile(targetFile).toString()
            preferences.setPanicWallpaperUri(uriStr)
            uriStr
        } catch (e: Exception) {
            e.printStackTrace()
            sourceUri.toString().also {
                preferences.setPanicWallpaperUri(it)
            }
        }
    }

    suspend fun saveCustomNormalWallpaper(context: Context, sourceUri: Uri): String = withContext(Dispatchers.IO) {
        try {
            val targetFile = File(context.filesDir, CUSTOM_NORMAL_WALLPAPER_FILENAME)
            context.contentResolver.openInputStream(sourceUri)?.use { input ->
                FileOutputStream(targetFile).use { output ->
                    input.copyTo(output)
                }
            }
            val uriStr = Uri.fromFile(targetFile).toString()
            preferences.setNormalWallpaperUri(uriStr)
            // Also copy to pre_panic backup so restore target is immediately ready
            val backupFile = File(context.filesDir, PRE_PANIC_BACKUP_FILENAME)
            targetFile.copyTo(backupFile, overwrite = true)
            uriStr
        } catch (e: Exception) {
            e.printStackTrace()
            sourceUri.toString().also {
                preferences.setNormalWallpaperUri(it)
            }
        }
    }

    suspend fun captureCurrentAsNormalWallpaper(context: Context): Boolean = withContext(Dispatchers.IO) {
        try {
            val targetFile = File(context.filesDir, CUSTOM_NORMAL_WALLPAPER_FILENAME)
            val backupFile = File(context.filesDir, PRE_PANIC_BACKUP_FILENAME)

            val drawable = wallpaperEngine.getCurrentWallpaperDrawable(context)
            if (drawable != null) {
                val bitmap = if (drawable is BitmapDrawable && drawable.bitmap != null) {
                    drawable.bitmap
                } else {
                    val width = drawable.intrinsicWidth.coerceAtLeast(1080)
                    val height = drawable.intrinsicHeight.coerceAtLeast(1920)
                    val bmp = Bitmap.createBitmap(width, height, Bitmap.Config.ARGB_8888)
                    val canvas = Canvas(bmp)
                    drawable.setBounds(0, 0, canvas.width, canvas.height)
                    drawable.draw(canvas)
                    bmp
                }
                FileOutputStream(targetFile).use { out ->
                    bitmap.compress(Bitmap.CompressFormat.PNG, 95, out)
                }
                targetFile.copyTo(backupFile, overwrite = true)
                preferences.setNormalWallpaperUri(Uri.fromFile(targetFile).toString())
                return@withContext true
            }

            val lastApplied = File(context.filesDir, WallpaperEngine.LAST_APPLIED_WALLPAPER_FILE)
            if (lastApplied.exists() && lastApplied.length() > 0) {
                lastApplied.copyTo(targetFile, overwrite = true)
                lastApplied.copyTo(backupFile, overwrite = true)
                preferences.setNormalWallpaperUri(Uri.fromFile(targetFile).toString())
                return@withContext true
            }

            false
        } catch (e: Exception) {
            e.printStackTrace()
            false
        }
    }

    fun getNormalWallpaperPreviewFile(context: Context): File? {
        val normalFile = File(context.filesDir, CUSTOM_NORMAL_WALLPAPER_FILENAME)
        if (normalFile.exists() && normalFile.length() > 0) return normalFile
        val backupFile = File(context.filesDir, PRE_PANIC_BACKUP_FILENAME)
        if (backupFile.exists() && backupFile.length() > 0) return backupFile
        val lastApplied = File(context.filesDir, WallpaperEngine.LAST_APPLIED_WALLPAPER_FILE)
        if (lastApplied.exists() && lastApplied.length() > 0) return lastApplied
        return null
    }

    fun generateDefaultPanicBitmap(width: Int = 1080, height: Int = 2400): Bitmap {
        val bitmap = Bitmap.createBitmap(width, height, Bitmap.Config.ARGB_8888)
        val canvas = Canvas(bitmap)
        val paint = Paint(Paint.ANTI_ALIAS_FLAG)

        // Elegant deep dark slate & charcoal gradient
        val shader = LinearGradient(
            0f, 0f, width.toFloat(), height.toFloat(),
            intArrayOf(
                android.graphics.Color.rgb(18, 20, 26),
                android.graphics.Color.rgb(30, 36, 48),
                android.graphics.Color.rgb(14, 16, 20)
            ),
            floatArrayOf(0f, 0.5f, 1f),
            Shader.TileMode.CLAMP
        )
        paint.shader = shader
        canvas.drawRect(0f, 0f, width.toFloat(), height.toFloat(), paint)

        // Subtle geometric minimalist lines & concentric circles
        val accentPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
            style = Paint.Style.STROKE
            strokeWidth = 2f
            color = android.graphics.Color.argb(25, 255, 255, 255)
        }
        val cx = width / 2f
        val cy = height / 2f
        canvas.drawCircle(cx, cy, width * 0.25f, accentPaint)
        canvas.drawCircle(cx, cy, width * 0.40f, accentPaint)
        canvas.drawCircle(cx, cy, width * 0.60f, accentPaint)

        return bitmap
    }
}
