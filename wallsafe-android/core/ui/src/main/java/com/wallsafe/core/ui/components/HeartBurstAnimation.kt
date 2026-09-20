package com.wallsafe.core.ui.components

import androidx.compose.animation.core.*
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.size
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Favorite
import androidx.compose.material3.Icon
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.draw.scale
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.delay

@Composable
fun HeartBurstAnimation(
    trigger: Boolean,
    onAnimationEnd: () -> Unit,
    modifier: Modifier = Modifier,
    size: Dp = 72.dp,
    heartColor: Color = Color(0xFFFF2A55)
) {
    if (!trigger) return

    val scale = remember { Animatable(0f) }
    val alpha = remember { Animatable(1f) }

    LaunchedEffect(trigger) {
        scale.snapTo(0f)
        alpha.snapTo(1f)

        // Pop up with overshoot spring
        scale.animateTo(
            targetValue = 1.3f,
            animationSpec = spring(
                dampingRatio = Spring.DampingRatioMediumBouncy,
                stiffness = Spring.StiffnessLow
            )
        )
        scale.animateTo(
            targetValue = 1.0f,
            animationSpec = tween(150, easing = FastOutSlowInEasing)
        )

        // Hold briefly, then fade out
        delay(350)
        alpha.animateTo(
            targetValue = 0f,
            animationSpec = tween(250, easing = LinearOutSlowInEasing)
        )
        onAnimationEnd()
    }

    Box(
        modifier = modifier.fillMaxSize(),
        contentAlignment = Alignment.Center
    ) {
        Icon(
            imageVector = Icons.Default.Favorite,
            contentDescription = null,
            tint = heartColor,
            modifier = Modifier
                .size(size)
                .scale(scale.value)
                .alpha(alpha.value)
        )
    }
}
