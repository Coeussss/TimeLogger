package com.example.worktimetracker.ui

import android.app.Application
import android.net.Uri
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.example.worktimetracker.data.GoogleDriveSyncManager
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

class TimeTrackerViewModel(application: Application) : AndroidViewModel(application) {

    private val syncManager = GoogleDriveSyncManager(application)
    private val notificationHelper = IntervalNotificationHelper(application)

    private val _timeRemaining = MutableStateFlow(1800) // 30 mins in seconds
    val timeRemaining: StateFlow<Int> = _timeRemaining.asStateFlow()

    private val _isRunning = MutableStateFlow(true)
    val isRunning: StateFlow<Boolean> = _isRunning.asStateFlow()

    private val _allLogs = MutableStateFlow<List<TimeLog>>(emptyList())
    val allLogs: StateFlow<List<TimeLog>> = _allLogs.asStateFlow()

    private val _showPromptDialog = MutableStateFlow(false)
    val showPromptDialog: StateFlow<Boolean> = _showPromptDialog.asStateFlow()

    private val _isDriveLinked = MutableStateFlow(syncManager.isLinked())
    val isDriveLinked: StateFlow<Boolean> = _isDriveLinked.asStateFlow()

    private val _driveFileName = MutableStateFlow(syncManager.getLinkedFileName())
    val driveFileName: StateFlow<String> = _driveFileName.asStateFlow()

    private var timerJob: Job? = null

    init {
        refreshLogs()
        startTimer()
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
            syncManager.saveLogs(updated)
        }
    }

    fun deleteLog(id: String) {
        val updated = _allLogs.value.filter { it.id != id }
        _allLogs.value = updated
        viewModelScope.launch {
            syncManager.saveLogs(updated)
        }
    }

    fun linkDriveFile(uri: Uri, name: String = "timelogs.json") {
        syncManager.saveLinkedUri(uri, name)
        _isDriveLinked.value = syncManager.isLinked()
        _driveFileName.value = name
        refreshLogs()
    }

    fun unlinkDrive() {
        syncManager.unlink()
        _isDriveLinked.value = false
        _driveFileName.value = "Local Cache"
    }

    fun refreshLogs() {
        viewModelScope.launch {
            val loaded = syncManager.loadLogs()
            _allLogs.value = loaded
            _isDriveLinked.value = syncManager.isLinked()
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
