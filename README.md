# Dev & Support Time Logger (WPF + WPF-UI)

A modern Windows desktop application built with **WPF (.NET 8)** and **WPF-UI** (Fluent Design). Tailored specifically for software developers who also provide production support, this tool helps you account for every 30-minute block of work, track progress towards your daily 7.5-hour target, and export weekly summaries to Excel.

---

## ✨ Features

- **⏱️ 30-Minute Interval Tracker**:
  - Precision countdown timer with visual progress bar.
  - Controls: Start, Pause, Reset, and Manual Check-in.
  - Configurable interval: 30 minutes (default), 15m, 5m, or 1m test mode.

- **🔔 Automatic 30-Minute Check-in Popup**:
  - Topmost window that chimes and surfaces every 30 minutes.
  - Tailored categories for developers & production support:
    - 💻 *Feature Development & Coding*
    - 🚨 *Production Incident & Triage*
    - 🎫 *Prod Support & Monitoring*
    - 🐛 *Bug Fixing & Investigation*
    - 🔍 *Code Review & Pull Requests*
    - 🚀 *Deployments & Releases*
    - 👥 *Standups, Meetings & Syncs*
    - 🏗️ *Architecture & Tech Design*
    - 📞 *User Inquiries & On-Call Admin*
    - 📚 *Documentation & Knowledge Base*
    - ☕ *Break / Other*
  - Quick category chip buttons for one-click selection.
  - Notes field for ticket IDs (e.g. `PROD-1234`).
  - Actions: *Save (30 mins)*, *Snooze (5 mins)*, *Skip*.

- **📊 Weekly Dashboard**:
  - Work week totals (Monday – Sunday).
  - Summary metrics: Total Hours, Dominant Category, Prod Support vs Dev ratio.
  - Category breakdown with colored percentage bars.
  - Chronological activity log with edit and delete capabilities.

- **📅 Calendar View & 7.5-Hour Target Tick Marks**:
  - Month calendar with days of the week (Monday to Sunday).
  - **7.5-Hour Goal**: Days with $\ge 7.5$ hours logged show a prominent green checkmark badge (`✓ 7h 30m` / `✓ Target Met`).
  - Days with partial time show current hours and remaining time needed (`4.5h / 7.5h`).

- **📝 Daily Log Editor**:
  - Click any day in the calendar to view its time blocks.
  - `+ Add 30m Block`: Backfill or add missing entries with custom timestamps and durations.
  - Inline Edit (`✏️`) and Delete (`🗑️`) on existing entries.

- **📊 Export Week to Excel (.xlsx)**:
  - Generates styled Microsoft Excel `.xlsx` spreadsheets using `ClosedXML`.
  - **Sheet 1 ("Weekly Summary")**: Title, date range, KPIs, Category Breakdown Table with Excel formulas (`=SUM(...)`), and Daily Hours Table with 7.5h target status.
  - **Sheet 2 ("Activity Log")**: Complete itemized logs with timestamps, categories, durations, and notes.

---

## 🛠️ Tech Stack

- **Platform**: Windows Desktop (x64 / x86 / ARM64)
- **Framework**: .NET 8.0 Windows (`net8.0-windows`)
- **UI Library**: [WPF-UI (lepoco)](https://github.com/lepoco/wpfui) (v4.3.0) — Modern Fluent Design / Mica backdrop
- **Excel Generation**: [ClosedXML](https://github.com/ClosedXML/ClosedXML) (v0.105.1)
- **MVVM**: CommunityToolkit.Mvvm (v8.4.2)
- **Persistence**: JSON storage in `%LOCALAPPDATA%\WorkTimeTracker\time_logs.json`

---

## 🚀 Getting Started

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- Windows 10/11

### Build and Run
Clone the repository and run:

```powershell
git clone https://github.com/Coeussss/TimeLogger.git
cd TimeLogger
dotnet run
```
