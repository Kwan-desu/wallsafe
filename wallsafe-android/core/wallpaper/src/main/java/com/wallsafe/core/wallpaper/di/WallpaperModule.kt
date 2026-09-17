package com.wallsafe.core.wallpaper.di

import com.wallsafe.core.wallpaper.WallpaperEngine
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
object WallpaperModule {

    @Provides
    @Singleton
    fun provideWallpaperEngine(): WallpaperEngine {
        return WallpaperEngine()
    }
}
