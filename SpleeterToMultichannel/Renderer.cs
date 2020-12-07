using System;
using System.IO;
using System.Windows;

using Cavern.Format;
using Cavern.QuickEQ;

using CheckBox = System.Windows.Controls.CheckBox;
using MessageBox = System.Windows.MessageBox;
using Window = Cavern.QuickEQ.Window;

namespace SpleeterToMultichannel {
    public class Renderer {
        readonly MainWindow window;
        readonly TaskEngine task;

        public Renderer(MainWindow window, TaskEngine task) {
            this.window = window;
            this.task = task;
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

        void RenderUpdateTask(TaskEngine task, string instrument, double progress, double progressSrc, double progressGap) {
            task.UpdateProgressBar(progressSrc + progressGap * progress);
            task.UpdateStatusLazy(string.Format("Mixing {0} ({1})...", instrument, progress.ToString("0.00%")));
        }

        void Render(float[] source, float[] target, OutputMatrix matrix, bool LFE, string instrument, double progressGap) {
            const long sampleBlock = 131072;
            double progressSrc = task.Progress;
            long totalSamplesWritten = 0;
            for (long channel = 0; channel < 8; ++channel) {
                if (channel != 3) {
                    if (matrix.LeftInput[channel] == 0 && matrix.RightInput[channel] == 0) {
                        totalSamplesWritten += target.Length >> 3;
                        continue;
                    }
                    if (matrix.LeftInput[channel] == 0) {
                        for (long sample = 1; sample < source.LongLength; sample += 2) {
                            long actualSample = sample >> 1;
                            long actualPos = (actualSample << 3) + channel;
                            target[actualPos] += source[sample] * matrix.RightInput[channel];
                            if (++totalSamplesWritten % sampleBlock == 0)
                                RenderUpdateTask(task, instrument, totalSamplesWritten / (double)target.LongLength, progressSrc, progressGap);
                        }
                    } else if (matrix.RightInput[channel] == 0) {
                        for (long sample = 0; sample < source.LongLength; sample += 2) {
                            long actualSample = sample >> 1;
                            long actualPos = (actualSample << 3) + channel;
                            target[actualPos] += source[sample] * matrix.LeftInput[channel];
                            if (++totalSamplesWritten % sampleBlock == 0)
                                RenderUpdateTask(task, instrument, totalSamplesWritten / (double)target.LongLength, progressSrc, progressGap);
                        }
                    } else {
                        for (long sample = 0; sample < source.LongLength; sample += 2) {
                            long actualSample = sample >> 1;
                            long actualPos = (actualSample << 3) + channel;
                            target[actualPos] += source[sample] * matrix.LeftInput[channel] + source[sample + 1] * matrix.RightInput[channel];
                            if (++totalSamplesWritten % sampleBlock == 0)
                                RenderUpdateTask(task, instrument, totalSamplesWritten / (double)target.LongLength, progressSrc, progressGap);
                        }
                    }
                } else if (LFE) {
                    for (long sample = 0; sample < source.LongLength; ++sample) {
                        long actualSample = sample >> 1;
                        target[(actualSample << 3) + 3] += OutputMatrix.out2 * .31622776601f /* -10 dB */ * source[sample];
                        if (++totalSamplesWritten % sampleBlock == 0)
                            RenderUpdateTask(task, instrument, totalSamplesWritten / (double)target.LongLength, progressSrc, progressGap);
                    }
                }
            }
            task.UpdateProgressBar(progressSrc + progressGap);
            task.UpdateStatus(string.Format("Mixing {0} (100%)...", instrument));
        }

        void Process(string path) {
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
            Render(source, finalMix, GetMatrix(window.bass), GetLFE(window.bassLFE), "bass", pianoReader == null ? .1 : .08);
            bassReader.Dispose();
            source = Read(drumsReader, "drums", pianoReader == null ? .1 : .08);
            Render(source, finalMix, GetMatrix(window.drums), GetLFE(window.drumsLFE), "drums", pianoReader == null ? .1 : .08);
            drumsReader.Dispose();
            if (pianoReader != null) {
                source = Read(pianoReader, "piano", .08);
                Render(source, finalMix, GetMatrix(window.piano), GetLFE(window.pianoLFE), "piano", .08);
                pianoReader.Dispose();
            }
            source = Read(otherReader, "other", pianoReader == null ? .1 : .08);
            Render(source, finalMix, GetMatrix(window.other), GetLFE(window.otherLFE), "other", pianoReader == null ? .1 : .08);
            otherReader.Dispose();
            source = Read(vocalsReader, "vocals", pianoReader == null ? .1 : .08);
            Render(source, finalMix, GetMatrix(window.vocals), GetLFE(window.vocalsLFE), "vocals", pianoReader == null ? .1 : .08);
            vocalsReader.Dispose();

            task.UpdateProgressBar(.8);
            task.UpdateStatus("Checking peaks...");
            const int windowSize = 25 * 8;
            for (int i = 0; i < windowSize; ++i)
                finalMix[i] = finalMix[finalMix.Length - 1 - i] = 0;
            Windowing.ApplyWindow(finalMix, 8, Window.Sine, Window.Disabled, windowSize, 2 * windowSize, 0);
            Windowing.ApplyWindow(finalMix, 8, Window.Disabled, Window.Sine, 0, finalMix.Length - 2 * windowSize, finalMix.Length - windowSize);
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
            using (RIFFWaveWriter writer = new RIFFWaveWriter(new BinaryWriter(File.Open(Path.Combine(path, "render.wav"), FileMode.Create)),
                8, bassReader.Length, bassReader.SampleRate, BitDepth.Int16)) {
                writer.WriteHeader();
                const long blockSize = 8192;
                for (long sample = 0; sample < finalMix.LongLength;) {
                    double progress = sample / (double)finalMix.LongLength;
                    writer.WriteBlock(finalMix, sample, Math.Min(sample += blockSize, finalMix.LongLength));
                    task.UpdateProgressBar(.9 + .1 * progress);
                    task.UpdateStatusLazy(string.Format("Exporting to render.wav ({0})...", progress.ToString("0.00%")));
                }
            }

            task.UpdateProgressBar(1);
            task.UpdateStatus("Finished!");
        }

        public void Render(string path) => task.Run(() => Process(path));
    }
}