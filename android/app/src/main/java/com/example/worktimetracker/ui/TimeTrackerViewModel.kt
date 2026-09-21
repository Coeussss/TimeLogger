package com.example.worktimetracker.ui

import android.app.Application
import android.content.Context
import android.net.Uri
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.example.worktimetracker.data.GoogleDriveSyncManager
import com.example.worktimetracker.data.ServerSyncManager
import com.example.worktimetracker.model.TimeLog
import com.example.worktimetracker.service.IntervalNotificationHelper
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import java.time.LocalDateTime
import java.time.format.DateTimeFormatter
import java.util.UUID

enum class AndroidSyncMode {
    LOCAL,
    GOOGLE_DRIVE,
    HOME_SERVER
}

class TimeTrackerViewModel(application: Application) : AndroidViewModel(application) {

    private val prefs = application.getSharedPreferences("app_sync_prefs", Context.MODE_PRIVATE)
    private val driveSyncManager = GoogleDriveSyncManager(application)
    val serverSyncManager = ServerSyncManager(application)
    private val notificationHelper = IntervalNotificationHelper(application)

    private val _timeRemaining = MutableStateFlow(1800) // 30 mins in seconds
    val timeRemaining: StateFlow<Int> = _timeRemaining.asStateFlow()

    private val _isRunning = MutableStateFlow(true)
    val isRunning: StateFlow<Boolean> = _isRunning.asStateFlow()

    private val _allLogs = MutableStateFlow<List<TimeLog>>(emptyList())
    val allLogs: StateFlow<List<TimeLog>> = _allLogs.asStateFlow()

    private val _showPromptDialog = MutableStateFlow(false)
    val showPromptDialog: StateFlow<Boolean> = _showPromptDialog.asStateFlow()

    private val _showSyncSettingsDialog = MutableStateFlow(false)
    val showSyncSettingsDialog: StateFlow<Boolean> = _showSyncSettingsDialog.asStateFlow()

    private val _syncMode = MutableStateFlow(loadInitialSyncMode())
    val syncMode: StateFlow<AndroidSyncMode> = _syncMode.asStateFlow()

    private val _isDriveLinked = MutableStateFlow(driveSyncManager.isLinked())
    val isDriveLinked: StateFlow<Boolean> = _isDriveLinked.asStateFlow()

    private val _driveFileName = MutableStateFlow(driveSyncManager.getLinkedFileName())
    val driveFileName: StateFlow<String> = _driveFileName.asStateFlow()

    private val _serverUrl = MutableStateFlow(serverSyncManager.getServerUrl() ?: "")
    val serverUrl: StateFlow<String> = _serverUrl.asStateFlow()

    private val _serverSyncStatus = MutableStateFlow("Ready")
    val serverSyncStatus: StateFlow<String> = _serverSyncStatus.asStateFlow()

    private var timerJob: Job? = null

    init {
        refreshLogs()
        startTimer()
    }

    private fun loadInitialSyncMode(): AndroidSyncMode {
        val saved = prefs.getString("sync_mode", null)
        if (saved != null) {
            return try { AndroidSyncMode.valueOf(saved) } catch (e: Exception) { AndroidSyncMode.LOCAL }
        }
        return if (serverSyncManager.isConfigured()) {
            AndroidSyncMode.HOME_SERVER
        } else if (driveSyncManager.isLinked()) {
            AndroidSyncMode.GOOGLE_DRIVE
        } else {
            AndroidSyncMode.LOCAL
        }
    }

    private fun saveSyncMode(mode: AndroidSyncMode) {
        _syncMode.value = mode
        prefs.edit().putString("sync_mode", mode.name).apply()
    }

    fun startTimer() {
        _isRunning.value = true
        timerJob?.cancel()
        timerJob = viewModelScope.launch {
            while (isActive) {
                delay(1000)
                if (_isRunning.value) {
                    if (_timeRemaining.value > 1) {
                        _timeRemaining.value -= 1
                    } else {
                        // Interval completed!
                        _timeRemaining.value = 1800
                        _showPromptDialog.value = true
                        notificationHelper.showCheckInNotification()
                    }
                }
            }
        }
    }

    fun pauseTimer() {
        _isRunning.value = false
    }

    fun resetTimer() {
        _timeRemaining.value = 1800
        _isRunning.value = false
    }

    fun openPromptDialog() {
        _showPromptDialog.value = true
    }

    fun dismissPromptDialog() {
        _showPromptDialog.value = false
    }

    fun openSyncSettings() {
        _showSyncSettingsDialog.value = true
    }

    fun dismissSyncSettings() {
        _showSyncSettingsDialog.value = false
    }

    fun snooze5Minutes() {
        _timeRemaining.value = 300 // 5 mins
        _isRunning.value = true
        _showPromptDialog.value = false
    }

    fun recordLog(category: String, description: String, minutes: Int = 30) {
        val durationStr = String.format("%02d:%02d:00", minutes / 60, minutes % 60)
        val newLog = TimeLog(
            id = UUID.randomUUID().toString(),
            timestamp = LocalDateTime.now().format(DateTimeFormatter.ISO_LOCAL_DATE_TIME),
            duration = durationStr,
            category = category,
            description = description
        )

        val updated = listOf(newLog) + _allLogs.value
        _allLogs.value = updated
        _showPromptDialog.value = false

        viewModelScope.launch {
            when (_syncMode.value) {
                AndroidSyncMode.HOME_SERVER -> {
                    serverSyncManager.saveLogsToLocalCache(updated)
                    _serverSyncStatus.value = "Syncing..."
                    val res = serverSyncManager.syncLogs(updated)
                    if (res.isSuccess) {
                        _allLogs.value = res.getOrNull() ?: updated
                        _serverSyncStatus.value = "Synced ✓"
                    } else {
                        _serverSyncStatus.value = "Offline (Cached)"
                    }
                }
                AndroidSyncMode.GOOGLE_DRIVE -> {
                    driveSyncManager.saveLogs(updated)
                }
                AndroidSyncMode.LOCAL -> {
                    serverSyncManager.saveLogsToLocalCache(updated)
                }
            }
        }
    }

    fun deleteLog(id: String) {
        val updated = _allLogs.value.filter { it.id != id }
        _allLogs.value = updated

        viewModelScope.launch {
            when (_syncMode.value) {
                AndroidSyncMode.HOME_SERVER -> {
                    serverSyncManager.saveLogsToLocalCache(updated)
                    serverSyncManager.deleteLogOnServer(id)
                    val res = serverSyncManager.syncLogs(updated)
                    if (res.isSuccess) {
                        _allLogs.value = res.getOrNull() ?: updated
                    }
                }
                AndroidSyncMode.GOOGLE_DRIVE -> {
                    driveSyncManager.saveLogs(updated)
                }
                AndroidSyncMode.LOCAL -> {
                    serverSyncManager.saveLogsToLocalCache(updated)
                }
            }
        }
    }

    fun configureHomeServer(url: String, apiKey: String) {
        serverSyncManager.saveConfig(url, apiKey)
        _serverUrl.value = url.trim().trimEnd('/')
        saveSyncMode(AndroidSyncMode.HOME_SERVER)
        refreshLogs()
    }

    fun linkDriveFile(uri: Uri, name: String = "timelogs.json") {
        driveSyncManager.saveLinkedUri(uri, name)
        _isDriveLinked.value = driveSyncManager.isLinked()
        _driveFileName.value = name
        saveSyncMode(AndroidSyncMode.GOOGLE_DRIVE)
        refreshLogs()
    }

    fun setLocalOnly() {
        saveSyncMode(AndroidSyncMode.LOCAL)
        refreshLogs()
    }

    fun refreshLogs() {
        viewModelScope.launch {
            when (_syncMode.value) {
                AndroidSyncMode.HOME_SERVER -> {
                    val cached = serverSyncManager.loadFromLocalCache()
                    if (cached.isNotEmpty()) {
                        _allLogs.value = cached
                    }
                    _serverSyncStatus.value = "Syncing..."
                    val res = serverSyncManager.syncLogs(_allLogs.value)
                    if (res.isSuccess) {
                        _allLogs.value = res.getOrNull() ?: _allLogs.value
                        _serverSyncStatus.value = "Synced ✓"
                    } else {
                        _serverSyncStatus.value = "Offline (Cached)"
                    }
                }
                AndroidSyncMode.GOOGLE_DRIVE -> {
                    val loaded = driveSyncManager.loadLogs()
                    _allLogs.value = loaded
                    _isDriveLinked.value = driveSyncManager.isLinked()
                }
                AndroidSyncMode.LOCAL -> {
                    val cached = serverSyncManager.loadFromLocalCache()
                    _allLogs.value = cached
                }
            }
        }
    }

    fun getTodayLogs(): List<TimeLog> {
        return _allLogs.value.filter { it.isToday() }
    }

    fun getTodayTotalHours(): Double {
        return getTodayLogs().sumOf { it.getDurationHours() }
    }

    fun formatCountdown(seconds: Int): String {
        val m = seconds / 60
        val s = seconds % 60
        return String.format("%02d:%02d", m, s)
    }
}
