package com.wallsafe.feature.explore.components

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp
import com.wallsafe.core.model.TagSuggestion
import kotlin.math.ln
import kotlin.math.pow

@Composable
fun SuggestionsDropdown(
    suggestions: List<TagSuggestion>,
    onSuggestionSelected: (String) -> Unit,
    modifier: Modifier = Modifier
) {
    LazyColumn(
        modifier = modifier.fillMaxWidth()
    ) {
        items(
            items = suggestions,
            key = { it.name }
        ) { suggestion ->
            SuggestionItem(
                suggestion = suggestion,
                onClick = { onSuggestionSelected(suggestion.name) }
            )
        }
    }
}

@Composable
private fun SuggestionItem(
    suggestion: TagSuggestion,
    onClick: () -> Unit
) {
    val categoryColor = remember(suggestion.categoryColor) {
        try {
            Color(android.graphics.Color.parseColor(suggestion.categoryColor))
        } catch (e: Exception) {
            Color(0xFF64B5F6)
        }
    }

    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(onClick = onClick)
            .padding(horizontal = 16.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Box(
            modifier = Modifier
                .size(8.dp)
                .background(categoryColor, CircleShape)
        )
        
        Text(
            text = suggestion.name,
            style = MaterialTheme.typography.bodyLarge,
            modifier = Modifier.padding(start = 16.dp)
        )
        
        Spacer(modifier = Modifier.weight(1f))
        
        Text(
            text = formatCount(suggestion.count),
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
    }
}

private fun formatCount(count: Int): String {
    if (count < 1000) return count.toString()
    val exp = (ln(count.toDouble()) / ln(1000.0)).toInt()
    return String.format("%.1f%c", count / 1000.0.pow(exp.toDouble()), "kMGTPE"[exp - 1])
}
