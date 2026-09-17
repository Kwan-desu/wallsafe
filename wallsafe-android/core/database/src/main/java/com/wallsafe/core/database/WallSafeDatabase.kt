package com.wallsafe.core.database

import androidx.room.Database
import androidx.room.RoomDatabase
import com.wallsafe.core.database.dao.CollectionsDao
import com.wallsafe.core.database.dao.DownloadsDao
import com.wallsafe.core.database.dao.FavoritesDao
import com.wallsafe.core.database.dao.HistoryDao
import com.wallsafe.core.database.entity.CollectionEntity
import com.wallsafe.core.database.entity.CustomSourceEntity
import com.wallsafe.core.database.entity.DownloadEntity
import com.wallsafe.core.database.entity.FavoriteEntity
import com.wallsafe.core.database.entity.HistoryEntity

@Database(
    entities = [
        FavoriteEntity::class,
        CollectionEntity::class,
        HistoryEntity::class,
        DownloadEntity::class,
        CustomSourceEntity::class
    ],
    version = 1,
    exportSchema = true
)
abstract class WallSafeDatabase : RoomDatabase() {
    abstract fun favoritesDao(): FavoritesDao
    abstract fun collectionsDao(): CollectionsDao
    abstract fun historyDao(): HistoryDao
    abstract fun downloadsDao(): DownloadsDao
}
