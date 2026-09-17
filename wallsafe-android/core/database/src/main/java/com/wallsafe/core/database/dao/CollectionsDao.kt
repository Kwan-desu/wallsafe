package com.wallsafe.core.database.dao

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import com.wallsafe.core.database.entity.CollectionEntity
import kotlinx.coroutines.flow.Flow

data class CollectionWithCount(
    val id: Int,
    val name: String,
    val orderIndex: Int,
    val createdAt: Long,
    val favoriteCount: Int
)

@Dao
interface CollectionsDao {
    @Query("SELECT * FROM collections ORDER BY orderIndex ASC")
    fun getAll(): Flow<List<CollectionEntity>>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insert(entity: CollectionEntity)

    @Query("DELETE FROM collections WHERE id = :id")
    suspend fun delete(id: Int)

    @Query("UPDATE collections SET name = :newName WHERE id = :id")
    suspend fun rename(id: Int, newName: String)

    @Query("UPDATE collections SET orderIndex = :orderIndex WHERE id = :id")
    suspend fun updateOrder(id: Int, orderIndex: Int)

    @Query("""
        SELECT c.*, (SELECT COUNT(*) FROM favorites f WHERE f.collectionId = c.id) as favoriteCount
        FROM collections c
        ORDER BY c.orderIndex ASC
    """)
    fun getCollectionWithCount(): Flow<List<CollectionWithCount>>
}
