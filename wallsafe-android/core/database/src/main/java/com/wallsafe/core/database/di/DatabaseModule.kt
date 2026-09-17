package com.wallsafe.core.database.di

import android.content.Context
import androidx.room.Room
import com.wallsafe.core.database.WallSafeDatabase
import com.wallsafe.core.database.dao.CollectionsDao
import com.wallsafe.core.database.dao.DownloadsDao
import com.wallsafe.core.database.dao.FavoritesDao
import com.wallsafe.core.database.dao.HistoryDao
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
object DatabaseModule {

    @Provides
    @Singleton
    fun provideWallSafeDatabase(
        @ApplicationContext context: Context
    ): WallSafeDatabase {
        return Room.databaseBuilder(
            context,
            WallSafeDatabase::class.java,
            "wallsafe_database"
        ).build()
    }

    @Provides
    fun provideFavoritesDao(database: WallSafeDatabase): FavoritesDao = database.favoritesDao()

    @Provides
    fun provideCollectionsDao(database: WallSafeDatabase): CollectionsDao = database.collectionsDao()

    @Provides
    fun provideHistoryDao(database: WallSafeDatabase): HistoryDao = database.historyDao()

    @Provides
    fun provideDownloadsDao(database: WallSafeDatabase): DownloadsDao = database.downloadsDao()
}
