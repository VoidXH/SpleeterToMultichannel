using Cavern.Filters;
using Cavern.Format;
using Cavern.QuickEQ;
using Cavern.Utilities;
using System;
using System.IO;
using System.Windows;

using MessageBox = System.Windows.MessageBox;
using Window = Cavern.QuickEQ.Window;

namespace SpleeterToMultichannel {
    public class Renderer {
        internal const long ioBlockSize = 1 << 18;

        protected readonly TaskEngine engine;
        protected readonly byte? lowpass;

        public int RenderChannels { get; protected set; } = 8;

        public Renderer(TaskEngine engine, byte? lowpass) {
            this.engine = engine;
            this.lowpass = lowpass;
        }

        internal void ProcessError(string message) {
            engine.UpdateProgressBar(1);
            engine.UpdateStatus("Failed!");
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        protected static void RenderUpdateTask(TaskEngine task, string instrument, double progress,
            double progressSrc, double progressGap) {
            task.UpdateProgressBar(progressSrc + progressGap * progress);
            task.UpdateStatusLazy(string.Format("Mixing {0} ({1})...", instrument, progress.ToString("0.00%")));
        }

        public virtual void Render(float[] source, float[] target, Instrument instrument, double progressGap) {
            double progressSrc = engine.Progress;
            long totalSamplesWritten = 0;
            OutputMatrix matrix = instrument.Matrix;
            for (long channel = 0; channel < RenderChannels; ++channel) {
                if (channel != 3) {
                    if (matrix.LeftInput[channel] == 0 && matrix.RightInput[channel] == 0) {
                        totalSamplesWritten += target.Length >> 3;
                        continue;
                    }
                    if (matrix.LeftInput[channel] == 0) {
                        for (long sample = 1; sample < source.LongLength; sample += 2) {
                            target[(sample >> 1 << 3) + channel] += source[sample] * matrix.RightInput[channel];
                            if (++totalSamplesWritten % ioBlockSize == 0)
                                RenderUpdateTask(engine, instrument.Name, totalSamplesWritten / (double)target.LongLength,
                                    progressSrc, progressGap);
                        }
                    } else if (matrix.RightInput[channel] == 0) {
                        for (long sample = 0; sample < source.LongLength; sample += 2) {
                            target[(sample >> 1 << 3) + channel] += source[sample] * matrix.LeftInput[channel];
                            if (++totalSamplesWritten % ioBlockSize == 0)
                                RenderUpdateTask(engine, instrument.Name, totalSamplesWritten / (double)target.LongLength,
                                    progressSrc, progressGap);
                        }
                    } else {
                        for (long sample = 0; sample < source.LongLength; sample += 2) {
                            target[(sample >> 1 << 3) + channel] +=
                                source[sample] * matrix.LeftInput[channel] + source[sample + 1] * matrix.RightInput[channel];
                            if (++totalSamplesWritten % ioBlockSize == 0)
                                RenderUpdateTask(engine, instrument.Name, totalSamplesWritten / (double)target.LongLength,
                                    progressSrc, progressGap);
                        }
                    }
                } else if (instrument.LFE) {
                    for (long sample = 0; sample < source.LongLength; ++sample) {
                        target[(sample >> 1 << 3) + 3] += OutputMatrix.out2 * .31622776601f /* -10 dB */ * source[sample];
                        if (++totalSamplesWritten % ioBlockSize == 0)
                            RenderUpdateTask(engine, instrument.Name, totalSamplesWritten / (double)target.LongLength,
                                progressSrc, progressGap);
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
            try {
                spleet.Bass.Open();
                spleet.Drums.Open();
                spleet.Other.Open();
                spleet.Piano.Open();
                spleet.Vocals.Open();
            } catch (IOException ex) {
                spleet.Dispose();
                ProcessError(ex.Message);
                return;
            }

            double progressMul = 1.0 / of, progressStep = (spleet.Piano.IsValid ? .08 : .1) * progressMul;
            double readStep = progressStep * .1, mixStep = progressStep * 1.9;
            float[] finalMix = new float[spleet.Length * RenderChannels];
            spleet.RenderTrack(spleet.Bass, this, finalMix, readStep, mixStep);
            spleet.RenderTrack(spleet.Drums, this, finalMix, readStep, mixStep);
            if (spleet.Piano.IsValid)
                spleet.RenderTrack(spleet.Piano, this, finalMix, readStep, mixStep);
            spleet.RenderTrack(spleet.Other, this, finalMix, readStep, mixStep);
            spleet.RenderTrack(spleet.Vocals, this, finalMix, readStep, mixStep);

            engine.UpdateProgressBar(.8 * progressMul + progressStart);
            engine.UpdateStatus("Checking peaks...");
            int windowSize = 25 * RenderChannels;
            for (int i = 0; i < windowSize; ++i)
                finalMix[i] = finalMix[finalMix.Length - 1 - i] = 0;
            Windowing.ApplyWindow(finalMix, RenderChannels, Window.Sine, Window.Disabled, windowSize, 2 * windowSize, 0);
            Windowing.ApplyWindow(finalMix, RenderChannels, Window.Disabled, Window.Sine, 0,
                finalMix.Length - 2 * windowSize, finalMix.Length - windowSize);
            float peak = WaveformUtils.GetPeak(finalMix);

            if (peak != 1) {
                engine.UpdateProgressBar(.83 * progressMul + progressStart);
                engine.UpdateStatus("Normalizing...");
                WaveformUtils.Gain(finalMix, 1 / peak);
            }

            if (RenderChannels > 4 && lowpass.HasValue) {
                engine.UpdateProgressBar(.86 * progressMul + progressStart);
                engine.UpdateStatus("Applying LFE lowpass...");
                Lowpass filter = new(spleet.SampleRate, lowpass.Value);
                filter.Process(finalMix, 3, RenderChannels);
            }

            engine.UpdateProgressBar(.9 * progressMul + progressStart);
            engine.UpdateStatus("Exporting to render.wav (0.00%)...");
            using (RIFFWaveWriter writer = new(spleet.RenderPath, RenderChannels, spleet.Length, spleet.SampleRate,
                BitDepth.Int16)) {
                writer.WriteHeader();
                for (long sample = 0; sample < finalMix.LongLength;) {
                    double progress = sample / (double)finalMix.LongLength;
                    writer.WriteBlock(finalMix, sample, Math.Min(sample += ioBlockSize, finalMix.LongLength));
                    engine.UpdateProgressBar((.9 + .1 * progress) * progressMul + progressStart);
                    engine.UpdateStatusLazy(string.Format("Exporting to render.wav ({0})...", progress.ToString("0.00%")));
                }
            }

#pragma warning disable IDE0059 // Unnecessary assignment of a value
            finalMix = null;
#pragma warning restore IDE0059 // Unnecessary assignment of a value
            GC.Collect();

            engine.UpdateProgressBar(progressMul + progressStart);
            engine.UpdateStatus("Finished!");
        }
    }
}