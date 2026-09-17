package com.wallsafe.core.database.dao

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import com.wallsafe.core.database.entity.DownloadEntity
import kotlinx.coroutines.flow.Flow

@Dao
interface DownloadsDao {
    @Query("SELECT * FROM downloads ORDER BY downloadedAt DESC")
    fun getAll(): Flow<List<DownloadEntity>>

    @Query("SELECT EXISTS(SELECT 1 FROM downloads WHERE postId = :postId AND source = :source)")
    fun isDownloaded(postId: Int, source: String): Flow<Boolean>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insert(entity: DownloadEntity)

    @Query("DELETE FROM downloads WHERE postId = :postId AND source = :source")
    suspend fun delete(postId: Int, source: String)

    @Query("SELECT * FROM downloads WHERE postId = :postId AND source = :source LIMIT 1")
    suspend fun getByPostIdAndSource(postId: Int, source: String): DownloadEntity?
}
