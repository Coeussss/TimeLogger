package com.example.worktimetracker.data

import android.content.Context
import android.content.Intent
import android.net.Uri
import com.example.worktimetracker.model.TimeLog
import org.json.JSONArray
import org.json.JSONObject
import java.io.File

class GoogleDriveSyncManager(private val context: Context) {

    private val prefs = context.getSharedPreferences("google_drive_sync_prefs", Context.MODE_PRIVATE)
    private val localCacheFile = File(context.filesDir, "timelogs_cache.json")

    companion object {
        private const val KEY_LINKED_URI = "linked_file_uri"
        private const val KEY_LINKED_NAME = "linked_file_name"
    }

    fun isLinked(): Boolean {
        val uriStr = prefs.getString(KEY_LINKED_URI, null) ?: return false
        return try {
            val uri = Uri.parse(uriStr)
            val flags = context.contentResolver.persistedUriPermissions
            flags.any { it.uri == uri }
        } catch (e: Exception) {
            false
        }
    }

    fun getLinkedFileName(): String {
        return prefs.getString(KEY_LINKED_NAME, "timelogs.json") ?: "timelogs.json"
    }

    fun saveLinkedUri(uri: Uri, name: String = "timelogs.json") {
        try {
            val takeFlags = Intent.FLAG_GRANT_READ_URI_PERMISSION or Intent.FLAG_GRANT_WRITE_URI_PERMISSION
            context.contentResolver.takePersistableUriPermission(uri, takeFlags)
        } catch (e: Exception) {
            e.printStackTrace()
        }

        prefs.edit()
            .putString(KEY_LINKED_URI, uri.toString())
            .putString(KEY_LINKED_NAME, name)
            .apply()
    }

    fun unlink() {
        prefs.edit().remove(KEY_LINKED_URI).remove(KEY_LINKED_NAME).apply()
    }

    fun loadLogs(): List<TimeLog> {
        val uriStr = prefs.getString(KEY_LINKED_URI, null)
        if (!uriStr.isNullOrBlank()) {
            try {
                val uri = Uri.parse(uriStr)
                context.contentResolver.openInputStream(uri)?.use { stream ->
                    val json = stream.bufferedReader().readText()
                    val parsed = parseJson(json)
                    // Update local cache
                    saveToLocalCache(json)
                    return parsed
                }
            } catch (e: Exception) {
                e.printStackTrace()
            }
        }

        // Fallback to local cache
        return loadFromLocalCache()
    }

    fun saveLogs(logs: List<TimeLog>): Boolean {
        val json = serializeToJson(logs)
        saveToLocalCache(json)

        val uriStr = prefs.getString(KEY_LINKED_URI, null)
        if (!uriStr.isNullOrBlank()) {
            return try {
                val uri = Uri.parse(uriStr)
                context.contentResolver.openOutputStream(uri, "wt")?.use { stream ->
                    stream.bufferedWriter().use { writer ->
                        writer.write(json)
                        writer.flush()
                    }
                }
                true
            } catch (e: Exception) {
                e.printStackTrace()
                false
            }
        }
        return false
    }

    private fun loadFromLocalCache(): List<TimeLog> {
        if (!localCacheFile.exists()) return emptyList()
        return try {
            val json = localCacheFile.readText()
            parseJson(json)
        } catch (e: Exception) {
            emptyList()
        }
    }

    private fun saveToLocalCache(json: String) {
        try {
            localCacheFile.writeText(json)
        } catch (e: Exception) {
            e.printStackTrace()
        }
    }

    private fun parseJson(json: String): List<TimeLog> {
        if (json.isBlank()) return emptyList()
        val list = mutableListOf<TimeLog>()
        try {
            val arr = JSONArray(json)
            for (i in 0 until arr.length()) {
                val obj = arr.getJSONObject(i)
                val id = obj.optString("Id", obj.optString("id", ""))
                val timestamp = obj.optString("Timestamp", obj.optString("timestamp", ""))
                val duration = obj.optString("Duration", obj.optString("duration", "00:30:00"))
                val category = obj.optString("Category", obj.optString("category", ""))
                val description = obj.optString("Description", obj.optString("description", ""))

                if (id.isNotBlank()) {
                    list.add(TimeLog(id, timestamp, duration, category, description))
                }
            }
        } catch (e: Exception) {
            e.printStackTrace()
        }
        return list
    }

    private fun serializeToJson(logs: List<TimeLog>): String {
        val arr = JSONArray()
        for (log in logs) {
            val obj = JSONObject()
            obj.put("Id", log.id)
            obj.put("Timestamp", log.timestamp)
            obj.put("Duration", log.duration)
            obj.put("Category", log.category)
            obj.put("Description", log.description)
            arr.put(obj)
        }
        return arr.toString(2)
    }
}
