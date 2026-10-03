package com.wallsafe.core.network.interceptor

import okhttp3.Interceptor
import okhttp3.Response

/**
 * Sanitizes URLs for Booru APIs (such as Yande.re and Konachan).
 * Yande.re returns 404 Not Found if query string contains '%20' (must be '+')
 * or '%28' / '%29' (must be raw parentheses '(' and ')').
 */
class BooruQuerySanitizerInterceptor : Interceptor {
    override fun intercept(chain: Interceptor.Chain): Response {
        val originalRequest = chain.request()
        val url = originalRequest.url
        val host = url.host

        if (!host.contains("github.com")) {
            val encodedQuery = url.encodedQuery
            if (encodedQuery != null && (encodedQuery.contains("%20") || encodedQuery.contains("%28") || encodedQuery.contains("%29"))) {
                val sanitizedQuery = encodedQuery
                    .replace("%20", "+")
                    .replace("%28", "(")
                    .replace("%29", ")")

                val newUrl = url.newBuilder()
                    .encodedQuery(sanitizedQuery)
                    .build()

                return chain.proceed(originalRequest.newBuilder().url(newUrl).build())
            }
        }

        return chain.proceed(originalRequest)
    }
}
