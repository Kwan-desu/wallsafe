package com.wallsafe.core.data.repository

import android.content.Context
import android.content.Intent
import com.wallsafe.core.database.dao.CollectionsDao
import com.wallsafe.core.database.dao.CollectionWithCount
import com.wallsafe.core.database.dao.FavoritesDao
import com.wallsafe.core.database.entity.CollectionEntity
import com.wallsafe.core.database.entity.FavoriteEntity
import com.wallsafe.core.model.PostItem
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import javax.inject.Inject
import javax.inject.Singleton

interface FavoritesRepository {
    suspend fun toggleFavorite(post: PostItem)
    fun getAllFavorites(): Flow<List<FavoriteEntity>>
    fun getFavoriteKeys(): Flow<Set<String>>
    fun getFavoritesByCollection(id: Int): Flow<List<FavoriteEntity>>
    suspend fun createCollection(name: String)
    suspend fun deleteCollection(id: Int)
    suspend fun renameCollection(id: Int, name: String)
    fun getCollectionsWithCount(): Flow<List<CollectionWithCount>>
    suspend fun assignToCollection(postId: Int, source: String, collectionId: Int?)
}

@Singleton
class FavoritesRepositoryImpl @Inject constructor(
    @ApplicationContext private val context: Context,
    private val favoritesDao: FavoritesDao,
    private val collectionsDao: CollectionsDao
) : FavoritesRepository {

    override suspend fun toggleFavorite(post: PostItem) {
        val existing = favoritesDao.getFavoriteSync(post.id, post.source)
        if (existing != null) {
            favoritesDao.delete(post.id, post.source)
        } else {
            favoritesDao.insert(
                FavoriteEntity(
                    postId = post.id,
                    source = post.source,
                    previewUrl = post.previewUrl ?: post.fileUrl ?: "",
                    sampleUrl = post.sampleUrl,
                    fileUrl = post.fileUrl,
                    width = post.width,
                    height = post.height,
                    rating = post.rating,
                    score = post.score,
                    tags = post.tags,
                    author = post.author,
                    sourceUrl = post.sourceUrl,
                    createdAt = post.createdAt,
                    collectionId = null,
                    favoritedAt = System.currentTimeMillis()
                )
            )
        }
        try {
            val intent = Intent("android.appwidget.action.APPWIDGET_UPDATE").apply {
                setPackage(context.packageName)
            }
            context.sendBroadcast(intent)
        } catch (e: Exception) {
            e.printStackTrace()
        }
    }

    override fun getAllFavorites(): Flow<List<FavoriteEntity>> = favoritesDao.getAllFavorites()

    override fun getFavoriteKeys(): Flow<Set<String>> =
        favoritesDao.getAllFavorites().map { list ->
            list.map { "${it.source}_${it.postId}" }.toSet()
        }

    override fun getFavoritesByCollection(id: Int): Flow<List<FavoriteEntity>> = favoritesDao.getFavoritesByCollection(id)

    override suspend fun createCollection(name: String) {
        collectionsDao.insert(CollectionEntity(name = name, orderIndex = 0, createdAt = System.currentTimeMillis()))
    }

    override suspend fun deleteCollection(id: Int) {
        collectionsDao.delete(id)
    }

    override suspend fun renameCollection(id: Int, name: String) {
        collectionsDao.rename(id, name)
    }

    override fun getCollectionsWithCount(): Flow<List<CollectionWithCount>> = collectionsDao.getCollectionWithCount()

    override suspend fun assignToCollection(postId: Int, source: String, collectionId: Int?) {
        favoritesDao.assignToCollection(postId, source, collectionId)
    }
}
