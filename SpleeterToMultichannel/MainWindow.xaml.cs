using System;
using System.IO;
using System.Windows;
using System.Windows.Forms;

using Cavern.Format;

using MessageBox = System.Windows.MessageBox;
using CheckBox = System.Windows.Controls.CheckBox;

namespace SpleeterToMultichannel {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        string path = null;
        readonly TaskEngine task;

        public MainWindow() {
            InitializeComponent();
            task = new TaskEngine();
            task.SetProgressReporting(progress, progressLabel);
        }

        void OpenSpleeterOutput(object sender, RoutedEventArgs e) {
            FolderBrowserDialog browser = new FolderBrowserDialog();
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                browser.SelectedPath = path;
            if (browser.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                path = browser.SelectedPath;
                folder.Content = Path.GetFileName(path);
            }
        }

        void ProcessError(string message) {
            task.UpdateProgressBar(1);
            task.UpdateStatus("Failed!");
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        float[] Read(RIFFWaveReader reader, string instrument, double progressGap) {
            const long sampleBlock = 8192;
            double progressSrc = task.Progress;
            float[] read = new float[reader.Length * reader.ChannelCount];
            for (long sample = 0; sample < read.LongLength;) {
                double progress = sample / (double)read.LongLength;
                reader.ReadBlock(read, sample, Math.Min(sample += sampleBlock, read.LongLength));
                task.UpdateProgressBar(progressSrc + progressGap * progress);
                task.UpdateStatusLazy(string.Format("Reading {0} ({1})...", instrument, progress.ToString("0.00%")));
            }
            task.UpdateProgressBar(progressSrc + progressGap);
            task.UpdateStatus(string.Format("Reading {0} (100%)...", instrument));
            reader.Dispose();
            return read;
        }

        OutputMatrix GetMatrix(UpmixComboBox instrument) {
            UpmixOption ret = UpmixOption.Full;
            instrument.Dispatcher.Invoke(() => ret = (UpmixOption)instrument.SelectedItem);
            return new OutputMatrix(ret);
        }

        bool GetLFE(CheckBox instrument) {
            bool check = false;
            instrument.Dispatcher.Invoke(() => check = instrument.IsChecked.Value);
            return check;
        }

        void Render(float[] source, float[] target, OutputMatrix matrix, bool LFE, string instrument, double progressGap) {
            const long sampleBlock = 32768;
            double progressSrc = task.Progress;
            for (long sample = 0; sample < source.LongLength; sample += 2) {
                long actualSample = sample >> 1;
                float left = source[sample], right = source[sample + 1];
                for (long targetPos = 0; targetPos < 8; ++targetPos) {
                    long actualPos = (actualSample << 3) + targetPos;
                    target[actualPos] += left * matrix.LeftInput[targetPos];
                    target[actualPos] += right * matrix.RightInput[targetPos];
                }
                if (LFE)
                    target[(actualSample << 3) + 3] += OutputMatrix.out2 * .31622776601f /* -10 dB */ * (left + right);
                if (sample % sampleBlock == 0) {
                    double progress = sample / (double)source.LongLength;
                    task.UpdateProgressBar(progressSrc + progressGap * progress);
                    task.UpdateStatusLazy(string.Format("Mixing {0} ({1})...", instrument, progress.ToString("0.00%")));
                }
            }
            task.UpdateProgressBar(progressSrc + progressGap);
            task.UpdateStatus(string.Format("Mixing {0} (100%)...", instrument));
        }

        void Process() {
            task.UpdateProgressBar(0);
            task.UpdateStatus("Reading headers...");
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) {
                ProcessError("No valid path was selected.");
                return;
            }
            string bassPath = Path.Combine(path, "bass.wav"), drumsPath = Path.Combine(path, "drums.wav"),
                    otherPath = Path.Combine(path, "other.wav"), pianoPath = Path.Combine(path, "piano.wav"),
                    vocalsPath = Path.Combine(path, "vocals.wav");
            if (!File.Exists(bassPath) || !File.Exists(drumsPath) || !File.Exists(otherPath) || !File.Exists(vocalsPath)) {
                ProcessError("Essence files not found. Please export with at least 4 parts.");
                return;
            }
            RIFFWaveReader bassReader = null, drumsReader = null, otherReader = null, pianoReader = null, vocalsReader = null;
            try {
                bassReader = new RIFFWaveReader(new BinaryReader(File.Open(bassPath, FileMode.Open)));
                bassReader.ReadHeader();
                drumsReader = new RIFFWaveReader(new BinaryReader(File.Open(drumsPath, FileMode.Open)));
                drumsReader.ReadHeader();
                otherReader = new RIFFWaveReader(new BinaryReader(File.Open(otherPath, FileMode.Open)));
                otherReader.ReadHeader();
                vocalsReader = new RIFFWaveReader(new BinaryReader(File.Open(vocalsPath, FileMode.Open)));
                vocalsReader.ReadHeader();
                if (File.Exists(pianoPath)) {
                    pianoReader = new RIFFWaveReader(new BinaryReader(File.Open(pianoPath, FileMode.Open)));
                    pianoReader.ReadHeader();
                }
            } catch (IOException ex) {
                if (bassReader != null)
                    bassReader.Dispose();
                if (drumsReader != null)
                    drumsReader.Dispose();
                if (pianoReader != null)
                    pianoReader.Dispose();
                if (otherReader != null)
                    otherReader.Dispose();
                if (vocalsReader != null)
                    vocalsReader.Dispose();
                ProcessError(ex.Message);
                return;
            }

            float[] finalMix = new float[bassReader.Length * 8];
            float[] source = Read(bassReader, "bass", pianoReader == null ? .1 : .08);
            Render(source, finalMix, GetMatrix(bass), GetLFE(bassLFE), "bass", pianoReader == null ? .1 : .08);
            source = Read(drumsReader, "drums", pianoReader == null ? .1 : .08);
            Render(source, finalMix, GetMatrix(drums), GetLFE(drumsLFE), "drums", pianoReader == null ? .1 : .08);
            if (pianoReader != null) {
                source = Read(pianoReader, "piano", .08);
                Render(source, finalMix, GetMatrix(piano), GetLFE(pianoLFE), "piano", .08);
            }
            source = Read(otherReader, "other", pianoReader == null ? .1 : .08);
            Render(source, finalMix, GetMatrix(other), GetLFE(otherLFE), "other", pianoReader == null ? .1 : .08);
            source = Read(vocalsReader, "vocals", pianoReader == null ? .1 : .08);
            Render(source, finalMix, GetMatrix(vocals), GetLFE(vocalsLFE), "vocals", pianoReader == null ? .1 : .08);

            task.UpdateProgressBar(.8);
            task.UpdateStatus("Checking peaks...");
            float peak = 1;
            for (long i = 0; i < finalMix.LongLength; ++i)
                if (peak < Math.Abs(finalMix[i]))
                    peak = Math.Abs(finalMix[i]);

            if (peak != 1) {
                task.UpdateProgressBar(.85);
                task.UpdateStatus("Normalizing...");
                float mul = 1 / peak;
                for (long i = 0; i < finalMix.LongLength; ++i)
                    finalMix[i] *= mul;
            }

            task.UpdateProgressBar(.9);
            task.UpdateStatus("Exporting to render.wav (0.00%)...");
            RIFFWaveWriter writer = new RIFFWaveWriter(new BinaryWriter(File.Open(Path.Combine(path, "render.wav"), FileMode.Create)),
                8, bassReader.Length, bassReader.SampleRate, BitDepth.Int16);
            writer.WriteHeader();
            const long blockSize = 8192;
            for (long sample = 0; sample < finalMix.LongLength;) {
                double progress = sample / (double)finalMix.LongLength;
                writer.WriteBlock(finalMix, sample, Math.Min(sample += blockSize, finalMix.LongLength));
                task.UpdateProgressBar(.9 + .1 * progress);
                task.UpdateStatusLazy(string.Format("Exporting to render.wav ({0})...", progress.ToString("0.00%")));
            }

            task.UpdateProgressBar(1);
            task.UpdateStatus("Finished!");
        }

        void Process(object sender, RoutedEventArgs e) => task.Run(Process);
    }
}