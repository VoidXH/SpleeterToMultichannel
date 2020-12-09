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
        readonly TaskEngine engine;

        public Renderer(MainWindow window, TaskEngine engine) {
            this.window = window;
            this.engine = engine;
        }

        internal void ProcessError(string message) {
            engine.UpdateProgressBar(1);
            engine.UpdateStatus("Failed!");
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        float[] Read(RIFFWaveReader reader, string instrument, double progressGap) {
            const long sampleBlock = 8192;
            double progressSrc = engine.Progress;
            float[] read = new float[reader.Length * reader.ChannelCount];
            for (long sample = 0; sample < read.LongLength;) {
                double progress = sample / (double)read.LongLength;
                reader.ReadBlock(read, sample, Math.Min(sample += sampleBlock, read.LongLength));
                engine.UpdateProgressBar(progressSrc + progressGap * progress);
                engine.UpdateStatusLazy(string.Format("Reading {0} ({1})...", instrument, progress.ToString("0.00%")));
            }
            engine.UpdateProgressBar(progressSrc + progressGap);
            engine.UpdateStatus(string.Format("Reading {0} (100%)...", instrument));
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
            double progressSrc = engine.Progress;
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
                                RenderUpdateTask(engine, instrument, totalSamplesWritten / (double)target.LongLength, progressSrc, progressGap);
                        }
                    } else if (matrix.RightInput[channel] == 0) {
                        for (long sample = 0; sample < source.LongLength; sample += 2) {
                            long actualSample = sample >> 1;
                            long actualPos = (actualSample << 3) + channel;
                            target[actualPos] += source[sample] * matrix.LeftInput[channel];
                            if (++totalSamplesWritten % sampleBlock == 0)
                                RenderUpdateTask(engine, instrument, totalSamplesWritten / (double)target.LongLength, progressSrc, progressGap);
                        }
                    } else {
                        for (long sample = 0; sample < source.LongLength; sample += 2) {
                            long actualSample = sample >> 1;
                            long actualPos = (actualSample << 3) + channel;
                            target[actualPos] += source[sample] * matrix.LeftInput[channel] + source[sample + 1] * matrix.RightInput[channel];
                            if (++totalSamplesWritten % sampleBlock == 0)
                                RenderUpdateTask(engine, instrument, totalSamplesWritten / (double)target.LongLength, progressSrc, progressGap);
                        }
                    }
                } else if (LFE) {
                    for (long sample = 0; sample < source.LongLength; ++sample) {
                        target[(sample >> 1 << 3) + 3] += OutputMatrix.out2 * .31622776601f /* -10 dB */ * source[sample];
                        if (++totalSamplesWritten % sampleBlock == 0)
                            RenderUpdateTask(engine, instrument, totalSamplesWritten / (double)target.LongLength, progressSrc, progressGap);
                    }
                }
            }
            engine.UpdateProgressBar(progressSrc + progressGap);
            engine.UpdateStatus(string.Format("Mixing {0} (100%)...", instrument));
        }

        public void Process(Spleet spleet, int file = 0, int of = 1) {
            double progressStart = file / (double)of;
            engine.UpdateProgressBar(progressStart);
            engine.UpdateStatus("Reading headers...");
            if (!spleet.IsValid()) // Deleted since the scheduler was started
                return;
            RIFFWaveReader bassReader = null, drumsReader = null, otherReader = null, pianoReader = null, vocalsReader = null;
            try {
                bassReader = new RIFFWaveReader(new BinaryReader(File.Open(spleet.BassPath, FileMode.Open)));
                bassReader.ReadHeader();
                drumsReader = new RIFFWaveReader(new BinaryReader(File.Open(spleet.DrumsPath, FileMode.Open)));
                drumsReader.ReadHeader();
                otherReader = new RIFFWaveReader(new BinaryReader(File.Open(spleet.OtherPath, FileMode.Open)));
                otherReader.ReadHeader();
                vocalsReader = new RIFFWaveReader(new BinaryReader(File.Open(spleet.VocalsPath, FileMode.Open)));
                vocalsReader.ReadHeader();
                if (File.Exists(spleet.PianoPath)) {
                    pianoReader = new RIFFWaveReader(new BinaryReader(File.Open(spleet.PianoPath, FileMode.Open)));
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

            double progressMul = 1.0 / of, progressStep = (pianoReader == null ? .1 : .08) * progressMul;
            float[] finalMix = new float[bassReader.Length * 8];
            float[] source = Read(bassReader, "bass", progressStep);
            Render(source, finalMix, GetMatrix(window.bass), GetLFE(window.bassLFE), "bass", progressStep);
            bassReader.Dispose();
            source = Read(drumsReader, "drums", progressStep);
            Render(source, finalMix, GetMatrix(window.drums), GetLFE(window.drumsLFE), "drums", progressStep);
            drumsReader.Dispose();
            if (pianoReader != null) {
                source = Read(pianoReader, "piano", progressStep);
                Render(source, finalMix, GetMatrix(window.piano), GetLFE(window.pianoLFE), "piano", progressStep);
                pianoReader.Dispose();
            }
            source = Read(otherReader, "other", progressStep);
            Render(source, finalMix, GetMatrix(window.other), GetLFE(window.otherLFE), "other", progressStep);
            otherReader.Dispose();
            source = Read(vocalsReader, "vocals", progressStep);
            Render(source, finalMix, GetMatrix(window.vocals), GetLFE(window.vocalsLFE), "vocals", progressStep);
            vocalsReader.Dispose();

            engine.UpdateProgressBar(.8 * progressMul + progressStart);
            engine.UpdateStatus("Checking peaks...");
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
                engine.UpdateProgressBar(.85 * progressMul + progressStart);
                engine.UpdateStatus("Normalizing...");
                float mul = 1 / peak;
                for (long i = 0; i < finalMix.LongLength; ++i)
                    finalMix[i] *= mul;
            }

            engine.UpdateProgressBar(.9 * progressMul + progressStart);
            engine.UpdateStatus("Exporting to render.wav (0.00%)...");
            using (RIFFWaveWriter writer = new RIFFWaveWriter(new BinaryWriter(File.Open(spleet.RenderPath, FileMode.Create)),
                8, bassReader.Length, bassReader.SampleRate, BitDepth.Int16)) {
                writer.WriteHeader();
                const long blockSize = 8192;
                for (long sample = 0; sample < finalMix.LongLength;) {
                    double progress = sample / (double)finalMix.LongLength;
                    writer.WriteBlock(finalMix, sample, Math.Min(sample += blockSize, finalMix.LongLength));
                    engine.UpdateProgressBar(.9 + .1 * progress);
                    engine.UpdateStatusLazy(string.Format("Exporting to render.wav ({0})...", progress.ToString("0.00%")));
                }
            }

            engine.UpdateProgressBar(progressMul + progressStart);
            engine.UpdateStatus("Finished!");
        }
    }
}