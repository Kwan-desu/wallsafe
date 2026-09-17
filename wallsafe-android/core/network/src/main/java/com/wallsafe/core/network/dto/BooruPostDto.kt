package com.wallsafe.core.network.dto

import com.wallsafe.core.model.PostItem
import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable

@Serializable
data class BooruPostDto(
    @SerialName("id") val id: Int,
    @SerialName("tags") val tags: String,
    @SerialName("created_at") val createdAt: Int,
    @SerialName("source") val source: String? = null,
    @SerialName("score") val score: Int,
    @SerialName("md5") val md5: String? = null,
    @SerialName("rating") val rating: String,
    @SerialName("file_url") val fileUrl: String? = null,
    @SerialName("preview_url") val previewUrl: String? = null,
    @SerialName("sample_url") val sampleUrl: String? = null,
    @SerialName("width") val width: Int,
    @SerialName("height") val height: Int,
    @SerialName("author") val author: String? = null,
    @SerialName("jpeg_url") val jpegUrl: String? = null
)

fun BooruPostDto.toDomain(sourceId: String): PostItem {
    return PostItem(
        id = id,
        source = sourceId,
        previewUrl = fixProtocolRelativeUrl(previewUrl) ?: "",
        sampleUrl = fixProtocolRelativeUrl(sampleUrl),
        fileUrl = fixProtocolRelativeUrl(fileUrl) ?: fixProtocolRelativeUrl(jpegUrl),
        width = width,
        height = height,
        rating = rating,
        score = score,
        tags = tags,
        author = author,
        sourceUrl = source,
        createdAt = createdAt.toString()
    )
}

private fun fixProtocolRelativeUrl(url: String?): String? {
    if (url == null) return null
    if (url.startsWith("//")) {
        return "https:$url"
    }
    return url
}
