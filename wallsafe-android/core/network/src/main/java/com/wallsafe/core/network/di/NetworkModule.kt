package com.wallsafe.core.network.di

import android.content.Context
import com.jakewharton.retrofit2.converter.kotlinx.serialization.asConverterFactory
import com.wallsafe.core.network.BooruApiServiceFactory
import com.wallsafe.core.network.interceptor.BooruQuerySanitizerInterceptor
import com.wallsafe.core.network.interceptor.RefererInterceptor
import com.wallsafe.core.network.interceptor.UserAgentInterceptor
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import kotlinx.serialization.json.Json
import okhttp3.Cache
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.logging.HttpLoggingInterceptor
import retrofit2.Retrofit
import java.io.File
import java.util.concurrent.TimeUnit
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
object NetworkModule {

    @Provides
    @Singleton
    fun provideJson(): Json = Json {
        ignoreUnknownKeys = true
        isLenient = true
    }

    @Provides
    @Singleton
    fun provideOkHttpClient(@ApplicationContext context: Context): OkHttpClient {
        val cacheDir = File(context.cacheDir, "http_cache")
        val cache = Cache(cacheDir, 50L * 1024L * 1024L) // 50MB

        val loggingInterceptor = HttpLoggingInterceptor().apply {
            level = HttpLoggingInterceptor.Level.NONE
        }

        return OkHttpClient.Builder()
            .cache(cache)
            .connectTimeout(15, TimeUnit.SECONDS)
            .readTimeout(30, TimeUnit.SECONDS)
            .addInterceptor(RefererInterceptor())
            .addInterceptor(UserAgentInterceptor())
            .addInterceptor(BooruQuerySanitizerInterceptor())
            .addInterceptor(loggingInterceptor)
            .build()
    }

    @Provides
    @Singleton
    fun provideRetrofitBuilder(
        okHttpClient: OkHttpClient,
        json: Json
    ): Retrofit.Builder {
        val contentType = "application/json".toMediaType()
        return Retrofit.Builder()
            .client(okHttpClient)
            .addConverterFactory(json.asConverterFactory(contentType))
    }

    @Provides
    @Singleton
    fun provideBooruApiServiceFactory(
        retrofitBuilder: Retrofit.Builder
    ): BooruApiServiceFactory {
        return BooruApiServiceFactory(retrofitBuilder)
    }

    @Provides
    @Singleton
    fun provideGitHubApiService(
        retrofitBuilder: Retrofit.Builder
    ): com.wallsafe.core.network.GitHubApiService {
        return retrofitBuilder
            .baseUrl("https://api.github.com/")
            .build()
            .create(com.wallsafe.core.network.GitHubApiService::class.java)
    }
}
