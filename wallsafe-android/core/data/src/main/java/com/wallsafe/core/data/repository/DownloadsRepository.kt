package com.wallsafe.core.data.repository

import com.wallsafe.core.database.dao.DownloadsDao
import com.wallsafe.core.database.entity.DownloadEntity
import com.wallsafe.core.model.PostItem
import kotlinx.coroutines.flow.Flow
import javax.inject.Inject
import javax.inject.Singleton

interface DownloadsRepository {
    fun isDownloaded(postId: Int, source: String): Flow<Boolean>
    suspend fun recordDownload(post: PostItem, localUri: String)
    fun getDownloads(): Flow<List<DownloadEntity>>
    suspend fun deleteDownload(postId: Int, source: String)
}

@Singleton
class DownloadsRepositoryImpl @Inject constructor(
    private val downloadsDao: DownloadsDao
) : DownloadsRepository {

    override fun isDownloaded(postId: Int, source: String): Flow<Boolean> = downloadsDao.isDownloaded(postId, source)

    override suspend fun recordDownload(post: PostItem, localUri: String) {
        val entity = DownloadEntity(
            postId = post.id,
            source = post.source,
            previewUrl = post.previewUrl,
            sampleUrl = post.sampleUrl,
            fileUrl = post.fileUrl,
            width = post.width,
            height = post.height,
            rating = post.rating,
            score = post.score,
            tags = post.tags,
            localUri = localUri,
            downloadedAt = System.currentTimeMillis()
        )
        downloadsDao.insert(entity)
    }

    override fun getDownloads(): Flow<List<DownloadEntity>> = downloadsDao.getAll()

    override suspend fun deleteDownload(postId: Int, source: String) {
        downloadsDao.delete(postId, source)
    }
}
