using SpleeterToMultichannel.Properties;
using System;
using System.IO;
using System.Windows;
using System.Windows.Forms;

using MessageBox = System.Windows.MessageBox;

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
                    if (string.IsNullOrEmpty(Settings.Default.Path))
                        break;
                } catch {
                    Settings.Default.Path = string.Empty;
                    break;
                }
            }
            browser.SelectedPath = Settings.Default.Path;
            if (Settings.Default.Vocals == -1)
                Reset(null, null);
            else {
                vocals.SelectedIndex = Settings.Default.Vocals;
                vocalsLFE.IsChecked = Settings.Default.VocalsLFE;
                bass.SelectedIndex = Settings.Default.Bass;
                bassLFE.IsChecked = Settings.Default.BassLFE;
                drums.SelectedIndex = Settings.Default.Drums;
                drumsLFE.IsChecked = Settings.Default.DrumsLFE;
                piano.SelectedIndex = Settings.Default.Piano;
                pianoLFE.IsChecked = Settings.Default.PianoLFE;
                other.SelectedIndex = Settings.Default.Other;
                otherLFE.IsChecked = Settings.Default.OtherLFE;
            }
        }

        protected override void OnClosed(EventArgs e) {
            Settings.Default.Path = browser.SelectedPath;
            Settings.Default.Vocals = vocals.SelectedIndex;
            Settings.Default.VocalsLFE = vocalsLFE.IsChecked.Value;
            Settings.Default.Bass = bass.SelectedIndex;
            Settings.Default.BassLFE = bassLFE.IsChecked.Value;
            Settings.Default.Drums = drums.SelectedIndex;
            Settings.Default.DrumsLFE = drumsLFE.IsChecked.Value;
            Settings.Default.Piano = piano.SelectedIndex;
            Settings.Default.PianoLFE = pianoLFE.IsChecked.Value;
            Settings.Default.Other = other.SelectedIndex;
            Settings.Default.OtherLFE = otherLFE.IsChecked.Value;
            Settings.Default.Save();
            base.OnClosed(e);
        }

        string PathDisplay(string from) {
            string dir = Path.GetFileName(from);
            if (!string.IsNullOrEmpty(dir))
                return dir;
            return from;
        }

        void OpenSpleeterOutput(object sender, RoutedEventArgs e) {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                browser.SelectedPath = path;
            if (browser.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                path = browser.SelectedPath;
                folder.Content = PathDisplay(path);
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

        void Process(object sender, RoutedEventArgs e) {
            if (path == null && Directory.Exists(browser.SelectedPath)) {
                if (MessageBox.Show($"You did not select a folder but last time you selected {PathDisplay(browser.SelectedPath)}. " +
                    "Do you want to process that folder?", "Folder selection", MessageBoxButton.YesNo, MessageBoxImage.Question) ==
                    MessageBoxResult.Yes) {
                    path = browser.SelectedPath;
                    folder.Content = PathDisplay(path);
                }
            }
            scheduler.Run(path);
        }
    }
}