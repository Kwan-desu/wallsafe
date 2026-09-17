package com.wallsafe.core.network

import retrofit2.Retrofit
import java.util.concurrent.ConcurrentHashMap
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class BooruApiServiceFactory @Inject constructor(
    private val retrofitBuilder: Retrofit.Builder
) {
    private val serviceCache = ConcurrentHashMap<String, BooruApiService>()

    private val sourceMappings = mapOf(
        "yande" to "https://yande.re/",
        "konasfw" to "https://konachan.net/",
        "konansfw" to "https://konachan.com/"
    )

    fun create(sourceId: String): BooruApiService {
        val baseUrl = getBaseUrl(sourceId)
        
        return serviceCache.getOrPut(baseUrl) {
            retrofitBuilder
                .baseUrl(baseUrl)
                .build()
                .create(BooruApiService::class.java)
        }
    }

    private fun getBaseUrl(sourceId: String): String {
        return sourceMappings[sourceId]
            ?: if (sourceId.startsWith("http://") || sourceId.startsWith("https://")) {
                if (sourceId.endsWith("/")) sourceId else "$sourceId/"
            } else {
                throw IllegalArgumentException("Unknown sourceId: $sourceId. Must be a known source or a valid URL.")
            }
    }
}
