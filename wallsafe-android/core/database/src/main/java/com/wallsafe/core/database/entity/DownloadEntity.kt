package com.wallsafe.core.database.entity

import androidx.room.Entity
import androidx.room.Index
import androidx.room.PrimaryKey

@Entity(
    tableName = "downloads",
    indices = [Index(value = ["postId", "source"], unique = true)]
)
data class DownloadEntity(
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
    val localUri: String,
    val downloadedAt: Long
)
