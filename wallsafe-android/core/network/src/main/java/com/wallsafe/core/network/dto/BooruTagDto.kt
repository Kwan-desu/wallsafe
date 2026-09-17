package com.wallsafe.core.network.dto

import com.wallsafe.core.model.TagCategory
import com.wallsafe.core.model.TagSuggestion
import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable

@Serializable
data class BooruTagDto(
    @SerialName("id") val id: Int,
    @SerialName("name") val name: String,
    @SerialName("count") val count: Int,
    @SerialName("type") val type: Int
)

fun BooruTagDto.toDomain(): TagSuggestion {
    val category = when (type) {
        0 -> TagCategory.General
        1 -> TagCategory.Artist
        3 -> TagCategory.Series
        4 -> TagCategory.Character
        5 -> TagCategory.Circle
        else -> TagCategory.General
    }
    
    return TagSuggestion(
        name = name,
        count = count,
        category = category
    )
}
