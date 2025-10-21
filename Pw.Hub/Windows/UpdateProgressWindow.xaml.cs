using System;
using System.Windows;

namespace Pw.Hub.Windows
{
    public partial class UpdateProgressWindow : Window
    {
        public UpdateProgressWindow()
        {
            InitializeComponent();
            SetIndeterminate(true);
            SetStatus("Подготовка...");
        }

        public void SetTitle(string title)
        {
            if (!CheckAccess()) { Dispatcher.Invoke(() => SetTitle(title)); return; }
            TitleText.Text = title;
        }

        public void SetStatus(string status)
        {
            if (!CheckAccess()) { Dispatcher.Invoke(() => SetStatus(status)); return; }
            StatusText.Text = status;
        }

        public void SetIndeterminate(bool indeterminate)
        {
            if (!CheckAccess()) { Dispatcher.Invoke(() => SetIndeterminate(indeterminate)); return; }
            Progress.IsIndeterminate = indeterminate;
            PercentText.Text = indeterminate ? string.Empty : "0%";
        }

        public void SetProgress(double percent)
        {
            if (!CheckAccess()) { Dispatcher.Invoke(() => SetProgress(percent)); return; }
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;
            Progress.IsIndeterminate = false;
            Progress.Value = percent;
            PercentText.Text = Math.Round(percent) + "%";
        }

        public void CloseSafe()
        {
            if (!CheckAccess()) { Dispatcher.Invoke(CloseSafe); return; }
            try { Close(); } catch { /* ignore */ }
        }
    }
}