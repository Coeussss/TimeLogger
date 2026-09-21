package com.example.worktimetracker.model

import androidx.compose.ui.graphics.Color

data class CategoryItem(
    val name: String,
    val color: Color,
    val icon: String
)

object Categories {
    val quickCategories = listOf(
        CategoryItem("Coding & Dev", Color(0xFF4CC2FF), "💻"),
        CategoryItem("Prod Incidents & Triage", Color(0xFFFF6B68), "🚨"),
        CategoryItem("Prod Support & Monitoring", Color(0xFFFFAA44), "🎫"),
        CategoryItem("Bug Fixing", Color(0xFFFF6492), "🐛"),
        CategoryItem("Code Review & PRs", Color(0xFFB99BFF), "🔍"),
        CategoryItem("Deployments & Releases", Color(0xFF64DB8F), "🚀"),
        CategoryItem("Standups & Meetings", Color(0xFF8FA4FF), "👥")
    )

    val allCategories = listOf(
        "Feature Development & Coding",
        "Production Incident & Triage",
        "Prod Support & Monitoring",
        "Bug Fixing & Investigation",
        "Code Review & Pull Requests",
        "Deployments & Releases",
        "Architecture & Tech Design",
        "Standups, Meetings & Syncs",
        "User Inquiries & On-Call Admin",
        "Documentation & Knowledge Base",
        "Break / Other"
    )

    fun getColorForCategory(category: String): Color {
        val quick = quickCategories.firstOrNull { it.name.equals(category, ignoreCase = true) }
        if (quick != null) return quick.color

        return when {
            category.contains("Dev", true) || category.contains("Coding", true) -> Color(0xFF4CC2FF)
            category.contains("Incident", true) || category.contains("Triage", true) -> Color(0xFFFF6B68)
            category.contains("Support", true) || category.contains("Monitoring", true) -> Color(0xFFFFAA44)
            category.contains("Bug", true) -> Color(0xFFFF6492)
            category.contains("Review", true) || category.contains("PR", true) -> Color(0xFFB99BFF)
            category.contains("Deploy", true) || category.contains("Release", true) -> Color(0xFF64DB8F)
            category.contains("Standup", true) || category.contains("Meeting", true) -> Color(0xFF8FA4FF)
            category.contains("Architecture", true) || category.contains("Design", true) -> Color(0xFF00B7C3)
            category.contains("Doc", true) -> Color(0xFF4F6BED)
            else -> Color(0xFF8A8886)
        }
    }
}
