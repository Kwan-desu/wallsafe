package com.wallsafe.feature.preview.navigation

import androidx.navigation.NavController
import androidx.navigation.NavGraphBuilder
import androidx.navigation.NavType
import androidx.navigation.compose.composable
import androidx.navigation.navArgument
import com.wallsafe.feature.preview.PreviewScreen

const val previewRoute = "preview_route/{postId}/{source}"

fun NavController.navigateToPreview(postId: Int, source: String) {
    this.navigate("preview_route/$postId/$source")
}

fun NavGraphBuilder.previewScreen(
    onBackClick: () -> Unit
) {
    composable(
        route = previewRoute,
        arguments = listOf(
            navArgument("postId") { type = NavType.IntType },
            navArgument("source") { type = NavType.StringType }
        )
    ) {
        PreviewScreen(
            onBackClick = onBackClick
        )
    }
}
