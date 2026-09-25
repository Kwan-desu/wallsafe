package com.wallsafe.core.data.repository

import com.wallsafe.core.model.PostItem
import com.wallsafe.core.model.SeriesCategoryItem
import com.wallsafe.core.model.TagSuggestion
import com.wallsafe.core.network.BooruApiServiceFactory
import com.wallsafe.core.network.dto.toDomain
import kotlinx.coroutines.async
import kotlinx.coroutines.awaitAll
import kotlinx.coroutines.coroutineScope
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class BooruRepositoryImpl @Inject constructor(
    private val apiServiceFactory: BooruApiServiceFactory
) : BooruRepository {

    override suspend fun fetchPosts(source: String, tags: String, page: Int, limit: Int): List<PostItem> {
        if (source == "all") {
            return fetchAllSources(tags, page, limit)
        }
        return try {
            val api = apiServiceFactory.create(source)
            api.fetchPosts(tags = tags, page = page, limit = limit).map { it.toDomain(source) }
        } catch (e: Exception) {
            e.printStackTrace()
            emptyList()
        }
    }

    override suspend fun fetchAllSources(tags: String, page: Int, limit: Int): List<PostItem> = coroutineScope {
        val sources = listOf("yande", "konasfw")
        val perSourceLimit = (limit / sources.size).coerceAtLeast(1)
        val deferreds = sources.map { src ->
            async {
                try {
                    val api = apiServiceFactory.create(src)
                    api.fetchPosts(tags = tags, page = page, limit = perSourceLimit).map { it.toDomain(src) }
                } catch (e: Exception) {
                    e.printStackTrace()
                    emptyList()
                }
            }
        }
        deferreds.awaitAll().flatten().sortedByDescending { it.score }
    }

    override suspend fun searchTags(source: String, query: String): List<TagSuggestion> {
        val cleanQuery = query.trim()
        if (cleanQuery.isBlank()) return emptyList()
        val actualSource = when (source) {
            "konasfw", "konansfw" -> source
            else -> if (source.startsWith("http://") || source.startsWith("https://")) source else "yande"
        }
        return try {
            val api = apiServiceFactory.create(actualSource)
            api.searchTags(query = cleanQuery).map { it.toDomain() }
        } catch (e: Exception) {
            emptyList()
        }
    }

    private var cachedPopularSeries: List<SeriesCategoryItem>? = null

    override suspend fun getPopularSeries(source: String, limit: Int): List<SeriesCategoryItem> {
        cachedPopularSeries?.let { return it }
        val actualSource = if (source == "all") "yande" else source
        val api = apiServiceFactory.create(actualSource)
        val initialItems = try {
            val tags = api.getPopularSeriesTags(limit = limit)
            tags.map { tag ->
                SeriesCategoryItem(
                    name = formatTagName(tag.name),
                    tag = tag.name,
                    type = "Anime",
                    postCount = tag.count,
                    previewImageUrl = null
                )
            }
        } catch (e: Exception) {
            emptyList()
        }.ifEmpty {
            listOf(
                SeriesCategoryItem(
                    name = "Blue Archive",
                    tag = "blue_archive",
                    type = "Game",
                    postCount = 47703,
                    previewImageUrl = "https://files.yande.re/sample/0499e17c1295fcaf4cfdf2ed84fb997c/yande.re%201268720%20sample%20blue_archive%20halo%20professor_niyaniya%20seifuku%20shandian_yz.jpg"
                ),
                SeriesCategoryItem(
                    name = "Azur Lane",
                    tag = "azur_lane",
                    type = "Game",
                    postCount = 44473,
                    previewImageUrl = "https://files.yande.re/sample/faecbeabae5d5bf7a1d4e55dda881a59/yande.re%201268706%20sample%20azur_lane%20cleavage%20japanese_clothes%20open_shirt%20shoukaku_%28azur_lane%29%20tagme%20umbrella.jpg"
                ),
                SeriesCategoryItem(
                    name = "Genshin Impact",
                    tag = "genshin_impact",
                    type = "Game",
                    postCount = 42691,
                    previewImageUrl = "https://files.yande.re/sample/4eaea2146a27d06be7e98240ad94c267/yande.re%201268241%20sample%20dress%20genshin_impact%20heels%20no_bra%20odette_%28genshin_impact%29%20pantyhose%20skirt_lift%20tagme%20wallpaper.jpg"
                ),
                SeriesCategoryItem(
                    name = "Hololive",
                    tag = "hololive",
                    type = "VTuber",
                    postCount = 36314,
                    previewImageUrl = "https://files.yande.re/sample/9bb9f722fcaeca2b0440b6bd318f19a4/yande.re%201268561%20sample%20hololive%20hololive_dev_is%20ichijou_ririka%20jwk76806995.jpg"
                ),
                SeriesCategoryItem(
                    name = "Fate/Grand Order",
                    tag = "fate/grand_order",
                    type = "Game",
                    postCount = 31423,
                    previewImageUrl = "https://files.yande.re/sample/e5d14006b177a4327b1b132d1cdc08bc/yande.re%201264674%20sample%20artoria_caster_%28fate%29%20disc_cover%20fate_grand_order%20heels%20pantyhose%20takeuchi_takashi%20uniform%20weapon.jpg"
                ),
                SeriesCategoryItem(
                    name = "Touhou Project",
                    tag = "touhou",
                    type = "Game",
                    postCount = 31359,
                    previewImageUrl = "https://files.yande.re/sample/e2b1e85cccfaf3fc9d05f1c31e7a74c1/yande.re%201268556%20sample%20cirno%20dress%20kuroida%20skirt_lift%20touhou%20wings.jpg"
                ),
                SeriesCategoryItem(
                    name = "The Idolmaster",
                    tag = "the_idolmaster",
                    type = "Game",
                    postCount = 27340,
                    previewImageUrl = "https://files.yande.re/sample/f208c68fb7f521abf607f51f51c6b7eb/yande.re%201268613%20sample%20ikuta_haruki%20sakuragi_mano%20tagme%20the_idolm%40ster%20the_idolm%40ster_shiny_colors%20uniform.jpg"
                ),
                SeriesCategoryItem(
                    name = "Kantai Collection",
                    tag = "kantai_collection",
                    type = "Game",
                    postCount = 25137,
                    previewImageUrl = "https://files.yande.re/sample/cb1f6534ee85c4af86d535eefa88a41d/yande.re%201254074%20sample%20bigkwl%20garter%20gun%20iowa_%28kancolle%29%20kantai_collection%20torn_clothes.jpg"
                ),
                SeriesCategoryItem(
                    name = "Vocaloid",
                    tag = "vocaloid",
                    type = "Music",
                    postCount = 16343,
                    previewImageUrl = "https://files.yande.re/sample/7058395362093e7df08d9927d275015b/yande.re%201268053%20sample%20hatsune_miku%20headphones%20pipi_%28hana_no_orchestra%29%20tattoo%20vocaloid.jpg"
                ),
                SeriesCategoryItem(
                    name = "Arknights",
                    tag = "arknights",
                    type = "Game",
                    postCount = 16284,
                    previewImageUrl = "https://files.yande.re/sample/fb8d1abf74aac414539faae1d8e4091b/yande.re%201268362%20sample%20arknights%20horns%20pdxen%20skirt_lift%20tail%20thighhighs%20typhon.jpg"
                )
            ).take(limit)
        }

        return coroutineScope {
            initialItems.map { item ->
                if (!item.previewImageUrl.isNullOrBlank()) {
                    item
                } else {
                    async {
                        val preview = try {
                            val posts = api.fetchPosts(page = 1, limit = 1, tags = "${item.tag} rating:s")
                            posts.firstOrNull()?.sampleUrl ?: posts.firstOrNull()?.previewUrl
                        } catch (e: Exception) {
                            null
                        }
                        item.copy(previewImageUrl = preview)
                    }.await()
                }
            }
        }.also { cachedPopularSeries = it }
    }

    private fun formatTagName(raw: String): String {
        return raw.split("_")
            .joinToString(" ") { word ->
                word.replaceFirstChar { if (it.isLowerCase()) it.titlecase() else it.toString() }
            }
    }
}
