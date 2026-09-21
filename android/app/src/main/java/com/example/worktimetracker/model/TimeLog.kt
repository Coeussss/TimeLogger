package com.example.worktimetracker.model

import java.time.Instant
import java.time.LocalDate
import java.time.LocalDateTime
import java.time.OffsetDateTime
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.util.UUID

data class TimeLog(
    val id: String = UUID.randomUUID().toString(),
    val timestamp: String = LocalDateTime.now().format(DateTimeFormatter.ISO_LOCAL_DATE_TIME),
    val duration: String = "00:30:00",
    val category: String = "",
    val description: String = ""
) {
    fun getDurationMinutes(): Double {
        return try {
            val parts = duration.split(":")
            val hours = parts.getOrNull(0)?.toDoubleOrNull() ?: 0.0
            val mins = parts.getOrNull(1)?.toDoubleOrNull() ?: 0.0
            val secs = parts.getOrNull(2)?.toDoubleOrNull() ?: 0.0
            hours * 60.0 + mins + (secs / 60.0)
        } catch (e: Exception) {
            30.0
        }
    }

    fun getDurationHours(): Double = getDurationMinutes() / 60.0

    fun toLocalDateTime(): LocalDateTime {
        if (timestamp.isBlank()) return LocalDateTime.now()

        // 1. If timestamp has zone offset or Z (e.g., 2026-09-21T21:40:00Z or +01:00)
        try {
            return OffsetDateTime.parse(timestamp).atZoneSameInstant(ZoneId.systemDefault()).toLocalDateTime()
        } catch (e: Exception) { }

        // 2. Try Instant
        try {
            return Instant.parse(timestamp).atZone(ZoneId.systemDefault()).toLocalDateTime()
        } catch (e: Exception) { }

        // 3. Try standard ISO Local Date Time (e.g. 2026-09-21T21:40:00)
        try {
            return LocalDateTime.parse(timestamp, DateTimeFormatter.ISO_DATE_TIME)
        } catch (e: Exception) { }

        try {
            return LocalDateTime.parse(timestamp, DateTimeFormatter.ISO_LOCAL_DATE_TIME)
        } catch (e: Exception) { }

        // 4. Try normalized string
        try {
            val clean = timestamp.trim().trimEnd('Z').replace(' ', 'T')
            return LocalDateTime.parse(clean)
        } catch (e: Exception) { }

        return LocalDateTime.now()
    }

    fun getDisplayTime(): String {
        return try {
            val ldt = toLocalDateTime()
            if (ldt.toLocalDate() == LocalDate.now()) {
                ldt.format(DateTimeFormatter.ofPattern("h:mm a"))
            } else {
                ldt.format(DateTimeFormatter.ofPattern("MMM d, h:mm a"))
            }
        } catch (e: Exception) {
            timestamp
        }
    }

    fun isToday(): Boolean {
        return try {
            val ldt = toLocalDateTime()
            ldt.toLocalDate() == LocalDate.now()
        } catch (e: Exception) {
            false
        }
    }
}
