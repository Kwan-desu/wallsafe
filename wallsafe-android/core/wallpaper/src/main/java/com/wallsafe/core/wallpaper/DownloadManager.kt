package com.wallsafe.core.wallpaper

import android.content.ContentValues
import android.content.Context
import android.net.Uri
import android.os.Build
import android.os.Environment
import android.provider.MediaStore
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.OkHttpClient
import okhttp3.Request
import java.io.File
import java.io.FileOutputStream
import java.io.InputStream
import java.io.OutputStream
import javax.inject.Inject
import javax.inject.Singleton

import com.wallsafe.core.model.PostItem

@Singleton
class DownloadManager @Inject constructor(private val okHttpClient: OkHttpClient) {

    suspend fun downloadWallpaper(context: Context, post: PostItem, onProgress: (Float) -> Unit): Uri? = withContext(Dispatchers.IO) {
        val downloadUrl = post.fileUrl ?: post.sampleUrl ?: post.previewUrl
        val request = Request.Builder().url(downloadUrl).build()
        val extension = downloadUrl.substringAfterLast('.', "jpg")
        val fileName = "WallSafe_${post.source}_${post.id}_${post.width}x${post.height}.$extension"
        
        try {
            val response = okHttpClient.newCall(request).execute()
            if (!response.isSuccessful) return@withContext null
            
            val body = response.body ?: return@withContext null
            val contentLength = body.contentLength()
            var bytesReadTotal = 0L
            
            val mimeType = when (extension.lowercase()) {
                "png" -> "image/png"
                "webp" -> "image/webp"
                "gif" -> "image/gif"
                else -> "image/jpeg"
            }

            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
                val resolver = context.contentResolver
                val contentValues = ContentValues().apply {
                    put(MediaStore.MediaColumns.DISPLAY_NAME, fileName)
                    put(MediaStore.MediaColumns.MIME_TYPE, mimeType)
                    put(MediaStore.MediaColumns.RELATIVE_PATH, Environment.DIRECTORY_PICTURES + "/WallSafe")
                    put(MediaStore.Images.Media.IS_PENDING, 1)
                }
                val uri = resolver.insert(MediaStore.Images.Media.EXTERNAL_CONTENT_URI, contentValues) ?: return@withContext null
                
                resolver.openOutputStream(uri)?.use { outputStream ->
                    body.byteStream().use { inputStream ->
                        copyStream(inputStream, outputStream, contentLength) { progress ->
                            onProgress(progress)
                        }
                    }
                }

                // Mark download as completed so Gallery and other apps immediately see it as ready (not pending)
                val completedValues = ContentValues().apply {
                    put(MediaStore.Images.Media.IS_PENDING, 0)
                }
                resolver.update(uri, completedValues, null, null)

                return@withContext uri
            } else {
                val dir = File(Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_PICTURES), "WallSafe")
                if (!dir.exists()) dir.mkdirs()
                
                val file = File(dir, fileName)
                val tempFile = File(dir, "$fileName.download")
                
                FileOutputStream(tempFile).use { outputStream ->
                    body.byteStream().use { inputStream ->
                        copyStream(inputStream, outputStream, contentLength) { progress ->
                            onProgress(progress)
                        }
                    }
                }
                tempFile.renameTo(file)
                android.media.MediaScannerConnection.scanFile(
                    context,
                    arrayOf(file.absolutePath),
                    arrayOf(mimeType),
                    null
                )
                return@withContext Uri.fromFile(file)
            }
        } catch (e: Exception) {
            e.printStackTrace()
            null
        }
    }

    private fun copyStream(inputStream: InputStream, outputStream: OutputStream, contentLength: Long, onProgress: (Float) -> Unit) {
        val buffer = ByteArray(16 * 1024)
        var bytesRead: Int
        var totalBytesRead = 0L
        
        while (inputStream.read(buffer).also { bytesRead = it } != -1) {
            outputStream.write(buffer, 0, bytesRead)
            totalBytesRead += bytesRead
            if (contentLength > 0) {
                onProgress(totalBytesRead.toFloat() / contentLength.toFloat())
            }
        }
        outputStream.flush()
    }
}
