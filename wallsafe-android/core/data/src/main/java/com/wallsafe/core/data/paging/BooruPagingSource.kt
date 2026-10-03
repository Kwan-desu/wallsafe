package com.wallsafe.core.data.paging

import androidx.paging.PagingSource
import androidx.paging.PagingState
import com.wallsafe.core.data.repository.BooruRepository
import com.wallsafe.core.model.PostItem

import com.wallsafe.core.model.ContentRatingMode
import com.wallsafe.core.model.TagAliases

class BooruPagingSource(
    private val repository: BooruRepository,
    private val source: String,
    private val tags: String,
    private val ratingMode: ContentRatingMode = ContentRatingMode.SfwOnly,
    private val sortOrder: String = "score",
    private val resolutionFilter: Int = 0,
    private val aspectRatioFilter: Int = 0,
    private val wallpaperOnly: Boolean = false,
    private val tagBlacklist: Set<String> = emptySet()
) : PagingSource<Int, PostItem>() {

    override suspend fun load(params: LoadParams<Int>): LoadResult<Int, PostItem> {
        return try {
            val page = params.key ?: 1
            
            // Build effective tags with smart alias resolution
            val resolvedTags = TagAliases.resolveSmartQuery(tags)
            val queryTokens = resolvedTags.trim().split("\\s+".toRegex()).filter { it.isNotBlank() }.toMutableList()
            
            // 1. Rating filter
            queryTokens.removeAll { it.startsWith("rating:") }
            when (ratingMode) {
                ContentRatingMode.SfwOnly -> queryTokens.add("rating:s")
                ContentRatingMode.Questionable -> queryTokens.add("rating:q")
                else -> { /* unrestricted */ }
            }

            // 2. Wallpaper tag filter or sort
            if (wallpaperOnly || sortOrder == "wallpaper") {
                if (!queryTokens.any { it.equals("wallpaper", ignoreCase = true) }) {
                    queryTokens.add("wallpaper")
                }
            }

            // 3. Sort order
            queryTokens.removeAll { it.startsWith("order:") }
            when (sortOrder) {
                "score" -> queryTokens.add("order:score")
                "newest" -> queryTokens.add("order:id")
                "random" -> queryTokens.add("order:random")
                "wallpaper" -> queryTokens.add("order:score")
            }

            val effectiveTags = queryTokens.joinToString(" ")

            var currentPage = page
            val collectedItems = mutableListOf<PostItem>()
            val maxPagesToProbe = if (aspectRatioFilter != 0 || resolutionFilter != 0) 3 else 1
            var reachedEnd = false

            for (probe in 0 until maxPagesToProbe) {
                val rawItems = if (source == "all") {
                    repository.fetchAllSources(effectiveTags, currentPage, params.loadSize)
                } else {
                    repository.fetchPosts(source, effectiveTags, currentPage, params.loadSize)
                }

                if (rawItems.isEmpty()) {
                    reachedEnd = true
                    break
                }

                val filtered = rawItems.filter { post ->
                    // Rating check
                    val r = post.rating.lowercase()
                    val matchesRating = when (ratingMode) {
                        ContentRatingMode.SfwOnly -> r == "s" || r == "safe" || r == "g" || r == "general"
                        ContentRatingMode.Questionable -> r != "e" && r != "explicit"
                        else -> true
                    }
                    if (!matchesRating) return@filter false

                    // Tag blacklist check
                    if (tagBlacklist.isNotEmpty()) {
                        val postTags = post.tags.lowercase().split("\\s+".toRegex()).toSet()
                        if (tagBlacklist.any { it.isNotBlank() && it.lowercase() in postTags }) return@filter false
                    }

                    // Wallpaper tag or widescreen check
                    if (wallpaperOnly || sortOrder == "wallpaper") {
                        val hasTag = post.tags.split(" ").any { it.equals("wallpaper", ignoreCase = true) }
                        val isLandscape = post.width > 0 && post.height > 0 && (post.width.toFloat() / post.height.toFloat() >= 1.25f)
                        val isPortraitWanted = aspectRatioFilter == 4
                        if (!hasTag && !isLandscape && !isPortraitWanted) return@filter false
                    }

                    // Resolution filter
                    if (resolutionFilter != 0) {
                        val matchesResolution = when (resolutionFilter) {
                            1 -> post.width >= 1920 || post.height >= 1080 // 1080p+
                            2 -> post.width >= 2560 || post.height >= 1440 // 1440p+
                            3 -> post.width >= 3840 || post.height >= 2160 // 4K+
                            else -> true
                        }
                        if (!matchesResolution) return@filter false
                    }

                    // Aspect ratio filter
                    if (aspectRatioFilter != 0) {
                        if (post.width <= 0 || post.height <= 0) return@filter false
                        val ratio = post.width.toFloat() / post.height.toFloat()
                        val matchesAspect = when (aspectRatioFilter) {
                            1 -> ratio in 1.55f..1.95f // 16:9
                            2 -> ratio > 1.95f // Ultrawide
                            3 -> ratio in 1.2f..1.55f // 4:3
                            4 -> ratio < 0.95f // Portrait
                            else -> true
                        }
                        if (!matchesAspect) return@filter false
                    }

                    true
                }

                collectedItems.addAll(filtered)
                currentPage++

                if (collectedItems.size >= 12 || rawItems.size < params.loadSize) {
                    if (rawItems.size < params.loadSize) reachedEnd = true
                    break
                }
            }

            // Deduplicate items
            val distinctItems = collectedItems.distinctBy { "${it.source}_${it.id}" }

            val nextKey = if (reachedEnd || (distinctItems.isEmpty() && currentPage > page)) {
                null
            } else {
                currentPage
            }

            LoadResult.Page(
                data = distinctItems,
                prevKey = if (page == 1) null else page - 1,
                nextKey = nextKey
            )
        } catch (e: Exception) {
            LoadResult.Error(e)
        }
    }

    override fun getRefreshKey(state: PagingState<Int, PostItem>): Int? {
        return state.anchorPosition?.let { anchorPosition ->
            state.closestPageToPosition(anchorPosition)?.prevKey?.plus(1)
                ?: state.closestPageToPosition(anchorPosition)?.nextKey?.minus(1)
        }
    }
}
