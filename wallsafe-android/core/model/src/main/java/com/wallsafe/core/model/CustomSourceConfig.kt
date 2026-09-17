package com.wallsafe.core.model

import kotlinx.serialization.Serializable

@Serializable
data class CustomSourceConfig(
    val id: String,
    val name: String,
    val baseUrl: String,
    val isSfw: Boolean,
    val enabled: Boolean = true
)
