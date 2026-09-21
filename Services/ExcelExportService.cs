using System;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using WorkTimeTracker.Models;

namespace WorkTimeTracker.Services
{
    public class ExcelExportService
    {
        public static void ExportWeekToExcel(
            TimeLogService logService,
            DateTime weekStart,
            string targetFilePath)
        {
            var (start, end) = logService.GetWeekRangeForDate(weekStart);
            var weekLogs = logService.GetLogsForWeek(start).OrderBy(l => l.Timestamp).ToList();
            var categorySummaries = logService.GetCategorySummariesForWeek(start);
            var totalDuration = logService.GetTotalDurationForWeek(start);

            using var workbook = new XLWorkbook();

            // -------------------------------------------------------------
            // SHEET 1: WEEKLY SUMMARY
            // -------------------------------------------------------------
            var wsSummary = workbook.Worksheets.Add("Weekly Summary");
            wsSummary.ShowGridLines = true;

            // Title block
            wsSummary.Cell("A1").Value = "WORK TIME TRACKER — WEEKLY REPORT";
            wsSummary.Cell("A1").Style.Font.Bold = true;
            wsSummary.Cell("A1").Style.Font.FontSize = 16;
            wsSummary.Cell("A1").Style.Font.FontColor = XLColor.FromHtml("#1F4E79");

            wsSummary.Cell("A2").Value = $"Work Week: {start:ddd, MMM d, yyyy} – {end:ddd, MMM d, yyyy}";
            wsSummary.Cell("A2").Style.Font.Italic = true;
            wsSummary.Cell("A2").Style.Font.FontSize = 11;
            wsSummary.Cell("A2").Style.Font.FontColor = XLColor.FromHtml("#555555");

            wsSummary.Cell("A3").Value = $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}";
            wsSummary.Cell("A3").Style.Font.FontSize = 9;
            wsSummary.Cell("A3").Style.Font.FontColor = XLColor.FromHtml("#888888");

            // KPI Overview
            int kpiRow = 5;
            wsSummary.Cell(kpiRow, 1).Value = "Total Tracked Hours:";
            wsSummary.Cell(kpiRow, 1).Style.Font.Bold = true;
            wsSummary.Cell(kpiRow, 2).Value = Math.Round(totalDuration.TotalHours, 2);
            wsSummary.Cell(kpiRow, 2).Style.NumberFormat.Format = "0.0 \"hrs\"";
            wsSummary.Cell(kpiRow, 2).Style.Font.Bold = true;

            wsSummary.Cell(kpiRow + 1, 1).Value = "Total 30m Blocks:";
            wsSummary.Cell(kpiRow + 1, 1).Style.Font.Bold = true;
            wsSummary.Cell(kpiRow + 1, 2).Value = weekLogs.Count;

            // Section 1: Categories Breakdown Table
            int catStartRow = 8;
            wsSummary.Cell(catStartRow, 1).Value = "CATEGORY BREAKDOWN";
            wsSummary.Cell(catStartRow, 1).Style.Font.Bold = true;
            wsSummary.Cell(catStartRow, 1).Style.Font.FontSize = 12;
            wsSummary.Cell(catStartRow, 1).Style.Font.FontColor = XLColor.FromHtml("#1F4E79");

            int catHeaderRow = catStartRow + 1;
            string[] catHeaders = { "Category", "Hours Spent", "30-Min Blocks", "% of Total Week" };
            for (int col = 0; col < catHeaders.Length; col++)
            {
                var cell = wsSummary.Cell(catHeaderRow, col + 1);
                cell.Value = catHeaders[col];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E79");
                cell.Style.Alignment.Horizontal = col == 0 ? XLAlignmentHorizontalValues.Left : XLAlignmentHorizontalValues.Right;
            }

            int currRow = catHeaderRow + 1;
            double totalWeekHours = totalDuration.TotalHours;

            foreach (var cat in categorySummaries)
            {
                wsSummary.Cell(currRow, 1).Value = cat.Category;
                
                var hoursCell = wsSummary.Cell(currRow, 2);
                hoursCell.Value = Math.Round(cat.TotalDuration.TotalHours, 2);
                hoursCell.Style.NumberFormat.Format = "0.00";
                hoursCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                var countCell = wsSummary.Cell(currRow, 3);
                countCell.Value = cat.Count;
                countCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                var pctCell = wsSummary.Cell(currRow, 4);
                pctCell.Value = totalWeekHours > 0 ? (cat.TotalDuration.TotalHours / totalWeekHours) : 0.0;
                pctCell.Style.NumberFormat.Format = "0.0%";
                pctCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                // Subtle zebra background
                if ((currRow - catHeaderRow) % 2 == 0)
                {
                    wsSummary.Range(currRow, 1, currRow, 4).Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F5F8");
                }

                currRow++;
            }

            // Category Totals Row
            if (categorySummaries.Count > 0)
            {
                wsSummary.Cell(currRow, 1).Value = "Grand Total";
                wsSummary.Cell(currRow, 1).Style.Font.Bold = true;

                var totalHoursCell = wsSummary.Cell(currRow, 2);
                totalHoursCell.FormulaA1 = $"=SUM(B{catHeaderRow + 1}:B{currRow - 1})";
                totalHoursCell.Style.Font.Bold = true;
                totalHoursCell.Style.NumberFormat.Format = "0.00";

                var totalCountCell = wsSummary.Cell(currRow, 3);
                totalCountCell.FormulaA1 = $"=SUM(C{catHeaderRow + 1}:C{currRow - 1})";
                totalCountCell.Style.Font.Bold = true;

                var totalPctCell = wsSummary.Cell(currRow, 4);
                totalPctCell.Value = 1.0;
                totalPctCell.Style.Font.Bold = true;
                totalPctCell.Style.NumberFormat.Format = "0.0%";

                wsSummary.Range(currRow, 1, currRow, 4).Style.Border.TopBorder = XLBorderStyleValues.Thin;
                wsSummary.Range(currRow, 1, currRow, 4).Style.Border.BottomBorder = XLBorderStyleValues.Double;
            }

            // Section 2: Daily Hours & 7.5h Target Table
            int dayStartRow = currRow + 3;
            wsSummary.Cell(dayStartRow, 1).Value = "DAILY WORK LOG & 7.5-HOUR TARGET";
            wsSummary.Cell(dayStartRow, 1).Style.Font.Bold = true;
            wsSummary.Cell(dayStartRow, 1).Style.Font.FontSize = 12;
            wsSummary.Cell(dayStartRow, 1).Style.Font.FontColor = XLColor.FromHtml("#1F4E79");

            int dayHeaderRow = dayStartRow + 1;
            string[] dayHeaders = { "Date", "Day", "Hours Logged", "Target (hrs)", "Target Met (>= 7.5h)" };
            for (int col = 0; col < dayHeaders.Length; col++)
            {
                var cell = wsSummary.Cell(dayHeaderRow, col + 1);
                cell.Value = dayHeaders[col];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2F5597");
                cell.Style.Alignment.Horizontal = col < 2 ? XLAlignmentHorizontalValues.Left : XLAlignmentHorizontalValues.Right;
            }

            int dayCurrRow = dayHeaderRow + 1;
            int targetMetCount = 0;
            for (int d = 0; d < 7; d++)
            {
                var currentDay = start.AddDays(d);
                var dayDuration = logService.GetTotalDurationForDate(currentDay);
                double dayHours = dayDuration.TotalHours;
                bool isTargetMet = dayHours >= DaySummary.TargetHours;
                if (isTargetMet) targetMetCount++;

                wsSummary.Cell(dayCurrRow, 1).Value = currentDay.ToString("yyyy-MM-dd");
                wsSummary.Cell(dayCurrRow, 2).Value = currentDay.ToString("dddd");

                var dayHoursCell = wsSummary.Cell(dayCurrRow, 3);
                dayHoursCell.Value = Math.Round(dayHours, 2);
                dayHoursCell.Style.NumberFormat.Format = "0.00";
                dayHoursCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                var targetCell = wsSummary.Cell(dayCurrRow, 4);
                targetCell.Value = 7.50;
                targetCell.Style.NumberFormat.Format = "0.00";
                targetCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                var statusCell = wsSummary.Cell(dayCurrRow, 5);
                if (isTargetMet)
                {
                    statusCell.Value = "✓ Target Met";
                    statusCell.Style.Font.FontColor = XLColor.FromHtml("#107C41"); // Green
                    statusCell.Style.Font.Bold = true;
                }
                else
                {
                    statusCell.Value = dayHours > 0 ? $"{dayHours:0.0}h / 7.5h" : "No hours";
                    statusCell.Style.Font.FontColor = XLColor.FromHtml("#888888");
                }
                statusCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                if (d % 2 == 1)
                {
                    wsSummary.Range(dayCurrRow, 1, dayCurrRow, 5).Style.Fill.BackgroundColor = XLColor.FromHtml("#F9FAFB");
                }

                dayCurrRow++;
            }

            // Summary of 7.5h target
            wsSummary.Cell(kpiRow + 2, 1).Value = "7.5h Target Days Met:";
            wsSummary.Cell(kpiRow + 2, 1).Style.Font.Bold = true;
            wsSummary.Cell(kpiRow + 2, 2).Value = $"{targetMetCount} of 7 days";
            wsSummary.Cell(kpiRow + 2, 2).Style.Font.Bold = true;
            wsSummary.Cell(kpiRow + 2, 2).Style.Font.FontColor = targetMetCount >= 5 ? XLColor.FromHtml("#107C41") : XLColor.FromHtml("#D83B01");

            wsSummary.Columns().AdjustToContents();

            // -------------------------------------------------------------
            // SHEET 2: DETAILED ACTIVITY LOG
            // -------------------------------------------------------------
            var wsDetails = workbook.Worksheets.Add("Activity Log");
            wsDetails.ShowGridLines = true;

            string[] detailHeaders = { "Date", "Day", "Time", "Category", "Duration (Hours)", "Duration (Mins)", "Notes / Ticket Description" };
            for (int col = 0; col < detailHeaders.Length; col++)
            {
                var cell = wsDetails.Cell(1, col + 1);
                cell.Value = detailHeaders[col];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E79");
            }

            int logRow = 2;
            foreach (var log in weekLogs)
            {
                wsDetails.Cell(logRow, 1).Value = log.Timestamp.ToString("yyyy-MM-dd");
                wsDetails.Cell(logRow, 2).Value = log.Timestamp.ToString("ddd");
                wsDetails.Cell(logRow, 3).Value = log.Timestamp.ToString("h:mm tt");
                wsDetails.Cell(logRow, 4).Value = log.Category;

                var hrsCell = wsDetails.Cell(logRow, 5);
                hrsCell.Value = Math.Round(log.Duration.TotalHours, 2);
                hrsCell.Style.NumberFormat.Format = "0.00";

                var minCell = wsDetails.Cell(logRow, 6);
                minCell.Value = (int)log.Duration.TotalMinutes;

                wsDetails.Cell(logRow, 7).Value = log.Description;

                if (logRow % 2 == 1)
                {
                    wsDetails.Range(logRow, 1, logRow, 7).Style.Fill.BackgroundColor = XLColor.FromHtml("#F7F9FA");
                }

                logRow++;
            }

            if (weekLogs.Count == 0)
            {
                wsDetails.Cell(2, 1).Value = "No log entries recorded for this work week.";
                wsDetails.Range(2, 1, 2, 7).Merge().Style.Font.Italic = true;
            }

            wsDetails.Columns().AdjustToContents();

            // Save the workbook
            workbook.SaveAs(targetFilePath);
        }
    }
}
