package com.wallsafe.core.database.entity

import androidx.room.Entity
import androidx.room.Index
import androidx.room.PrimaryKey

@Entity(
    tableName = "favorites",
    indices = [
        Index(value = ["postId", "source"], unique = true),
        Index(value = ["collectionId"])
    ]
)
data class FavoriteEntity(
    @PrimaryKey(autoGenerate = true)
    val id: Int = 0,
    val postId: Int,
    val source: String,
    val previewUrl: String,
    val sampleUrl: String?,
    val fileUrl: String?,
    val width: Int,
    val height: Int,
    val rating: String,
    val score: Int,
    val tags: String,
    val author: String?,
    val sourceUrl: String?,
    val createdAt: String?,
    val collectionId: Int?,
    val favoritedAt: Long
)
