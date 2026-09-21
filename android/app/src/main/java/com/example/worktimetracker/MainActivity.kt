package com.example.worktimetracker

import android.content.Intent
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.viewModels
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.Surface
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import com.example.worktimetracker.service.IntervalNotificationHelper
import com.example.worktimetracker.theme.WorkTimeTrackerTheme
import com.example.worktimetracker.ui.TimeTrackerViewModel
import com.example.worktimetracker.ui.screens.HomeScreen

class MainActivity : ComponentActivity() {

    private val viewModel: TimeTrackerViewModel by viewModels()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        handleIntent(intent)
        enableEdgeToEdge()

        setContent {
            WorkTimeTrackerTheme(darkTheme = true) {
                Surface(
                    modifier = Modifier.fillMaxSize(),
                    color = Color(0xFF121212)
                ) {
                    HomeScreen(viewModel = viewModel)
                }
            }
        }
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        handleIntent(intent)
    }

    override fun onResume() {
        super.onResume()
        viewModel.refreshLogs()
    }

    private fun handleIntent(intent: Intent?) {
        if (intent?.action == IntervalNotificationHelper.ACTION_SHOW_CHECKIN) {
            viewModel.openPromptDialog()
        }
    }
}
