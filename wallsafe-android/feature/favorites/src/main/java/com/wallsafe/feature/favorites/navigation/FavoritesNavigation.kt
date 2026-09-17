package com.wallsafe.feature.favorites.navigation

import androidx.navigation.NavController
import androidx.navigation.NavGraphBuilder
import androidx.navigation.NavOptions
import androidx.navigation.compose.composable
import com.wallsafe.feature.favorites.FavoritesRoute

const val FAVORITES_ROUTE = "favorites_route"

fun NavController.navigateToFavorites(navOptions: NavOptions? = null) {
    this.navigate(FAVORITES_ROUTE, navOptions)
}

fun NavGraphBuilder.favoritesScreen(
    onNavigateToPost: (String) -> Unit
) {
    composable(route = FAVORITES_ROUTE) {
        FavoritesRoute(
            onNavigateToPost = onNavigateToPost
        )
    }
}
