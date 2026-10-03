package com.wallsafe.core.model

import kotlinx.serialization.Serializable

enum class TagCategory {
    General, Artist, Series, Character, Circle, Faults
}

@Serializable
data class TagSuggestion(
    val name: String,
    val count: Int,
    val category: TagCategory,
    val title: String? = null
) {
    val displayTitle: String
        get() = title?.takeIf { it.isNotBlank() } ?: formatBooruTagName(name)

    val categoryColor: String
        get() = when (category) {
            TagCategory.General -> "#009BE6"
            TagCategory.Artist -> "#CCA400"
            TagCategory.Series -> "#FF00FF"
            TagCategory.Character -> "#00AA00"
            TagCategory.Circle -> "#00CCCC"
            TagCategory.Faults -> "#FF0000"
        }

    companion object {
        fun formatBooruTagName(rawName: String): String {
            return rawName.split("_")
                .filter { it.isNotBlank() }
                .joinToString(" ") { word ->
                    when {
                        word.startsWith("(") && word.endsWith(")") -> {
                            val inner = word.removeSurrounding("(", ")")
                            "(" + inner.replaceFirstChar { if (it.isLowerCase()) it.titlecase() else it.toString() } + ")"
                        }
                        word.startsWith("(") -> {
                            val inner = word.removePrefix("(")
                            "(" + inner.replaceFirstChar { if (it.isLowerCase()) it.titlecase() else it.toString() }
                        }
                        word.endsWith(")") -> {
                            val inner = word.removeSuffix(")")
                            inner.replaceFirstChar { if (it.isLowerCase()) it.titlecase() else it.toString() } + ")"
                        }
                        else -> {
                            word.replaceFirstChar { if (it.isLowerCase()) it.titlecase() else it.toString() }
                        }
                    }
                }
        }
    }
}
