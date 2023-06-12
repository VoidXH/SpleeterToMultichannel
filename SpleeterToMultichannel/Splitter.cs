using Cavern.Format;
using Cavern.Utilities;
using SpleeterToMultichannel.Properties;
using System.Collections.Generic;
using System.IO;
using System;
using System.Windows.Forms;

using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace SpleeterToMultichannel {
    /// <summary>
    /// Handles chunking of content and recombining after the chunks were processed.
    /// </summary>
    public static class Splitter {
        public static void SplitSource(TaskEngine engine) {
            OpenFileDialog opener = new() {
                Filter = "Supported audio files|" + AudioReader.filter
            };
            if (opener.ShowDialog().Value) {
                engine.Run(() => SplitSourceProc(engine, opener.FileName));
            }
        }

        public static void CombineSplitResult(TaskEngine engine) {
            FolderBrowserDialog opener = new() {
                Description = "Select the first split's result folder (ending in .0) " +
                "that was created by Spleeter and was rendered with this application."
            };
            if (opener.ShowDialog() == DialogResult.OK) {
                int idx = opener.SelectedPath.LastIndexOf(".");
                if (idx == -1) {
                    // TODO: error message
                    return;
                }
                engine.Run(() => CombineSplitResultProc(engine, opener.SelectedPath[..(idx + 1)]));
            }
        }

        // TODO: progress bar + UpdateProgressBarLazy for both
        static void SplitSourceProc(TaskEngine engine, string sourceFile) {
            using AudioReader source = AudioReader.Open(sourceFile);
            source.ReadHeader();
            int idx = sourceFile.LastIndexOf('.');
            using SegmentedAudioWriter target = new(sourceFile[..idx] + ".{0}.wav",
                source.ChannelCount, source.Length, splitSeconds * source.SampleRate, source.SampleRate, source.Bits, source.SampleRate);
            target.WriteHeader();

            long remains = source.Length;
            float[] samples = new float[source.SampleRate * source.ChannelCount];
            while (remains != 0) {
                long nextSecond = Math.Min(source.SampleRate, remains);
                source.ReadBlock(samples, 0, nextSecond * source.ChannelCount);
                WaveformUtils.Gain(samples, .5f); // Less noticeable global volume oscillation as any split could get clipping prevention
                target.WriteBlock(samples, 0, nextSecond * source.ChannelCount);
                remains -= nextSecond;
            }
        }

        static void CombineSplitResultProc(TaskEngine engine, string sourceFolder) {
            List<AudioReader> splits = new();
            for (int i = 0; ; i++) {
                string currentSplit = $"{sourceFolder}{i}\\render.wav";
                if (File.Exists(currentSplit)) {
                    splits.Add(AudioReader.Open(currentSplit));
                } else {
                    break;
                }
            }

            long totalLength = 0;
            for (int i = 0, c = splits.Count; i < c;) {
                splits[i].ReadHeader();
                totalLength += splits[i].Length;
                if (++i != c) {
                    totalLength -= splits[i].SampleRate; // 1 second overlaps
                }
            }

            FileStream resultFile = new(Path.Combine(Path.GetDirectoryName(sourceFolder), "render.wav"), FileMode.Create);
            using AudioWriter result =
                new RIFFWaveWriter(resultFile, splits[0].ChannelCount, totalLength, splits[0].SampleRate, splits[0].Bits);
            result.WriteHeader();
            float[] mainSamples = new float[(splits[0].Length - splits[0].SampleRate) * splits[0].ChannelCount],
                fadeSamples = new float[splits[0].SampleRate * splits[0].ChannelCount];
            for (int i = 0, c = splits.Count; i < c; i++) {
                if (i + 1 != c) {
                    splits[i].ReadBlock(mainSamples, 0, mainSamples.Length);
                    if (i != 0) {
                        Fading.Linear(fadeSamples, mainSamples, splits[0].SampleRate, splits[0].ChannelCount);
                    }
                    result.WriteBlock(mainSamples, 0, mainSamples.Length);
                    splits[i].ReadBlock(fadeSamples, 0, fadeSamples.Length);
                } else {
                    totalLength = splits[i].Length * splits[i].ChannelCount;
                    splits[i].ReadBlock(mainSamples, 0, totalLength);
                    result.WriteBlock(mainSamples, 0, totalLength);
                }

                string path = splits[i].Path;
                splits[i].Dispose();
                if (Settings.Default.renderCleanup) {
                    File.Delete(path);
                }
            }
        }

        /// <summary>
        /// Each segment will be this many seconds when cut with the splitting feature. Each overlap is one second.
        /// </summary>
        const int splitSeconds = 60;
    }
}