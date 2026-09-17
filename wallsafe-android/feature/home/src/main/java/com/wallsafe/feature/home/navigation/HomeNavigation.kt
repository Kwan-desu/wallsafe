package com.wallsafe.feature.home.navigation

import androidx.navigation.NavController
import androidx.navigation.NavGraphBuilder
import androidx.navigation.NavOptions
import androidx.navigation.compose.composable
import com.wallsafe.feature.home.HomeRoute

const val HOME_ROUTE = "home_route"

fun NavController.navigateToHome(navOptions: NavOptions? = null) {
    this.navigate(HOME_ROUTE, navOptions)
}

fun NavGraphBuilder.homeScreen(
    onNavigateToExplore: () -> Unit,
    onNavigateToSeries: (String) -> Unit,
    onNavigateToPreview: (Int, String) -> Unit = { _, _ -> }
) {
    composable(route = HOME_ROUTE) {
        HomeRoute(
            onNavigateToExplore = onNavigateToExplore,
            onNavigateToSeries = onNavigateToSeries,
            onNavigateToPreview = onNavigateToPreview
        )
    }
}
