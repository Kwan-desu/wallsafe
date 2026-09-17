package com.wallsafe.core.network

import com.wallsafe.core.network.dto.BooruPostDto
import com.wallsafe.core.network.dto.BooruTagDto
import retrofit2.http.GET
import retrofit2.http.Query

interface BooruApiService {
    @GET("post.json")
    suspend fun fetchPosts(
        @Query("tags") tags: String,
        @Query("page") page: Int,
        @Query("limit") limit: Int = 40
    ): List<BooruPostDto>

    @GET("tag.json")
    suspend fun searchTags(
        @Query("name") query: String,
        @Query("order") order: String = "count",
        @Query("limit") limit: Int = 14
    ): List<BooruTagDto>

    @GET("tag.json")
    suspend fun getPopularSeriesTags(
        @Query("order") order: String = "count",
        @Query("type") type: Int = 3,
        @Query("limit") limit: Int = 30
    ): List<BooruTagDto>
}
