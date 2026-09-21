package com.example.worktimetracker.ui.screens

import android.net.Uri
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape

import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.worktimetracker.model.Categories
import com.example.worktimetracker.model.TimeLog
import com.example.worktimetracker.ui.AndroidSyncMode
import com.example.worktimetracker.ui.TimeTrackerViewModel
import com.example.worktimetracker.ui.dialogs.PromptDialog
import com.example.worktimetracker.ui.dialogs.SyncSettingsDialog

@Composable
fun HomeScreen(viewModel: TimeTrackerViewModel) {
    val timeRemaining by viewModel.timeRemaining.collectAsState()
    val isRunning by viewModel.isRunning.collectAsState()
    val showPromptDialog by viewModel.showPromptDialog.collectAsState()
    val isDriveLinked by viewModel.isDriveLinked.collectAsState()
    val driveFileName by viewModel.driveFileName.collectAsState()
    val allLogs by viewModel.allLogs.collectAsState()
    val syncMode by viewModel.syncMode.collectAsState()
    val showSyncSettingsDialog by viewModel.showSyncSettingsDialog.collectAsState()
    val serverUrl by viewModel.serverUrl.collectAsState()
    val serverSyncStatus by viewModel.serverSyncStatus.collectAsState()
    val isRefreshing by viewModel.isRefreshing.collectAsState()

    val todayLogs = remember(allLogs) { allLogs.filter { it.isToday() } }
    val todayHours = remember(todayLogs) { todayLogs.sumOf { it.getDurationHours() } }
    val targetMet = todayHours >= 7.5

    // Document Picker for linking Google Drive file
    val driveFilePicker = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.OpenDocument()
    ) { uri: Uri? ->
        if (uri != null) {
            viewModel.linkDriveFile(uri, "timelogs.json")
        }
    }

    Scaffold(
        containerColor = Color(0xFF121212)
    ) { paddingValues ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
                .padding(horizontal = 16.dp)
        ) {
            // Header Bar
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(vertical = 12.dp),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Box(
                        modifier = Modifier
                            .size(32.dp)
                            .background(Color(0xFF0078D4), RoundedCornerShape(6.dp)),
                        contentAlignment = Alignment.Center
                    ) {
                        Text("⏱️", fontSize = 16.sp)
                    }
                    Spacer(modifier = Modifier.width(10.dp))
                    Column {
                        Text("Work Time Tracker", fontWeight = FontWeight.Bold, fontSize = 15.sp, color = Color.White)
                        Text("Dev & Support Logger", fontSize = 11.sp, color = Color(0xFF888888))
                    }
                }

                // Sync Settings Pill Button
                val syncLabel = when (syncMode) {
                    AndroidSyncMode.HOME_SERVER -> if (serverSyncStatus.contains("Synced")) "🟢 Server" else "🟡 Server"
                    AndroidSyncMode.GOOGLE_DRIVE -> if (isDriveLinked) "☁️ Drive" else "☁️ Link Drive"
                    AndroidSyncMode.LOCAL -> "⚙ Sync"
                }
                val syncBg = when (syncMode) {
                    AndroidSyncMode.HOME_SERVER -> if (serverSyncStatus.contains("Synced")) Color(0xFF173121) else Color(0xFF2E2417)
                    AndroidSyncMode.GOOGLE_DRIVE -> if (isDriveLinked) Color(0xFF173121) else Color(0xFF242424)
                    AndroidSyncMode.LOCAL -> Color(0xFF242424)
                }
                val syncBorder = when (syncMode) {
                    AndroidSyncMode.HOME_SERVER -> if (serverSyncStatus.contains("Synced")) Color(0xFF107C41) else Color(0xFFFFAA44)
                    AndroidSyncMode.GOOGLE_DRIVE -> if (isDriveLinked) Color(0xFF107C41) else Color(0xFF383838)
                    AndroidSyncMode.LOCAL -> Color(0xFF383838)
                }
                val syncColor = when (syncMode) {
                    AndroidSyncMode.HOME_SERVER -> if (serverSyncStatus.contains("Synced")) Color(0xFF64DB8F) else Color(0xFFFFAA44)
                    AndroidSyncMode.GOOGLE_DRIVE -> if (isDriveLinked) Color(0xFF64DB8F) else Color(0xFF4CC2FF)
                    AndroidSyncMode.LOCAL -> Color(0xFFCCCCCC)
                }

                Box(
                    modifier = Modifier
                        .background(syncBg, RoundedCornerShape(20.dp))
                        .border(1.dp, syncBorder, RoundedCornerShape(20.dp))
                        .clickable {
                            viewModel.openSyncSettings()
                        }
                        .padding(horizontal = 10.dp, vertical = 6.dp)
                ) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Text(syncLabel, fontSize = 11.sp, color = syncColor, fontWeight = FontWeight.SemiBold)
                    }
                }
            }

            LazyColumn(
                modifier = Modifier.fillMaxSize(),
                verticalArrangement = Arrangement.spacedBy(14.dp)
            ) {
                // Item 1: Large Active Timer Card
                item {
                    Card(
                        modifier = Modifier.fillMaxWidth(),
                        colors = CardDefaults.cardColors(containerColor = Color(0xFF1A1A1A)),
                        shape = RoundedCornerShape(12.dp),
                        border = androidx.compose.foundation.BorderStroke(1.dp, Color(0xFF2E2E2E))
                    ) {
                        Column(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(16.dp),
                            horizontalAlignment = Alignment.CenterHorizontally
                        ) {
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.SpaceBetween,
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                Row(verticalAlignment = Alignment.CenterVertically) {
                                    Box(
                                        modifier = Modifier
                                            .size(8.dp)
                                            .background(if (isRunning) Color(0xFF4CC2FF) else Color(0xFFFFAA44), CircleShape)
                                    )
                                    Spacer(modifier = Modifier.width(6.dp))
                                    Text(
                                        if (isRunning) "INTERVAL ACTIVE" else "TIMER PAUSED",
                                        fontSize = 11.sp,
                                        fontWeight = FontWeight.Bold,
                                        color = if (isRunning) Color(0xFF4CC2FF) else Color(0xFFFFAA44)
                                    )
                                }

                                Text("30-Min Block", fontSize = 11.sp, color = Color(0xFF888888))
                            }

                            Spacer(modifier = Modifier.height(10.dp))

                            // Big Countdown
                            Text(
                                text = viewModel.formatCountdown(timeRemaining),
                                fontSize = 48.sp,
                                fontWeight = FontWeight.Bold,
                                fontFamily = FontFamily.Monospace,
                                color = Color.White
                            )

                            // Slim Progress Bar
                            val progress = (1800 - timeRemaining) / 1800f
                            LinearProgressIndicator(
                                progress = { progress },
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .height(4.dp)
                                    .padding(vertical = 4.dp),
                                color = Color(0xFF0078D4),
                                trackColor = Color(0xFF282828),
                            )

                            Spacer(modifier = Modifier.height(14.dp))

                            // Controls Row
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.spacedBy(8.dp)
                            ) {
                                Button(
                                    onClick = { if (isRunning) viewModel.pauseTimer() else viewModel.startTimer() },
                                    modifier = Modifier.weight(1f),
                                    shape = RoundedCornerShape(8.dp),
                                    colors = ButtonDefaults.buttonColors(
                                        containerColor = if (isRunning) Color(0xFF2A2A2A) else Color(0xFF0078D4),
                                        contentColor = Color.White
                                    )
                                ) {
                                    Text(if (isRunning) "⏸️ Pause" else "▶️ Start", fontSize = 12.sp, fontWeight = FontWeight.SemiBold)
                                }

                                OutlinedButton(
                                    onClick = { viewModel.resetTimer() },
                                    shape = RoundedCornerShape(8.dp),
                                    colors = ButtonDefaults.outlinedButtonColors(containerColor = Color(0xFF222222), contentColor = Color(0xFFAAAAAA)),
                                    border = androidx.compose.foundation.BorderStroke(1.dp, Color(0xFF383838))
                                ) {
                                    Text("↺ Reset", fontSize = 12.sp)
                                }

                                Button(
                                    onClick = { viewModel.openPromptDialog() },
                                    modifier = Modifier.weight(1.3f),
                                    shape = RoundedCornerShape(8.dp),
                                    colors = ButtonDefaults.buttonColors(containerColor = Color(0xFF0078D4), contentColor = Color.White)
                                ) {
                                    Text("+ Check-in", fontSize = 12.sp, fontWeight = FontWeight.Bold)
                                }
                            }
                        }
                    }
                }

                // Item 2: 7.5-Hour Target Card
                item {
                    Card(
                        modifier = Modifier.fillMaxWidth(),
                        colors = CardDefaults.cardColors(
                            containerColor = if (targetMet) Color(0xFF142E1E) else Color(0xFF231B10)
                        ),
                        shape = RoundedCornerShape(12.dp),
                        border = androidx.compose.foundation.BorderStroke(
                            1.dp,
                            if (targetMet) Color(0xFF1F5C37) else Color(0xFF4A381C)
                        )
                    ) {
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(14.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Box(
                                modifier = Modifier
                                    .size(40.dp)
                                    .background(
                                        if (targetMet) Color(0xFF107C41) else Color(0xFF382A14),
                                        CircleShape
                                    ),
                                contentAlignment = Alignment.Center
                            ) {
                                Text(
                                    text = if (targetMet) "✓" else "⏱️",
                                    fontSize = 18.sp,
                                    fontWeight = FontWeight.Bold,
                                    color = if (targetMet) Color(0xFF57D9A3) else Color(0xFFFFAA44)
                                )
                            }

                            Spacer(modifier = Modifier.width(12.dp))

                            Column(modifier = Modifier.weight(1f)) {
                                Text(
                                    text = if (targetMet) "✓ 7.5h Target Achieved!" else "Daily 7.5h Goal in Progress",
                                    fontWeight = FontWeight.Bold,
                                    fontSize = 13.sp,
                                    color = if (targetMet) Color(0xFF57D9A3) else Color(0xFFFFAA44)
                                )
                                val formattedHours = String.format(java.util.Locale.US, "%.1f", todayHours)
                                Text(
                                    text = "Today: ${formattedHours}h of 7.5h target (${todayLogs.size} blocks)",
                                    fontSize = 11.sp,
                                    color = Color(0xFFCCCCCC)
                                )
                                Spacer(modifier = Modifier.height(4.dp))
                                val targetProgress = (todayHours / 7.5f).coerceIn(0.0, 1.0).toFloat()
                                LinearProgressIndicator(
                                    progress = { targetProgress },
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .height(4.dp),
                                    color = if (targetMet) Color(0xFF57D9A3) else Color(0xFFFFAA44),
                                    trackColor = Color(0xFF282828)
                                )
                            }
                        }
                    }
                }

                // Item 3: Quick Category Shortcuts
                item {
                    Text(
                        "QUICK CHECK-IN CATEGORIES",
                        fontSize = 11.sp,
                        fontWeight = FontWeight.Bold,
                        color = Color(0xFF888888)
                    )
                    Spacer(modifier = Modifier.height(6.dp))
                    LazyRow(
                        horizontalArrangement = Arrangement.spacedBy(8.dp),
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        items(Categories.quickCategories) { cat ->
                            Box(
                                modifier = Modifier
                                    .background(Color(0xFF1E1E1E), RoundedCornerShape(8.dp))
                                    .border(1.dp, cat.color.copy(alpha = 0.4f), RoundedCornerShape(8.dp))
                                    .clickable {
                                        viewModel.openPromptDialog()
                                    }
                                    .padding(horizontal = 10.dp, vertical = 7.dp)
                            ) {
                                Row(verticalAlignment = Alignment.CenterVertically) {
                                    Text(cat.icon, fontSize = 12.sp)
                                    Spacer(modifier = Modifier.width(5.dp))
                                    Text(cat.name, fontSize = 11.sp, color = cat.color, fontWeight = FontWeight.SemiBold)
                                }
                            }
                        }
                    }
                }

                // Item 4: Today's Logs Header
                item {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(top = 4.dp),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text(
                            "TODAY'S LOGGED BLOCKS (${todayLogs.size})",
                            fontSize = 11.sp,
                            fontWeight = FontWeight.Bold,
                            color = Color(0xFF888888)
                        )

                        IconButton(
                            onClick = { viewModel.refreshLogs(showToast = true) },
                            modifier = Modifier.size(36.dp),
                            enabled = !isRefreshing
                        ) {
                            if (isRefreshing) {
                                CircularProgressIndicator(
                                    modifier = Modifier.size(16.dp),
                                    strokeWidth = 2.dp,
                                    color = Color(0xFF0078D4)
                                )
                            } else {
                                Text("↺", color = Color(0xFFCCCCCC), fontSize = 18.sp, fontWeight = FontWeight.Bold)
                            }
                        }
                    }
                }

                if (todayLogs.isEmpty()) {
                    item {
                        Box(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(vertical = 24.dp),
                            contentAlignment = Alignment.Center
                        ) {
                            Text(
                                "No blocks logged yet today.\nTap Check-in or start the interval timer!",
                                color = Color(0xFF555555),
                                fontSize = 12.sp,
                                textAlign = androidx.compose.ui.text.style.TextAlign.Center
                            )
                        }
                    }
                } else {
                    items(todayLogs, key = { it.id }) { log ->
                        LogItemRow(log = log, onDelete = { viewModel.deleteLog(log.id) })
                    }
                }

                item {
                    Spacer(modifier = Modifier.height(30.dp))
                }
            }
        }

        // Check-in Dialog
        if (showPromptDialog) {
            PromptDialog(
                onDismiss = { viewModel.dismissPromptDialog() },
                onRecord = { cat, desc, mins ->
                    viewModel.recordLog(cat, desc, mins)
                },
                onSnooze = { viewModel.snooze5Minutes() }
            )
        }

        // Sync Settings Dialog
        if (showSyncSettingsDialog) {
            SyncSettingsDialog(
                initialMode = syncMode,
                currentServerUrl = serverUrl,
                currentApiKey = viewModel.serverSyncManager.getApiKey(),
                isDriveLinked = isDriveLinked,
                onDismiss = { viewModel.dismissSyncSettings() },
                onSelectDriveFile = {
                    viewModel.dismissSyncSettings()
                    driveFilePicker.launch(arrayOf("application/json", "*/*"))
                },
                onSaveHomeServer = { url, key ->
                    viewModel.configureHomeServer(url, key)
                },
                onSaveLocalOnly = {
                    viewModel.setLocalOnly()
                },
                onTestConnection = { url, key ->
                    viewModel.serverSyncManager.testConnection(url, key)
                }
            )
        }
    }
}

@Composable
fun LogItemRow(log: TimeLog, onDelete: () -> Unit) {
    val catColor = Categories.getColorForCategory(log.category)

    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = Color(0xFF1A1A1A)),
        shape = RoundedCornerShape(8.dp),
        border = androidx.compose.foundation.BorderStroke(1.dp, Color(0xFF282828))
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 12.dp, vertical = 10.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            // Category color bar
            Box(
                modifier = Modifier
                    .size(4.dp, 36.dp)
                    .background(catColor, RoundedCornerShape(2.dp))
            )

            Spacer(modifier = Modifier.width(10.dp))

            Column(modifier = Modifier.weight(1f)) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text(
                        text = log.category.ifBlank { "General Work" },
                        fontSize = 12.sp,
                        fontWeight = FontWeight.Bold,
                        color = catColor
                    )
                    Spacer(modifier = Modifier.width(8.dp))
                    Text(
                        text = "• ${log.getDisplayTime()}",
                        fontSize = 11.sp,
                        color = Color(0xFF888888)
                    )
                }

                if (log.description.isNotBlank()) {
                    Text(
                        text = log.description,
                        fontSize = 12.sp,
                        color = Color(0xFFDDDDDD),
                        maxLines = 2
                    )
                }
            }

            // Duration Pill
            Box(
                modifier = Modifier
                    .background(Color(0xFF242424), RoundedCornerShape(4.dp))
                    .padding(horizontal = 6.dp, vertical = 3.dp)
            ) {
                Text(
                    text = "${log.getDurationMinutes().toInt()}m",
                    fontSize = 11.sp,
                    fontWeight = FontWeight.Bold,
                    color = Color.White
                )
            }

            Spacer(modifier = Modifier.width(4.dp))

            IconButton(
                onClick = onDelete,
                modifier = Modifier.size(28.dp)
            ) {
                Text("✕", color = Color(0xFF888888), fontSize = 13.sp, fontWeight = FontWeight.Bold)
            }
        }
    }
}
