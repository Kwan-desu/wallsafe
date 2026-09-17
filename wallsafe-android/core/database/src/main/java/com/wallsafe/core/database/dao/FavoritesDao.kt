package com.wallsafe.core.database.dao

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import com.wallsafe.core.database.entity.FavoriteEntity
import kotlinx.coroutines.flow.Flow

@Dao
interface FavoritesDao {
    @Query("SELECT * FROM favorites ORDER BY favoritedAt DESC")
    fun getAllFavorites(): Flow<List<FavoriteEntity>>

    @Query("SELECT * FROM favorites ORDER BY favoritedAt DESC")
    suspend fun getAllFavoritesList(): List<FavoriteEntity>

    @Query("SELECT * FROM favorites WHERE collectionId = :collectionId ORDER BY favoritedAt DESC")
    fun getFavoritesByCollection(collectionId: Int): Flow<List<FavoriteEntity>>

    @Query("SELECT * FROM favorites WHERE collectionId IS NULL ORDER BY favoritedAt DESC")
    fun getUncategorized(): Flow<List<FavoriteEntity>>

    @Query("SELECT EXISTS(SELECT 1 FROM favorites WHERE postId = :postId AND source = :source)")
    fun isFavorite(postId: Int, source: String): Flow<Boolean>

    @Query("SELECT * FROM favorites WHERE postId = :postId AND source = :source LIMIT 1")
    suspend fun getFavoriteSync(postId: Int, source: String): FavoriteEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insert(entity: FavoriteEntity)

    @Query("DELETE FROM favorites WHERE postId = :postId AND source = :source")
    suspend fun delete(postId: Int, source: String)

    @Query("UPDATE favorites SET collectionId = :collectionId WHERE postId = :postId AND source = :source")
    suspend fun assignToCollection(postId: Int, source: String, collectionId: Int?)

    @Query("SELECT COUNT(*) FROM favorites")
    fun getCount(): Flow<Int>
}
