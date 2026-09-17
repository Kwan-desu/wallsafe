package com.wallsafe.feature.downloads.navigation

import androidx.navigation.NavController
import androidx.navigation.NavGraphBuilder
import androidx.navigation.NavOptions
import androidx.navigation.compose.composable
import com.wallsafe.feature.downloads.DownloadsRoute

const val DOWNLOADS_ROUTE = "downloads_route"

fun NavController.navigateToDownloads(navOptions: NavOptions? = null) {
    this.navigate(DOWNLOADS_ROUTE, navOptions)
}

fun NavGraphBuilder.downloadsScreen() {
    composable(route = DOWNLOADS_ROUTE) {
        DownloadsRoute()
    }
}
