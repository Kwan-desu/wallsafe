package com.wallsafe.core.database.dao

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import com.wallsafe.core.database.entity.HistoryEntity
import kotlinx.coroutines.flow.Flow

@Dao
interface HistoryDao {
    @Query("SELECT * FROM history ORDER BY appliedAt DESC")
    fun getAll(): Flow<List<HistoryEntity>>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insert(entity: HistoryEntity)

    @Query("DELETE FROM history")
    suspend fun clearAll()

    @Query("SELECT COUNT(*) FROM history")
    fun getCount(): Flow<Int>

    @Query("DELETE FROM history WHERE id NOT IN (SELECT id FROM history ORDER BY appliedAt DESC LIMIT :maxSize)")
    suspend fun trimToSize(maxSize: Int = 200)
}
