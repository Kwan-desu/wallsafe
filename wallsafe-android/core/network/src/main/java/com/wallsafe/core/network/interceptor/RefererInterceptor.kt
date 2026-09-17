package com.wallsafe.core.network.interceptor

import okhttp3.Interceptor
import okhttp3.Response
import java.net.URL

class RefererInterceptor : Interceptor {
    override fun intercept(chain: Interceptor.Chain): Response {
        val originalRequest = chain.request()
        val url = originalRequest.url

        val host = url.host
        
        val referer = when {
            host.contains("yande.re") -> "https://yande.re/"
            host.contains("konachan.com") -> "https://konachan.com/"
            host.contains("konachan.net") -> "https://konachan.net/"
            else -> {
                val protocol = url.scheme
                "$protocol://$host/"
            }
        }

        val requestWithReferer = originalRequest.newBuilder()
            .header("Referer", referer)
            .build()

        return chain.proceed(requestWithReferer)
    }
}
