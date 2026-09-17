package com.wallsafe.core.model

import kotlinx.serialization.Serializable

@Serializable
data class PostItem(
    val id: Int,
    val source: String,
    val previewUrl: String,
    val sampleUrl: String?,
    val fileUrl: String?,
    val width: Int,
    val height: Int,
    val rating: String,
    val score: Int,
    val tags: String,
    val author: String? = null,
    val sourceUrl: String? = null,
    val createdAt: String? = null,
    val isFavorite: Boolean = false,
    val isDownloaded: Boolean = false,
    val localPath: String? = null,
    val collection: String? = null,
    val appliedAt: Long? = null
) {
    val resolutionText: String
        get() = "${width} × ${height}"

    val aspectRatioText: String
        get() {
            if (width == 0 || height == 0) return "Unknown"
            val ratio = width.toFloat() / height.toFloat()
            return when {
                ratio > 2.0f -> "Ultrawide"
                ratio >= 1.7f -> "16:9"
                ratio >= 1.3f -> "4:3"
                ratio >= 0.9f && ratio <= 1.1f -> "1:1"
                else -> "Portrait"
            }
        }

    val ratingDisplay: String
        get() = when (rating.lowercase()) {
            "s" -> "SFW"
            "q" -> "16+"
            "e" -> "NSFW"
            else -> "Unknown"
        }

    val thumbnailUrl: String
        get() = if (previewUrl.isNotBlank()) previewUrl else (sampleUrl ?: fileUrl ?: "")

    val bestImageUrl: String
        get() = sampleUrl ?: fileUrl ?: previewUrl

    val fullDownloadUrl: String
        get() = fileUrl ?: bestImageUrl
}
