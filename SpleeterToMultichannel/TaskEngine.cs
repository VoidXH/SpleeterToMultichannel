using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SpleeterToMultichannel {
    /// <summary>
    /// Progress reporter and job handler.
    /// </summary>
    public class TaskEngine {
        readonly static TimeSpan lazyStatusDelta = new TimeSpan(0, 0, 1);

        Task operation;
        ProgressBar progressBar;
        Label progressLabel;
        DateTime lastLazyStatus = DateTime.MinValue;

        /// <summary>
        /// A task is running and is not completed or failed.
        /// </summary>
        public bool IsOperationRunning => operation != null && operation.Status == TaskStatus.Running;

        /// <summary>
        /// Current value of the progress bar.
        /// </summary>
        public double Progress {
            get {
                double val = -1;
                if (progressBar != null)
                    progressBar.Dispatcher.Invoke(() => val = progressBar.Value);
                return val;
            }
        }

        /// <summary>
        /// Set the progress bar and status label to enable progress reporting on the UI.
        /// </summary>
        public void SetProgressReporting(ProgressBar progressBar, Label progressLabel) {
            this.progressBar = progressBar;
            this.progressLabel = progressLabel;
        }

        /// <summary>
        /// Set the progress on the progress bar if it's set.
        /// </summary>
        public void UpdateProgressBar(double progress) {
            if (progressBar != null)
                progressBar.Dispatcher.Invoke(() => progressBar.Value = progress);
        }

        /// <summary>
        /// Set the status text label, if it's given.
        /// </summary>
        public void UpdateStatus(string text) {
            if (progressLabel != null) {
                progressLabel.Dispatcher.Invoke(() => progressLabel.Content = text);
                lastLazyStatus = DateTime.Now;
            }
        }

        /// <summary>
        /// Set the status text label, if it's given. The label is only updated if the last update was <see cref="lazyStatusDelta"/> ago.
        /// </summary>
        public void UpdateStatusLazy(string text) {
            DateTime now = DateTime.Now;
            if (now - lastLazyStatus > lazyStatusDelta && progressLabel != null) {
                progressLabel.Dispatcher.Invoke(() => progressLabel.Content = text);
                lastLazyStatus = now;
            }
        }

        /// <summary>
        /// Run a new task if no task is running.
        /// </summary>
        public bool Run(Action task) {
            if (IsOperationRunning) {
                MessageBox.Show("Another operation is already running.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            operation = new Task(task);
            operation.Start();
            return true;
        }
    }
}