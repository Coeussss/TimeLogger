package com.example.worktimetracker.model

import java.time.LocalDateTime
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

    fun getDisplayTime(): String {
        return try {
            val ldt = LocalDateTime.parse(timestamp, DateTimeFormatter.ISO_LOCAL_DATE_TIME)
            ldt.format(DateTimeFormatter.ofPattern("h:mm a"))
        } catch (e: Exception) {
            timestamp
        }
    }

    fun isToday(): Boolean {
        return try {
            val logDate = LocalDateTime.parse(timestamp, DateTimeFormatter.ISO_LOCAL_DATE_TIME).toLocalDate()
            logDate == java.time.LocalDate.now()
        } catch (e: Exception) {
            false
        }
    }
}
