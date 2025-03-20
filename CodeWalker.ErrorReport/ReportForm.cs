using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CodeWalker.ErrorReport
{
    public partial class ReportForm : Form
    {
        public ReportForm()
        {
            InitializeComponent();
        }

        private void ReportForm_Load(object sender, EventArgs e)
        {
            using (EventLog eventLog = new EventLog("Application"))
            {
                if (eventLog.Entries.Count == 0)
                {
                    ErrorTextBox.Text = "No event log entries found.";
                    return;
                }

                bool errorFound = false;
                string[] targetProcesses =
                {
                    "CodeWalker.exe",
                    "CodeWalker RPF Explorer.exe",
                    "CodeWalker Ped Viewer.exe",
                    "CodeWalker Vehicle Viewer.exe"
                };

                for (int i = eventLog.Entries.Count - 1; i >= 0; i--)
                {
                    EventLogEntry entry = eventLog.Entries[i];

                    if (entry.EntryType == EventLogEntryType.Error && entry.Source == ".NET Runtime")
                    {
                        string[] messageLines = entry.Message.Split('\n');

                        if (messageLines.Length > 0 && targetProcesses.Any(p => messageLines[0].Contains(p)))
                        {
                            ErrorTextBox.Clear();
                            ErrorTextBox.Font = new Font("Consolas", 10);

                            AppendText("************** ERROR LOG **************\n", Color.DarkGray, true);
                            AppendText($"🕒 Timestamp: {entry.TimeGenerated}\n", Color.Blue);
                            AppendText($"🔍 Source: {entry.Source}\n", Color.Green);
                            AppendText($"🚀 Process: {messageLines[0].Trim()}\n", Color.Green);
                            AppendText("========================================\n", Color.DarkGray, true);

                            bool callStackStarted = false;
                            foreach (var line in messageLines.Skip(1)) // Skip the first line (process name)
                            {
                                string trimmedLine = line.Trim();

                                if (string.IsNullOrEmpty(trimmedLine))
                                    continue;

                                // Detect start of call stack (usually starts after an "Exception" message)
                                if (!callStackStarted && (trimmedLine.Contains("Exception") || trimmedLine.Contains("Error") || trimmedLine.Contains("failed")))
                                {
                                    AppendText(trimmedLine + "\n", Color.Red); // Error messages in Red
                                    callStackStarted = true;
                                }
                                else if (callStackStarted)
                                {
                                    if (trimmedLine.StartsWith("at ")) // Stack trace line
                                    {
                                        AppendText(trimmedLine + "\n\n", Color.Black); // extra line break
                                    }
                                    else
                                    {
                                        AppendText(trimmedLine + "\n", Color.Black); // Other details in default color
                                    }
                                }
                                else
                                {
                                    AppendText(trimmedLine + "\n", Color.Black);
                                }
                            }

                            AppendText("========================================\n", Color.DarkGray, true);

                            errorFound = true;
                            break;
                        }
                    }
                }

                if (!errorFound)
                {
                    AppendText("Event Log entry not found!\n", Color.Red);
                    MessageBox.Show("Unable to find the last CodeWalker.exe error in the Event Log.");
                }
            }
        }

        // Helper function to apply syntax highlighting
        private void AppendText(string text, Color color, bool bold = false)
        {
            ErrorTextBox.SelectionStart = ErrorTextBox.TextLength;
            ErrorTextBox.SelectionLength = 0;

            ErrorTextBox.SelectionColor = color;
            ErrorTextBox.SelectionFont = bold ? new Font("Consolas", 10, FontStyle.Bold) : new Font("Consolas", 10, FontStyle.Regular);

            ErrorTextBox.AppendText(text);
            ErrorTextBox.SelectionColor = ErrorTextBox.ForeColor; // Reset color
        }
    }
}
