package com.wallsafe.core.model

import kotlinx.serialization.Serializable

@Serializable
data class SeriesCategoryItem(
    val name: String,
    val tag: String,
    val type: String = "Anime",
    val postCount: Int = 0,
    val previewImageUrl: String? = null,
    val isCustom: Boolean = false
) {
    val formattedCount: String
        get() = when {
            postCount >= 1000000 -> String.format("%.1fm Artworks", postCount / 1000000.0)
            postCount >= 1000 -> String.format("%.1fk Artworks", postCount / 1000.0)
            else -> "$postCount Artworks"
        }
}
