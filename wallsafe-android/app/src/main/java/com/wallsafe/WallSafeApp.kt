package com.wallsafe

import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Download
import androidx.compose.material.icons.filled.Favorite
import androidx.compose.material.icons.filled.Home
import androidx.compose.material.icons.filled.Search
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material.icons.outlined.Download
import androidx.compose.material.icons.outlined.FavoriteBorder
import androidx.compose.material.icons.outlined.Home
import androidx.compose.material.icons.outlined.Search
import androidx.compose.material.icons.outlined.Settings
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.adaptive.navigationsuite.NavigationSuiteScaffold
import androidx.compose.material3.adaptive.navigationsuite.NavigationSuiteType
import androidx.compose.material3.windowsizeclass.WindowSizeClass
import androidx.compose.material3.windowsizeclass.WindowWidthSizeClass
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.navigation.NavDestination.Companion.hasRoute
import androidx.navigation.NavDestination.Companion.hierarchy
import androidx.navigation.compose.currentBackStackEntryAsState
import androidx.navigation.compose.rememberNavController
import com.wallsafe.navigation.Route
import com.wallsafe.navigation.WallSafeNavHost

@Composable
fun WallSafeApp(
    windowSizeClass: WindowSizeClass,
    modifier: Modifier = Modifier
) {
    val navController = rememberNavController()
    val navBackStackEntry by navController.currentBackStackEntryAsState()
    val currentDestination = navBackStackEntry?.destination

    val navSuiteType = when (windowSizeClass.widthSizeClass) {
        WindowWidthSizeClass.Expanded -> NavigationSuiteType.NavigationRail
        WindowWidthSizeClass.Medium -> NavigationSuiteType.NavigationRail
        else -> NavigationSuiteType.NavigationBar
    }

    // Show navigation suite only for top-level destinations
    val isTopLevelDestination = currentDestination?.let { dest ->
        dest.hierarchy.any {
            it.hasRoute(Route.Home::class) ||
            it.hasRoute(Route.Explore::class) ||
            it.hasRoute(Route.Favorites::class) ||
            it.hasRoute(Route.Downloads::class) ||
            it.hasRoute(Route.Settings::class)
        }
    } ?: true

    val isHome = currentDestination?.hierarchy?.any { it.hasRoute(Route.Home::class) } == true
    val isExplore = currentDestination?.hierarchy?.any { it.hasRoute(Route.Explore::class) } == true
    val isFavorites = currentDestination?.hierarchy?.any { it.hasRoute(Route.Favorites::class) } == true
    val isDownloads = currentDestination?.hierarchy?.any { it.hasRoute(Route.Downloads::class) } == true
    val isSettings = currentDestination?.hierarchy?.any { it.hasRoute(Route.Settings::class) } == true

    if (isTopLevelDestination) {
        NavigationSuiteScaffold(
            layoutType = navSuiteType,
            navigationSuiteItems = {
                item(
                    selected = isHome,
                    onClick = {
                        navController.navigate(Route.Home) {
                            popUpTo(Route.Home) { saveState = true }
                            launchSingleTop = true
                            restoreState = true
                        }
                    },
                    icon = {
                        Icon(
                            imageVector = if (isHome) Icons.Filled.Home else Icons.Outlined.Home,
                            contentDescription = "Home"
                        )
                    },
                    label = { 
                        Text("Home", maxLines = 1, softWrap = false, style = MaterialTheme.typography.labelSmall) 
                    }
                )
                item(
                    selected = isExplore,
                    onClick = {
                        navController.navigate(Route.Explore()) {
                            popUpTo(Route.Home) { saveState = true }
                            launchSingleTop = true
                            restoreState = true
                        }
                    },
                    icon = {
                        Icon(
                            imageVector = if (isExplore) Icons.Filled.Search else Icons.Outlined.Search,
                            contentDescription = "Explore"
                        )
                    },
                    label = { 
                        Text("Explore", maxLines = 1, softWrap = false, style = MaterialTheme.typography.labelSmall) 
                    }
                )
                item(
                    selected = isFavorites,
                    onClick = {
                        navController.navigate(Route.Favorites) {
                            popUpTo(Route.Home) { saveState = true }
                            launchSingleTop = true
                            restoreState = true
                        }
                    },
                    icon = {
                        Icon(
                            imageVector = if (isFavorites) Icons.Filled.Favorite else Icons.Outlined.FavoriteBorder,
                            contentDescription = "Favorites"
                        )
                    },
                    label = { 
                        Text("Favorites", maxLines = 1, softWrap = false, style = MaterialTheme.typography.labelSmall) 
                    }
                )
                item(
                    selected = isDownloads,
                    onClick = {
                        navController.navigate(Route.Downloads) {
                            popUpTo(Route.Home) { saveState = true }
                            launchSingleTop = true
                            restoreState = true
                        }
                    },
                    icon = {
                        Icon(
                            imageVector = if (isDownloads) Icons.Filled.Download else Icons.Outlined.Download,
                            contentDescription = "Downloads"
                        )
                    },
                    label = { 
                        Text("Downloads", maxLines = 1, softWrap = false, style = MaterialTheme.typography.labelSmall) 
                    }
                )
                item(
                    selected = isSettings,
                    onClick = {
                        navController.navigate(Route.Settings) {
                            popUpTo(Route.Home) { saveState = true }
                            launchSingleTop = true
                            restoreState = true
                        }
                    },
                    icon = {
                        Icon(
                            imageVector = if (isSettings) Icons.Filled.Settings else Icons.Outlined.Settings,
                            contentDescription = "Settings"
                        )
                    },
                    label = { 
                        Text("Settings", maxLines = 1, softWrap = false, style = MaterialTheme.typography.labelSmall) 
                    }
                )
            }
        ) {
            WallSafeNavHost(navController = navController, modifier = modifier)
        }
    } else {
        WallSafeNavHost(navController = navController, modifier = modifier)
    }
}
