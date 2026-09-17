package com.wallsafe.core.ui.theme

import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Shapes
import androidx.compose.ui.unit.dp

val Shapes = Shapes(
    small = RoundedCornerShape(8.dp),
    medium = RoundedCornerShape(16.dp), // cards
    large = RoundedCornerShape(16.dp), // FAB
    extraLarge = RoundedCornerShape(28.dp) // bottom sheets, dialogs, search bar
)
