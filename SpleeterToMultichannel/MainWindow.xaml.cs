using SpleeterToMultichannel.Properties;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;

using MessageBox = System.Windows.MessageBox;

namespace SpleeterToMultichannel {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        string path = null;
        Scheduler scheduler;
        readonly FolderBrowserDialog browser = new();
        readonly TaskEngine engine;

        public MainWindow() {
            InitializeComponent();
            engine = new TaskEngine();
            engine.SetProgressReporting(progress, progressLabel);
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
                vocalsGain.Value = Settings.Default.VocalsGain;
                bass.SelectedIndex = Settings.Default.Bass;
                bassLFE.IsChecked = Settings.Default.BassLFE;
                bassGain.Value = Settings.Default.VocalsGain;
                drums.SelectedIndex = Settings.Default.Drums;
                drumsLFE.IsChecked = Settings.Default.DrumsLFE;
                drumsGain.Value = Settings.Default.DrumsGain;
                piano.SelectedIndex = Settings.Default.Piano;
                pianoLFE.IsChecked = Settings.Default.PianoLFE;
                pianoGain.Value = Settings.Default.PianoGain;
                other.SelectedIndex = Settings.Default.Other;
                otherLFE.IsChecked = Settings.Default.OtherLFE;
                otherGain.Value = Settings.Default.OtherGain;
                recombiner.IsChecked = Settings.Default.stereoRenderer;
                lfeLowpass.IsChecked = Settings.Default.lfeLowpass;
                lfeLowpassFreq.Value = Settings.Default.lfeLowpassFreq;
            }
            stemCleanup.IsChecked = Settings.Default.stemCleanup;
            renderCleanup.IsChecked = Settings.Default.renderCleanup;
        }

        protected override void OnClosed(EventArgs e) {
            Settings.Default.Path = browser.SelectedPath;
            Settings.Default.Vocals = vocals.SelectedIndex;
            Settings.Default.VocalsLFE = vocalsLFE.IsChecked.Value;
            Settings.Default.VocalsGain = (short)vocalsGain.Value;
            Settings.Default.Bass = bass.SelectedIndex;
            Settings.Default.BassLFE = bassLFE.IsChecked.Value;
            Settings.Default.BassGain = (short)bassGain.Value;
            Settings.Default.Drums = drums.SelectedIndex;
            Settings.Default.DrumsLFE = drumsLFE.IsChecked.Value;
            Settings.Default.DrumsGain = (short)drumsGain.Value;
            Settings.Default.Piano = piano.SelectedIndex;
            Settings.Default.PianoLFE = pianoLFE.IsChecked.Value;
            Settings.Default.PianoGain = (short)pianoGain.Value;
            Settings.Default.Other = other.SelectedIndex;
            Settings.Default.OtherLFE = otherLFE.IsChecked.Value;
            Settings.Default.OtherGain = (short)otherGain.Value;
            Settings.Default.stereoRenderer = recombiner.IsChecked.Value;
            Settings.Default.lfeLowpass = lfeLowpass.IsChecked.Value;
            Settings.Default.lfeLowpassFreq = (byte)lfeLowpassFreq.Value;
            Settings.Default.Save();
            base.OnClosed(e);
        }

        static string PathDisplay(string from) {
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
                folder.Text = PathDisplay(path);
            }
        }

        void VocalsGainChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
            vocalsGainDisplay.Text = ((Slider)sender).Value.ToString("0 dB");

        void BassGainChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
            bassGainDisplay.Text = ((Slider)sender).Value.ToString("0 dB");

        void DrumsGainChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
            drumsGainDisplay.Text = ((Slider)sender).Value.ToString("0 dB");

        void PianoGainChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
            pianoGainDisplay.Text = ((Slider)sender).Value.ToString("0 dB");

        void OtherGainChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
            otherGainDisplay.Text = ((Slider)sender).Value.ToString("0 dB");

        void Reset(object _, RoutedEventArgs e) {
            vocals.SelectedIndex = (int)UpmixOption.MidSideScreen;
            vocalsLFE.IsChecked = false;
            vocalsGain.Value = 0;
            bass.SelectedIndex = (int)UpmixOption.QuadroRear;
            bassLFE.IsChecked = true;
            bassGain.Value = 0;
            drums.SelectedIndex = (int)UpmixOption.QuadroSide;
            drumsLFE.IsChecked = true;
            drumsGain.Value = 0;
            piano.SelectedIndex = (int)UpmixOption.Screen;
            pianoLFE.IsChecked = false;
            pianoGain.Value = 0;
            other.SelectedIndex = (int)UpmixOption.Full;
            otherLFE.IsChecked = false;
            otherGain.Value = 0;
            multichannel.IsChecked = true;
            lfeLowpass.IsChecked = true;
            lfeLowpassFreq.Value = 80;
        }

        void SplitSource(object _, RoutedEventArgs e) => Splitter.SplitSource(engine);

        void CombineSplitResult(object _, RoutedEventArgs e) => Splitter.CombineSplitResult(engine);

        void StemCleanupChanged(object sender, RoutedEventArgs e) => Settings.Default.stemCleanup = stemCleanup.IsChecked.Value;

        void RenderCleanupChanged(object sender, RoutedEventArgs e) => Settings.Default.renderCleanup = renderCleanup.IsChecked.Value;

        void Ad(object sender, RoutedEventArgs e) => System.Diagnostics.Process.Start("http://en.sbence.hu");

        void Process(object sender, RoutedEventArgs e) {
            if (path == null && Directory.Exists(browser.SelectedPath)) {
                if (MessageBox.Show($"You did not select a folder but last time you selected " +
                    $"{PathDisplay(browser.SelectedPath)}. Do you want to process that folder?",
                    "Folder selection", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) {
                    path = browser.SelectedPath;
                    folder.Text = PathDisplay(path);
                }
            }
            scheduler = new Scheduler(this, engine);
            scheduler.Run(path);
        }
    }
}