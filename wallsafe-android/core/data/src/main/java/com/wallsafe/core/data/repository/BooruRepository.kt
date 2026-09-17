package com.wallsafe.core.data.repository

import com.wallsafe.core.model.PostItem
import com.wallsafe.core.model.SeriesCategoryItem
import com.wallsafe.core.model.TagSuggestion

interface BooruRepository {
    suspend fun fetchPosts(source: String, tags: String, page: Int, limit: Int): List<PostItem>
    suspend fun fetchAllSources(tags: String, page: Int, limit: Int): List<PostItem>
    suspend fun searchTags(source: String, query: String): List<TagSuggestion>
    suspend fun getPopularSeries(source: String, limit: Int): List<SeriesCategoryItem>
}
