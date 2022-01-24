using System;

namespace SpleeterToMultichannel {
    public class Recombiner : Renderer {
        public Recombiner(MainWindow window, TaskEngine engine) : base(window, engine) {
            RenderChannels = 2;
        }

        public override void Render(float[] source, float[] target, Instrument instrument, double progressGap) {
            double progressSrc = engine.Progress;
            long totalSamplesWritten = 0;
            for (long start = 0; start < source.LongLength; start += ioBlockSize) {
                long blockSize = Math.Min(source.LongLength, start + ioBlockSize);
                for (long sample = start; sample < blockSize; ++sample)
                    target[sample] += source[sample];
                RenderUpdateTask(engine, instrument.Name, totalSamplesWritten / (double)target.LongLength,
                        progressSrc, progressGap);
            }
            engine.UpdateProgressBar(progressSrc + progressGap);
            engine.UpdateStatus(string.Format("Mixing {0} (100%)...", instrument.Name));
        }
    }
}