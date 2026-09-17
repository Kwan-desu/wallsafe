package com.wallsafe.widget

import android.app.PendingIntent
import android.app.WallpaperManager
import android.appwidget.AppWidgetManager
import android.appwidget.AppWidgetProvider
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.graphics.Canvas
import android.graphics.Paint
import android.graphics.PorterDuff
import android.graphics.PorterDuffXfermode
import android.graphics.RectF
import android.os.Build
import android.os.VibrationEffect
import android.os.Vibrator
import android.os.VibratorManager
import android.view.View
import android.widget.RemoteViews
import android.widget.Toast
import androidx.room.Room
import com.wallsafe.core.database.WallSafeDatabase
import com.wallsafe.core.database.entity.FavoriteEntity
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.io.File
import java.io.FileOutputStream
import java.net.HttpURLConnection
import java.net.URL

class FavoriteWallpaperWidgetProvider : AppWidgetProvider() {

    override fun onUpdate(
        context: Context,
        appWidgetManager: AppWidgetManager,
        appWidgetIds: IntArray
    ) {
        val pendingResult = goAsync()
        CoroutineScope(Dispatchers.IO).launch {
            try {
                loadAndRenderWidget(context, appWidgetManager, appWidgetIds)
            } finally {
                pendingResult.finish()
            }
        }
    }

    override fun onReceive(context: Context, intent: Intent) {
        super.onReceive(context, intent)
        when (intent.action) {
            ACTION_FAV_NEXT -> {
                val pendingResult = goAsync()
                CoroutineScope(Dispatchers.IO).launch {
                    try {
                        cycleNextFavorite(context)
                    } finally {
                        pendingResult.finish()
                    }
                }
            }
            ACTION_FAV_APPLY -> {
                val pendingResult = goAsync()
                CoroutineScope(Dispatchers.IO).launch {
                    try {
                        applyCurrentFavorite(context)
                    } finally {
                        pendingResult.finish()
                    }
                }
            }
        }
    }

    private suspend fun loadAndRenderWidget(
        context: Context,
        appWidgetManager: AppWidgetManager,
        appWidgetIds: IntArray
    ) = withContext(Dispatchers.IO) {
        val favorites = getFavoritesList(context)
        val prefs = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)

        if (favorites.isEmpty()) {
            withContext(Dispatchers.Main) {
                for (id in appWidgetIds) {
                    renderEmptyState(context, appWidgetManager, id)
                }
            }
            return@withContext
        }

        var index = prefs.getInt(KEY_FAV_INDEX, 0)
        if (index >= favorites.size) {
            index = 0
            prefs.edit().putInt(KEY_FAV_INDEX, 0).apply()
        }

        val favorite = favorites[index]
        val fullUrl = favorite.sampleUrl ?: favorite.fileUrl ?: favorite.previewUrl
        val title = formatTitle(favorite)
        val counterText = "Favorite ${index + 1} of ${favorites.size}"

        prefs.edit()
            .putString(KEY_CURRENT_APPLY_URL, fullUrl)
            .apply()

        // Load preview bitmap from cache or network
        val bitmap = loadOrFetchPreviewBitmap(context, favorite)

        withContext(Dispatchers.Main) {
            for (id in appWidgetIds) {
                renderContentState(
                    context = context,
                    appWidgetManager = appWidgetManager,
                    appWidgetId = id,
                    bitmap = bitmap,
                    title = title,
                    counterText = counterText
                )
            }
        }
    }

    private suspend fun cycleNextFavorite(context: Context) = withContext(Dispatchers.IO) {
        val favorites = getFavoritesList(context)
        if (favorites.isEmpty()) {
            triggerFullUpdate(context)
            return@withContext
        }

        val prefs = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
        val currentIndex = prefs.getInt(KEY_FAV_INDEX, 0)
        val nextIndex = (currentIndex + 1) % favorites.size
        prefs.edit().putInt(KEY_FAV_INDEX, nextIndex).apply()

        triggerFullUpdate(context)
    }

    private suspend fun applyCurrentFavorite(context: Context) = withContext(Dispatchers.IO) {
        val prefs = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
        val applyUrl = prefs.getString(KEY_CURRENT_APPLY_URL, null)

        if (applyUrl.isNullOrEmpty()) {
            withContext(Dispatchers.Main) {
                Toast.makeText(context, "No favorite wallpaper selected", Toast.LENGTH_SHORT).show()
            }
            return@withContext
        }

        withContext(Dispatchers.Main) {
            Toast.makeText(context, "Applying wallpaper...", Toast.LENGTH_SHORT).show()
        }

        try {
            val url = URL(applyUrl)
            val connection = url.openConnection() as HttpURLConnection
            connection.setRequestProperty("User-Agent", "WallSafe/1.0 (Android)")
            connection.connectTimeout = 15000
            connection.readTimeout = 30000
            connection.connect()

            connection.inputStream.use { stream ->
                val wallpaperManager = WallpaperManager.getInstance(context)
                wallpaperManager.setStream(stream, null, true, WallpaperManager.FLAG_SYSTEM or WallpaperManager.FLAG_LOCK)
            }

            triggerHaptic(context)

            withContext(Dispatchers.Main) {
                Toast.makeText(context, "WallSafe: Favorite Applied!", Toast.LENGTH_SHORT).show()
            }
        } catch (e: Exception) {
            e.printStackTrace()
            withContext(Dispatchers.Main) {
                Toast.makeText(context, "Failed to set wallpaper: ${e.localizedMessage}", Toast.LENGTH_SHORT).show()
            }
        }
    }

    private fun renderContentState(
        context: Context,
        appWidgetManager: AppWidgetManager,
        appWidgetId: Int,
        bitmap: Bitmap?,
        title: String,
        counterText: String
    ) {
        val views = RemoteViews(context.packageName, R.layout.widget_favorite_wallpaper)

        views.setViewVisibility(R.id.widget_fav_content_container, View.VISIBLE)
        views.setViewVisibility(R.id.widget_fav_empty_container, View.GONE)

        // Set Texts
        views.setTextViewText(R.id.widget_fav_title, title)
        views.setTextViewText(R.id.widget_fav_counter, counterText)
        views.setTextViewText(R.id.widget_fav_subtitle, "Quick Set Wallpaper")

        // Set Preview Bitmap
        if (bitmap != null) {
            views.setImageViewBitmap(R.id.widget_fav_preview, bitmap)
        } else {
            views.setImageViewResource(R.id.widget_fav_preview, R.drawable.ic_widget_star)
        }

        // 1. Next Button
        val nextIntent = Intent(context, FavoriteWallpaperWidgetProvider::class.java).apply {
            action = ACTION_FAV_NEXT
        }
        val nextPending = PendingIntent.getBroadcast(
            context,
            appWidgetId * 10 + 1,
            nextIntent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )
        views.setOnClickPendingIntent(R.id.widget_btn_fav_next, nextPending)

        // 2. Quick Apply Button
        val applyIntent = Intent(context, FavoriteWallpaperWidgetProvider::class.java).apply {
            action = ACTION_FAV_APPLY
        }
        val applyPending = PendingIntent.getBroadcast(
            context,
            appWidgetId * 10 + 2,
            applyIntent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )
        views.setOnClickPendingIntent(R.id.widget_btn_fav_apply, applyPending)

        // 3. Tap card to open App
        val launchIntent = context.packageManager.getLaunchIntentForPackage(context.packageName)
        if (launchIntent != null) {
            launchIntent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
            val launchPending = PendingIntent.getActivity(
                context,
                appWidgetId * 10 + 3,
                launchIntent,
                PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
            )
            views.setOnClickPendingIntent(R.id.widget_fav_root, launchPending)
            views.setOnClickPendingIntent(R.id.widget_fav_image_container, launchPending)
        }

        appWidgetManager.updateAppWidget(appWidgetId, views)
    }

    private fun renderEmptyState(
        context: Context,
        appWidgetManager: AppWidgetManager,
        appWidgetId: Int
    ) {
        val views = RemoteViews(context.packageName, R.layout.widget_favorite_wallpaper)

        views.setViewVisibility(R.id.widget_fav_content_container, View.GONE)
        views.setViewVisibility(R.id.widget_fav_empty_container, View.VISIBLE)

        val launchIntent = context.packageManager.getLaunchIntentForPackage(context.packageName)
        if (launchIntent != null) {
            launchIntent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
            val launchPending = PendingIntent.getActivity(
                context,
                appWidgetId * 10 + 4,
                launchIntent,
                PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
            )
            views.setOnClickPendingIntent(R.id.widget_fav_root, launchPending)
            views.setOnClickPendingIntent(R.id.widget_btn_fav_explore, launchPending)
        }

        appWidgetManager.updateAppWidget(appWidgetId, views)
    }

    private fun triggerFullUpdate(context: Context) {
        val appWidgetManager = AppWidgetManager.getInstance(context)
        val thisWidget = ComponentName(context, FavoriteWallpaperWidgetProvider::class.java)
        val ids = appWidgetManager.getAppWidgetIds(thisWidget)
        if (ids.isNotEmpty()) {
            CoroutineScope(Dispatchers.IO).launch {
                loadAndRenderWidget(context, appWidgetManager, ids)
            }
        }
    }

    private fun getFavoritesList(context: Context): List<FavoriteEntity> {
        return try {
            val db = Room.databaseBuilder(
                context.applicationContext,
                WallSafeDatabase::class.java,
                "wallsafe_database"
            ).build()
            kotlinx.coroutines.runBlocking {
                db.favoritesDao().getAllFavoritesList()
            }
        } catch (e: Exception) {
            e.printStackTrace()
            emptyList()
        }
    }

    private fun loadOrFetchPreviewBitmap(context: Context, favorite: FavoriteEntity): Bitmap? {
        val cacheKey = "fav_preview_${favorite.source}_${favorite.postId}.png"
        val cacheFile = File(context.cacheDir, cacheKey)

        if (cacheFile.exists()) {
            val bitmap = BitmapFactory.decodeFile(cacheFile.absolutePath)
            if (bitmap != null) return roundCorners(bitmap, 28f)
        }

        // Fetch preview from network
        return try {
            val url = URL(favorite.previewUrl)
            val conn = url.openConnection() as HttpURLConnection
            conn.setRequestProperty("User-Agent", "WallSafe/1.0 (Android)")
            conn.connectTimeout = 8000
            conn.readTimeout = 8000
            conn.connect()

            val options = BitmapFactory.Options().apply {
                inPreferredConfig = Bitmap.Config.RGB_565
            }
            val original = conn.inputStream.use { BitmapFactory.decodeStream(it, null, options) }
            if (original != null) {
                val maxDim = 380
                val scale = (maxDim.toFloat() / maxOf(original.width, original.height)).coerceAtMost(1f)
                val scaled = if (scale < 1f) {
                    Bitmap.createScaledBitmap(original, (original.width * scale).toInt(), (original.height * scale).toInt(), true)
                } else {
                    original
                }
                // Save to cache
                try {
                    FileOutputStream(cacheFile).use { out ->
                        scaled.compress(Bitmap.CompressFormat.PNG, 90, out)
                    }
                } catch (e: Exception) {
                    e.printStackTrace()
                }
                roundCorners(scaled, 28f)
            } else {
                null
            }
        } catch (e: Exception) {
            e.printStackTrace()
            null
        }
    }

    private fun roundCorners(bitmap: Bitmap, cornerRadiusPx: Float): Bitmap {
        val output = Bitmap.createBitmap(bitmap.width, bitmap.height, Bitmap.Config.ARGB_8888)
        val canvas = Canvas(output)
        val paint = Paint(Paint.ANTI_ALIAS_FLAG)
        val rect = RectF(0f, 0f, bitmap.width.toFloat(), bitmap.height.toFloat())
        canvas.drawRoundRect(rect, cornerRadiusPx, cornerRadiusPx, paint)
        paint.xfermode = PorterDuffXfermode(PorterDuff.Mode.SRC_IN)
        canvas.drawBitmap(bitmap, 0f, 0f, paint)
        return output
    }

    private fun formatTitle(favorite: FavoriteEntity): String {
        val tags = favorite.tags.split(" ").filter { it.isNotBlank() }
        val author = favorite.author?.takeIf { it.isNotBlank() }
        return when {
            author != null -> author
            tags.isNotEmpty() -> tags.take(2).joinToString(" ")
            else -> "Favorite Wallpaper"
        }
    }

    private fun triggerHaptic(context: Context) {
        val vibrator = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
            val manager = context.getSystemService(Context.VIBRATOR_MANAGER_SERVICE) as? VibratorManager
            manager?.defaultVibrator
        } else {
            @Suppress("DEPRECATION")
            context.getSystemService(Context.VIBRATOR_SERVICE) as? Vibrator
        }
        vibrator?.let {
            if (it.hasVibrator()) {
                val effect = VibrationEffect.createOneShot(100, VibrationEffect.DEFAULT_AMPLITUDE)
                it.vibrate(effect)
            }
        }
    }

    companion object {
        const val ACTION_FAV_NEXT = "com.wallsafe.widget.action.FAV_NEXT"
        const val ACTION_FAV_APPLY = "com.wallsafe.widget.action.FAV_APPLY"

        const val PREFS_NAME = "wallsafe_favorite_widget_prefs"
        const val KEY_FAV_INDEX = "fav_index"
        const val KEY_CURRENT_APPLY_URL = "current_apply_url"

        fun refreshWidgets(context: Context) {
            val intent = Intent(context, FavoriteWallpaperWidgetProvider::class.java).apply {
                action = AppWidgetManager.ACTION_APPWIDGET_UPDATE
                val manager = AppWidgetManager.getInstance(context)
                val ids = manager.getAppWidgetIds(ComponentName(context, FavoriteWallpaperWidgetProvider::class.java))
                putExtra(AppWidgetManager.EXTRA_APPWIDGET_IDS, ids)
            }
            context.sendBroadcast(intent)
        }
    }
}
