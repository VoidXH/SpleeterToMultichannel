using System.IO;
using System.Windows;
using System.Windows.Forms;

namespace SpleeterToMultichannel {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        string path = null;
        readonly Scheduler scheduler;

        public MainWindow() {
            InitializeComponent();
            TaskEngine task = new TaskEngine();
            task.SetProgressReporting(progress, progressLabel);
            scheduler = new Scheduler(this, task);
        }

        void Ad(object sender, RoutedEventArgs e) => System.Diagnostics.Process.Start("http://en.sbence.hu");

        void OpenSpleeterOutput(object sender, RoutedEventArgs e) {
            FolderBrowserDialog browser = new FolderBrowserDialog();
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                browser.SelectedPath = path;
            if (browser.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                path = browser.SelectedPath;
                folder.Content = Path.GetFileName(path);
            }
        }

        void Process(object sender, RoutedEventArgs e) {
            scheduler.Run(path);
        }
    }
}