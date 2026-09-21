package com.example.worktimetracker.data

import android.content.Context
import com.example.worktimetracker.model.TimeLog
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONArray
import org.json.JSONObject
import java.io.File
import java.net.HttpURLConnection
import java.net.URL

class ServerSyncManager(private val context: Context) {

    private val prefs = context.getSharedPreferences("server_sync_prefs", Context.MODE_PRIVATE)
    private val localCacheFile = File(context.filesDir, "timelogs_cache.json")

    companion object {
        private const val KEY_SERVER_URL = "server_url"
        private const val KEY_API_KEY = "api_key"
        private const val TIMEOUT_MS = 6000
    }

    fun isConfigured(): Boolean {
        val url = getServerUrl()
        return !url.isNullOrBlank()
    }

    fun getServerUrl(): String? = prefs.getString(KEY_SERVER_URL, null)

    fun getApiKey(): String = prefs.getString(KEY_API_KEY, "") ?: ""

    fun saveConfig(serverUrl: String, apiKey: String) {
        prefs.edit()
            .putString(KEY_SERVER_URL, serverUrl.trim().trimEnd('/'))
            .putString(KEY_API_KEY, apiKey.trim())
            .apply()
    }

    fun clearConfig() {
        prefs.edit().clear().apply()
    }

    suspend fun testConnection(serverUrl: String, apiKey: String): Result<String> = withContext(Dispatchers.IO) {
        try {
            val cleanUrl = serverUrl.trim().trimEnd('/')
            if (cleanUrl.isBlank()) return@withContext Result.failure(Exception("Server URL is empty"))

            // 1. Health check
            val healthUrl = URL("$cleanUrl/api/health")
            val healthConn = (healthUrl.openConnection() as HttpURLConnection).apply {
                connectTimeout = TIMEOUT_MS
                readTimeout = TIMEOUT_MS
                requestMethod = "GET"
            }

            val healthCode = healthConn.responseCode
            healthConn.disconnect()

            if (healthCode !in 200..299) {
                return@withContext Result.failure(Exception("Health check returned status $healthCode"))
            }

            // 2. Auth check
            val logsUrl = URL("$cleanUrl/api/logs")
            val logsConn = (logsUrl.openConnection() as HttpURLConnection).apply {
                connectTimeout = TIMEOUT_MS
                readTimeout = TIMEOUT_MS
                requestMethod = "GET"
                if (apiKey.isNotBlank()) {
                    setRequestProperty("X-API-Key", apiKey.trim())
                }
            }

            val logsCode = logsConn.responseCode
            logsConn.disconnect()

            if (logsCode == 401) {
                return@withContext Result.failure(Exception("Server reached, but API Key was rejected (401 Unauthorized)"))
            } else if (logsCode !in 200..299) {
                return@withContext Result.failure(Exception("Server returned status $logsCode"))
            }

            Result.success("Successfully connected to WorkTimeTracker Docker Server!")
        } catch (e: Exception) {
            Result.failure(Exception("Connection failed: ${e.localizedMessage ?: e.message}"))
        }
    }

    suspend fun syncLogs(clientLogs: List<TimeLog>): Result<List<TimeLog>> = withContext(Dispatchers.IO) {
        val serverUrl = getServerUrl()
        if (serverUrl.isNullOrBlank()) {
            return@withContext Result.failure(Exception("Server URL not configured"))
        }

        try {
            val syncUrl = URL("$serverUrl/api/logs/sync")
            val conn = (syncUrl.openConnection() as HttpURLConnection).apply {
                connectTimeout = TIMEOUT_MS
                readTimeout = TIMEOUT_MS
                requestMethod = "POST"
                setRequestProperty("Content-Type", "application/json; charset=utf-8")
                val key = getApiKey()
                if (key.isNotBlank()) {
                    setRequestProperty("X-API-Key", key)
                }
                doOutput = true
            }

            val jsonPayload = serializeToJson(clientLogs)
            conn.outputStream.use { os ->
                os.write(jsonPayload.toByteArray(Charsets.UTF_8))
                os.flush()
            }

            val responseCode = conn.responseCode
            if (responseCode in 200..299) {
                val responseJson = conn.inputStream.bufferedReader().use { it.readText() }
                conn.disconnect()

                val mergedLogs = parseJson(responseJson)
                saveToLocalCache(responseJson)
                Result.success(mergedLogs)
            } else {
                conn.disconnect()
                Result.failure(Exception("Sync failed with HTTP $responseCode"))
            }
        } catch (e: Exception) {
            Result.failure(e)
        }
    }

    suspend fun deleteLogOnServer(id: String): Result<Boolean> = withContext(Dispatchers.IO) {
        val serverUrl = getServerUrl() ?: return@withContext Result.failure(Exception("Server not configured"))

        try {
            val delUrl = URL("$serverUrl/api/logs/$id")
            val conn = (delUrl.openConnection() as HttpURLConnection).apply {
                connectTimeout = TIMEOUT_MS
                readTimeout = TIMEOUT_MS
                requestMethod = "DELETE"
                val key = getApiKey()
                if (key.isNotBlank()) {
                    setRequestProperty("X-API-Key", key)
                }
            }

            val code = conn.responseCode
            conn.disconnect()
            Result.success(code in 200..299)
        } catch (e: Exception) {
            Result.failure(e)
        }
    }

    fun loadFromLocalCache(): List<TimeLog> {
        if (!localCacheFile.exists()) return emptyList()
        return try {
            val json = localCacheFile.readText()
            parseJson(json)
        } catch (e: Exception) {
            emptyList()
        }
    }

    fun saveToLocalCache(json: String) {
        try {
            localCacheFile.writeText(json)
        } catch (e: Exception) {
            e.printStackTrace()
        }
    }

    fun saveLogsToLocalCache(logs: List<TimeLog>) {
        saveToLocalCache(serializeToJson(logs))
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
