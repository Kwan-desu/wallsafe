package com.wallsafe.core.model

import kotlinx.serialization.Serializable

enum class TagCategory {
    General, Artist, Series, Character, Circle, Faults
}

@Serializable
data class TagSuggestion(
    val name: String,
    val count: Int,
    val category: TagCategory
) {
    val categoryColor: String
        get() = when (category) {
            TagCategory.General -> "#009BE6"
            TagCategory.Artist -> "#CCA400"
            TagCategory.Series -> "#FF00FF"
            TagCategory.Character -> "#00AA00"
            TagCategory.Circle -> "#00CCCC"
            TagCategory.Faults -> "#FF0000"
        }
}
