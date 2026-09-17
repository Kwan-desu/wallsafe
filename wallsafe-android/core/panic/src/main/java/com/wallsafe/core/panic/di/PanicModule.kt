package com.wallsafe.core.panic.di

import com.wallsafe.core.datastore.WallSafePreferences
import com.wallsafe.core.panic.PanicManager
import com.wallsafe.core.wallpaper.WallpaperEngine
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
object PanicModule {

    @Provides
    @Singleton
    fun providePanicManager(
        wallpaperEngine: WallpaperEngine,
        preferences: WallSafePreferences
    ): PanicManager {
        return PanicManager(wallpaperEngine, preferences)
    }
}
