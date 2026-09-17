package com.wallsafe.core.common

fun String.toSafeUrl(): String {
    if (this.startsWith("//")) {
        return "https:$this"
    }
    return this
}

fun Int.formatCount(): String {
    return when {
        this >= 1_000_000 -> String.format("%.1fm", this / 1_000_000.0)
        this >= 1_000 -> String.format("%.1fk", this / 1_000.0)
        else -> this.toString()
    }
}

fun Long.toRelativeTimeString(): String {
    val now = System.currentTimeMillis()
    val diff = now - this
    
    val seconds = diff / 1000
    val minutes = seconds / 60
    val hours = minutes / 60
    val days = hours / 24
    
    return when {
        days > 365 -> "${days / 365}y ago"
        days > 30 -> "${days / 30}mo ago"
        days > 0 -> "${days}d ago"
        hours > 0 -> "${hours}h ago"
        minutes > 0 -> "${minutes}m ago"
        else -> "Just now"
    }
}
