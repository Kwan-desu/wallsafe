package com.wallsafe.core.database.entity

import androidx.room.Entity
import androidx.room.PrimaryKey

@Entity(tableName = "custom_sources")
data class CustomSourceEntity(
    @PrimaryKey
    val id: String,
    val name: String,
    val baseUrl: String,
    val isSfw: Boolean,
    val enabled: Boolean
)
