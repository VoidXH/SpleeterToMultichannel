using SpleeterToMultichannel.Properties;
using System;
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
        readonly FolderBrowserDialog browser = new FolderBrowserDialog();

        public MainWindow() {
            InitializeComponent();
            TaskEngine task = new TaskEngine();
            task.SetProgressReporting(progress, progressLabel);
            scheduler = new Scheduler(this, task);
            while (!Directory.Exists(Settings.Default.Path)) {
                try {
                    Settings.Default.Path = Path.GetDirectoryName(Settings.Default.Path);
                } catch {
                    Settings.Default.Path = string.Empty;
                    break;
                }
            }
            browser.SelectedPath = Settings.Default.Path;
        }

        protected override void OnClosed(EventArgs e) {
            Settings.Default.Path = browser.SelectedPath;
            Settings.Default.Save();
            base.OnClosed(e);
        }

        void OpenSpleeterOutput(object sender, RoutedEventArgs e) {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                browser.SelectedPath = path;
            if (browser.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                path = browser.SelectedPath;
                folder.Content = Path.GetFileName(path);
            }
        }

        void Reset(object sender, RoutedEventArgs e) {
            vocals.SelectedIndex = (int)UpmixOption.MidSideScreen;
            vocalsLFE.IsChecked = false;
            bass.SelectedIndex = (int)UpmixOption.QuadroRear;
            bassLFE.IsChecked = true;
            drums.SelectedIndex = (int)UpmixOption.QuadroSide;
            drumsLFE.IsChecked = true;
            piano.SelectedIndex = (int)UpmixOption.Screen;
            pianoLFE.IsChecked = false;
            other.SelectedIndex = (int)UpmixOption.Full;
            otherLFE.IsChecked = false;
        }

        void Ad(object sender, RoutedEventArgs e) => System.Diagnostics.Process.Start("http://en.sbence.hu");

        void Process(object sender, RoutedEventArgs e) => scheduler.Run(path);
    }
}