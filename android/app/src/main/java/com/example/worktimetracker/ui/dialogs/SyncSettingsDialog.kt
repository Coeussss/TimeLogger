package com.example.worktimetracker.ui.dialogs

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.RadioButton
import androidx.compose.material3.RadioButtonDefaults
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.worktimetracker.ui.AndroidSyncMode
import kotlinx.coroutines.launch

@Composable
fun SyncSettingsDialog(
    initialMode: AndroidSyncMode,
    currentServerUrl: String,
    currentApiKey: String,
    isDriveLinked: Boolean,
    onDismiss: () -> Unit,
    onSelectDriveFile: () -> Unit,
    onSaveHomeServer: (url: String, apiKey: String) -> Unit,
    onSaveLocalOnly: () -> Unit,
    onTestConnection: suspend (url: String, key: String) -> Result<String>
) {
    var selectedMode by remember { mutableStateOf(initialMode) }
    var serverUrl by remember { mutableStateOf(currentServerUrl) }
    var apiKey by remember { mutableStateOf(currentApiKey) }
    var testResultText by remember { mutableStateOf<String?>(null) }
    var isTesting by remember { mutableStateOf(false) }

    val coroutineScope = rememberCoroutineScope()

    AlertDialog(
        onDismissRequest = onDismiss,
        containerColor = Color(0xFF1E1E1E),
        titleContentColor = Color.White,
        textContentColor = Color(0xFFCCCCCC),
        shape = RoundedCornerShape(16.dp),
        title = {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Box(
                    modifier = Modifier
                        .size(30.dp)
                        .background(Color(0xFF0078D4), RoundedCornerShape(6.dp)),
                    contentAlignment = Alignment.Center
                ) {
                    Text("☁", color = Color.White, fontSize = 16.sp)
                }
                Spacer(modifier = Modifier.width(10.dp))
                Column {
                    Text("Sync Settings", fontSize = 16.sp, fontWeight = FontWeight.Bold, color = Color.White)
                    Text("Sync phone with desktop & server", fontSize = 11.sp, color = Color(0xFF888888))
                }
            }
        },
        text = {
            Column(modifier = Modifier.fillMaxWidth()) {

                // Option 1: Home Server (Docker)
                Card(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(vertical = 4.dp)
                        .clickable { selectedMode = AndroidSyncMode.HOME_SERVER },
                    colors = CardDefaults.cardColors(
                        containerColor = if (selectedMode == AndroidSyncMode.HOME_SERVER) Color(0xFF262E38) else Color(0xFF252525)
                    ),
                    shape = RoundedCornerShape(10.dp)
                ) {
                    Column(modifier = Modifier.padding(10.dp)) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            RadioButton(
                                selected = selectedMode == AndroidSyncMode.HOME_SERVER,
                                onClick = { selectedMode = AndroidSyncMode.HOME_SERVER },
                                colors = RadioButtonDefaults.colors(selectedColor = Color(0xFF4CC2FF))
                            )
                            Spacer(modifier = Modifier.width(6.dp))
                            Text(
                                "Home Server (Docker)",
                                fontSize = 13.sp,
                                fontWeight = FontWeight.SemiBold,
                                color = Color.White
                            )
                        }

                        if (selectedMode == AndroidSyncMode.HOME_SERVER) {
                            Spacer(modifier = Modifier.height(8.dp))
                            OutlinedTextField(
                                value = serverUrl,
                                onValueChange = { serverUrl = it; testResultText = null },
                                label = { Text("Server URL (e.g. http://ip:48291)", fontSize = 11.sp) },
                                singleLine = true,
                                modifier = Modifier.fillMaxWidth(),
                                colors = OutlinedTextFieldDefaults.colors(
                                    focusedTextColor = Color.White,
                                    unfocusedTextColor = Color.White,
                                    focusedBorderColor = Color(0xFF4CC2FF),
                                    unfocusedBorderColor = Color(0xFF555555)
                                )
                            )

                            Spacer(modifier = Modifier.height(6.dp))
                            OutlinedTextField(
                                value = apiKey,
                                onValueChange = { apiKey = it; testResultText = null },
                                label = { Text("API Key", fontSize = 11.sp) },
                                singleLine = true,
                                modifier = Modifier.fillMaxWidth(),
                                colors = OutlinedTextFieldDefaults.colors(
                                    focusedTextColor = Color.White,
                                    unfocusedTextColor = Color.White,
                                    focusedBorderColor = Color(0xFF4CC2FF),
                                    unfocusedBorderColor = Color(0xFF555555)
                                )
                            )

                            Spacer(modifier = Modifier.height(8.dp))
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                OutlinedButton(
                                    onClick = {
                                        isTesting = true
                                        testResultText = "Connecting..."
                                        coroutineScope.launch {
                                            val result = onTestConnection(serverUrl, apiKey)
                                            isTesting = false
                                            testResultText = if (result.isSuccess) {
                                                "✓ " + result.getOrNull()
                                            } else {
                                                "✗ " + (result.exceptionOrNull()?.message ?: "Failed")
                                            }
                                        }
                                    },
                                    enabled = !isTesting && serverUrl.isNotBlank(),
                                    shape = RoundedCornerShape(6.dp)
                                ) {
                                    Text(if (isTesting) "Testing..." else "Test Connection", fontSize = 11.sp)
                                }

                                if (testResultText != null) {
                                    Spacer(modifier = Modifier.width(8.dp))
                                    Text(
                                        testResultText!!,
                                        fontSize = 11.sp,
                                        color = if (testResultText!!.startsWith("✓")) Color(0xFF107C41) else Color(0xFFEA4335),
                                        maxLines = 2
                                    )
                                }
                            }
                        }
                    }
                }

                // Option 2: Google Drive
                Card(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(vertical = 4.dp)
                        .clickable { selectedMode = AndroidSyncMode.GOOGLE_DRIVE },
                    colors = CardDefaults.cardColors(
                        containerColor = if (selectedMode == AndroidSyncMode.GOOGLE_DRIVE) Color(0xFF262E38) else Color(0xFF252525)
                    ),
                    shape = RoundedCornerShape(10.dp)
                ) {
                    Column(modifier = Modifier.padding(10.dp)) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            RadioButton(
                                selected = selectedMode == AndroidSyncMode.GOOGLE_DRIVE,
                                onClick = { selectedMode = AndroidSyncMode.GOOGLE_DRIVE },
                                colors = RadioButtonDefaults.colors(selectedColor = Color(0xFF4CC2FF))
                            )
                            Spacer(modifier = Modifier.width(6.dp))
                            Text(
                                "Google Drive (File Picker)",
                                fontSize = 13.sp,
                                fontWeight = FontWeight.SemiBold,
                                color = Color.White
                            )
                        }

                        if (selectedMode == AndroidSyncMode.GOOGLE_DRIVE) {
                            Spacer(modifier = Modifier.height(6.dp))
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.SpaceBetween,
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                Text(
                                    if (isDriveLinked) "Status: Linked ✓" else "Status: Not Linked",
                                    fontSize = 12.sp,
                                    color = if (isDriveLinked) Color(0xFF107C41) else Color(0xFF888888)
                                )

                                Button(
                                    onClick = onSelectDriveFile,
                                    colors = ButtonDefaults.buttonColors(containerColor = Color(0xFF333333)),
                                    shape = RoundedCornerShape(6.dp)
                                ) {
                                    Text("Pick timelogs.json", fontSize = 11.sp, color = Color.White)
                                }
                            }
                        }
                    }
                }

                // Option 3: Local Only
                Card(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(vertical = 4.dp)
                        .clickable { selectedMode = AndroidSyncMode.LOCAL },
                    colors = CardDefaults.cardColors(
                        containerColor = if (selectedMode == AndroidSyncMode.LOCAL) Color(0xFF262E38) else Color(0xFF252525)
                    ),
                    shape = RoundedCornerShape(10.dp)
                ) {
                    Row(
                        modifier = Modifier.padding(10.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        RadioButton(
                            selected = selectedMode == AndroidSyncMode.LOCAL,
                            onClick = { selectedMode = AndroidSyncMode.LOCAL },
                            colors = RadioButtonDefaults.colors(selectedColor = Color(0xFF4CC2FF))
                        )
                        Spacer(modifier = Modifier.width(6.dp))
                        Text(
                            "Local Cache Only",
                            fontSize = 13.sp,
                            fontWeight = FontWeight.SemiBold,
                            color = Color.White
                        )
                    }
                }
            }
        },
        confirmButton = {
            Button(
                onClick = {
                    when (selectedMode) {
                        AndroidSyncMode.HOME_SERVER -> onSaveHomeServer(serverUrl, apiKey)
                        AndroidSyncMode.GOOGLE_DRIVE -> { /* Handled via file picker */ onDismiss() }
                        AndroidSyncMode.LOCAL -> onSaveLocalOnly()
                    }
                    onDismiss()
                },
                colors = ButtonDefaults.buttonColors(containerColor = Color(0xFF0078D4)),
                shape = RoundedCornerShape(8.dp)
            ) {
                Text("Save", fontWeight = FontWeight.Bold, color = Color.White)
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) {
                Text("Cancel", color = Color(0xFFAAAAAA))
            }
        }
    )
}
