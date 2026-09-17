package com.wallsafe.feature.explore.navigation

import androidx.navigation.NavController
import androidx.navigation.NavGraphBuilder
import androidx.navigation.compose.composable
import com.wallsafe.feature.explore.ExploreScreen

const val exploreRoute = "explore_route"

fun NavController.navigateToExplore() {
    this.navigate(exploreRoute) {
        popUpTo(0)
    }
}

fun NavGraphBuilder.exploreScreen(
    onNavigateToPreview: (postId: Int, source: String) -> Unit
) {
    composable(route = exploreRoute) {
        ExploreScreen(
            onNavigateToPreview = onNavigateToPreview
        )
    }
}
