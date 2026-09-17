package com.wallsafe.navigation

import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.navigation.NavHostController
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import com.wallsafe.feature.downloads.DownloadsRoute
import com.wallsafe.feature.explore.ExploreScreen
import com.wallsafe.feature.favorites.FavoritesRoute
import com.wallsafe.feature.home.HomeRoute
import com.wallsafe.feature.preview.PreviewScreen
import com.wallsafe.feature.settings.SettingsRoute
import kotlinx.serialization.Serializable

import androidx.navigation.toRoute

sealed interface Route {
    @Serializable
    data object Home : Route

    @Serializable
    data class Explore(val tag: String? = null) : Route

    @Serializable
    data object Favorites : Route

    @Serializable
    data object Downloads : Route

    @Serializable
    data object Settings : Route

    @Serializable
    data class Preview(val postId: Int, val source: String) : Route
}

@Composable
fun WallSafeNavHost(
    navController: NavHostController,
    modifier: Modifier = Modifier
) {
    NavHost(
        navController = navController,
        startDestination = Route.Home,
        modifier = modifier
    ) {
        composable<Route.Home> {
            HomeRoute(
                onNavigateToExplore = {
                    navController.navigate(Route.Explore()) {
                        popUpTo(Route.Home) { saveState = true }
                        launchSingleTop = true
                        restoreState = true
                    }
                },
                onNavigateToSeries = { seriesTag ->
                    navController.navigate(Route.Explore(tag = seriesTag)) {
                        popUpTo(Route.Home) { saveState = true }
                        launchSingleTop = true
                    }
                },
                onNavigateToFavorites = {
                    navController.navigate(Route.Favorites) {
                        popUpTo(Route.Home) { saveState = true }
                        launchSingleTop = true
                        restoreState = true
                    }
                },
                onNavigateToDownloads = {
                    navController.navigate(Route.Downloads) {
                        popUpTo(Route.Home) { saveState = true }
                        launchSingleTop = true
                        restoreState = true
                    }
                },
                onNavigateToPreview = { postId, source ->
                    navController.navigate(Route.Preview(postId = postId, source = source))
                }
            )
        }
        composable<Route.Explore> { backStackEntry ->
            val exploreRoute = backStackEntry.toRoute<Route.Explore>()
            ExploreScreen(
                initialTag = exploreRoute.tag,
                onNavigateToPreview = { postId, source ->
                    navController.navigate(Route.Preview(postId = postId, source = source))
                }
            )
        }
        composable<Route.Favorites> {
            FavoritesRoute(
                onNavigateToPost = { postIdentifier ->
                    val parts = postIdentifier.split(":")
                    val id = parts.firstOrNull()?.toIntOrNull() ?: 0
                    val src = if (parts.size > 1) parts[1] else "all"
                    navController.navigate(Route.Preview(postId = id, source = src))
                }
            )
        }
        composable<Route.Downloads> {
            DownloadsRoute(
                onNavigateToPreview = { postId, source ->
                    navController.navigate(Route.Preview(postId = postId, source = source))
                }
            )
        }
        composable<Route.Settings> {
            SettingsRoute()
        }
        composable<Route.Preview> {
            PreviewScreen(
                onBackClick = { navController.popBackStack() },
                onTagClick = { tag ->
                    navController.navigate(Route.Explore(tag = tag)) {
                        popUpTo(Route.Home) { saveState = true }
                        launchSingleTop = true
                    }
                }
            )
        }
    }
}
