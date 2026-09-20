package com.wallsafe.core.network

import com.wallsafe.core.network.dto.GitHubReleaseDto
import retrofit2.http.GET
import retrofit2.http.Header
import retrofit2.http.Path

interface GitHubApiService {

    @GET("repos/{owner}/{repo}/releases")
    suspend fun getReleases(
        @Path("owner") owner: String = "Kwan-desu",
        @Path("repo") repo: String = "wallsafe",
        @Header("Authorization") authorization: String? = null
    ): List<GitHubReleaseDto>
}
