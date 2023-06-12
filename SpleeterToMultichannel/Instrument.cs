using Cavern.Format;
using System;
using System.IO;

namespace SpleeterToMultichannel {
    public class Instrument : IDisposable {
        const string wavExtension = ".wav";

        public string Name { get; private set; }
        public string Path { get; private set; }
        public bool LFE { get; private set; }
        public float Gain { get; private set; }
        public OutputMatrix Matrix { get; private set; }
        public bool IsValid { get; private set; }

        public long Length => reader.Length;
        public int SampleRate => reader.SampleRate;

        RIFFWaveReader reader;

        public Instrument(string name, string path, bool LFE, float gain, OutputMatrix matrix) {
            Name = name;
            Path = System.IO.Path.Combine(path, name + wavExtension);
            this.LFE = LFE;
            Gain = gain;
            Matrix = matrix;
        }

        public void Open() {
            if (!File.Exists(Path)) {
                IsValid = false;
                return;
            }
            reader = new RIFFWaveReader(Path);
            reader.ReadHeader();
            IsValid = true;
        }

        public float[] Read(TaskEngine engine, double progressGap) {
            double progressSrc = engine.Progress;
            float[] read = new float[reader.Length * reader.ChannelCount];
            for (long sample = 0; sample < read.LongLength;) {
                double progress = sample / (double)read.LongLength;
                long lastSample = sample;
                reader.ReadBlock(read, sample, sample = Math.Min(sample + Renderer.ioBlockSize, read.LongLength));
                if (Gain != 1)
                    for (long gained = lastSample; gained < sample; ++gained)
                        read[gained] *= Gain;
                engine.UpdateProgressBar(progressSrc + progressGap * progress);
                engine.UpdateStatusLazy(string.Format("Reading {0} ({1})...", Name, progress.ToString("0.00%")));
            }
            engine.UpdateProgressBar(progressSrc + progressGap);
            engine.UpdateStatus(string.Format("Reading {0} (100%)...", Name));
            reader.Dispose();
            return read;
        }

        public void Dispose() {
            reader?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}