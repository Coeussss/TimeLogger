package com.example.worktimetracker.ui.dialogs

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
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
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.worktimetracker.model.Categories

@OptIn(ExperimentalLayoutApi::class)
@Composable
fun PromptDialog(
    onDismiss: () -> Unit,
    onRecord: (category: String, description: String, minutes: Int) -> Unit,
    onSnooze: () -> Unit
) {
    var selectedCategory by remember { mutableStateOf("Coding & Dev") }
    var description by remember { mutableStateOf("") }
    var durationMinutes by remember { mutableStateOf(30) }

    AlertDialog(
        onDismissRequest = onDismiss,
        containerColor = Color(0xFF1E1E1E),
        titleContentColor = Color.White,
        textContentColor = Color(0xFFCCCCCC),
        shape = RoundedCornerShape(16.dp),
        title = {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(text = "⏱️ 30-Minute Check-in", fontSize = 18.sp, fontWeight = FontWeight.Bold, color = Color.White)
            }
        },
        text = {
            Column(modifier = Modifier.fillMaxWidth()) {
                Text(
                    text = "What were you working on during this block?",
                    fontSize = 13.sp,
                    color = Color(0xFFAAAAAA)
                )

                Spacer(modifier = Modifier.height(12.dp))

                Text(
                    text = "Select Category:",
                    fontSize = 12.sp,
                    fontWeight = FontWeight.SemiBold,
                    color = Color(0xFF888888)
                )

                Spacer(modifier = Modifier.height(6.dp))

                // Quick chips
                FlowRow(
                    horizontalArrangement = Arrangement.spacedBy(6.dp),
                    verticalArrangement = Arrangement.spacedBy(6.dp),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Categories.quickCategories.forEach { item ->
                        val isSelected = selectedCategory == item.name
                        Box(
                            modifier = Modifier
                                .background(
                                    if (isSelected) item.color.copy(alpha = 0.25f) else Color(0xFF282828),
                                    RoundedCornerShape(8.dp)
                                )
                                .border(
                                    width = if (isSelected) 1.5.dp else 1.dp,
                                    color = if (isSelected) item.color else Color(0xFF383838),
                                    shape = RoundedCornerShape(8.dp)
                                )
                                .clickable { selectedCategory = item.name }
                                .padding(horizontal = 8.dp, vertical = 6.dp)
                        ) {
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                Text(text = item.icon, fontSize = 12.sp)
                                Spacer(modifier = Modifier.width(4.dp))
                                Text(
                                    text = item.name,
                                    fontSize = 11.sp,
                                    fontWeight = if (isSelected) FontWeight.Bold else FontWeight.Normal,
                                    color = if (isSelected) item.color else Color(0xFFDDDDDD)
                                )
                            }
                        }
                    }
                }

                Spacer(modifier = Modifier.height(14.dp))

                // Description Input
                OutlinedTextField(
                    value = description,
                    onValueChange = { description = it },
                    label = { Text("Ticket Notes / Description", fontSize = 12.sp) },
                    placeholder = { Text("e.g. Investigating issue #412 in production", fontSize = 12.sp, color = Color(0xFF666666)) },
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(8.dp),
                    colors = OutlinedTextFieldDefaults.colors(
                        focusedBorderColor = Color(0xFF0078D4),
                        unfocusedBorderColor = Color(0xFF383838),
                        focusedTextColor = Color.White,
                        unfocusedTextColor = Color.White,
                        cursorColor = Color(0xFF4CC2FF)
                    ),
                    maxLines = 3
                )

                Spacer(modifier = Modifier.height(12.dp))

                // Duration Selector
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(6.dp)
                ) {
                    listOf(15, 30, 45, 60).forEach { mins ->
                        val isSel = durationMinutes == mins
                        OutlinedButton(
                            onClick = { durationMinutes = mins },
                            modifier = Modifier.weight(1f),
                            shape = RoundedCornerShape(6.dp),
                            colors = ButtonDefaults.outlinedButtonColors(
                                containerColor = if (isSel) Color(0xFF0078D4) else Color(0xFF242424),
                                contentColor = if (isSel) Color.White else Color(0xFFAAAAAA)
                            ),
                            border = ButtonDefaults.outlinedButtonBorder.copy(
                                brush = androidx.compose.ui.graphics.SolidColor(if (isSel) Color(0xFF0078D4) else Color(0xFF383838))
                            )
                        ) {
                            Text(text = "${mins}m", fontSize = 11.sp, fontWeight = if (isSel) FontWeight.Bold else FontWeight.Normal)
                        }
                    }
                }
            }
        },
        confirmButton = {
            Button(
                onClick = { onRecord(selectedCategory, description, durationMinutes) },
                colors = ButtonDefaults.buttonColors(containerColor = Color(0xFF0078D4), contentColor = Color.White),
                shape = RoundedCornerShape(8.dp)
            ) {
                Text("Record ${durationMinutes}m", fontWeight = FontWeight.Bold)
            }
        },
        dismissButton = {
            Row {
                TextButton(onClick = onSnooze) {
                    Text("Snooze 5m", color = Color(0xFFFFAA44))
                }
                Spacer(modifier = Modifier.width(4.dp))
                TextButton(onClick = onDismiss) {
                    Text("Skip", color = Color(0xFF888888))
                }
            }
        }
    )
}
