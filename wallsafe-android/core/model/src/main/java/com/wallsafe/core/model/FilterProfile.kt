package com.wallsafe.core.model

import kotlinx.serialization.Serializable

@Serializable
data class FilterProfile(
    val ratingTag: String,
    val sortOrder: String = "score",
    val resolutionIndex: Int = 0,
    val aspectRatioIndex: Int = 0,
    val sourceKey: String = "all"
)
