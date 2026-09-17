package com.wallsafe.core.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

@Composable
fun RatingBadge(rating: String, modifier: Modifier = Modifier) {
    val (text, backgroundColor, textColor) = when (rating.lowercase()) {
        "s", "safe" -> Triple("SFW", Color(0xFF4CAF50), Color.White)
        "q", "questionable" -> Triple("16+", Color(0xFFFF9800), Color.White)
        "e", "explicit" -> Triple("NSFW", Color(0xFFF44336), Color.White)
        else -> Triple("UNKN", Color.Gray, Color.White)
    }

    Box(
        modifier = modifier
            .background(backgroundColor, RoundedCornerShape(percent = 50))
            .padding(horizontal = 8.dp, vertical = 4.dp)
    ) {
        Text(
            text = text,
            color = textColor,
            fontSize = 10.sp,
            fontWeight = FontWeight.Bold
        )
    }
}
