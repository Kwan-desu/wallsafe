package com.wallsafe.core.data.repository

import com.wallsafe.core.database.dao.HistoryDao
import com.wallsafe.core.database.entity.HistoryEntity
import com.wallsafe.core.model.PostItem
import kotlinx.coroutines.flow.Flow
import javax.inject.Inject
import javax.inject.Singleton

interface HistoryRepository {
    suspend fun recordApplied(post: PostItem, appliedPath: String)
    fun getHistory(): Flow<List<HistoryEntity>>
    suspend fun clearHistory()
}

@Singleton
class HistoryRepositoryImpl @Inject constructor(
    private val historyDao: HistoryDao
) : HistoryRepository {

    override suspend fun recordApplied(post: PostItem, appliedPath: String) {
        val entity = HistoryEntity(
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
            appliedPath = appliedPath,
            appliedAt = System.currentTimeMillis()
        )
        historyDao.insert(entity)
        historyDao.trimToSize()
    }

    override fun getHistory(): Flow<List<HistoryEntity>> = historyDao.getAll()

    override suspend fun clearHistory() {
        historyDao.clearAll()
    }
}
