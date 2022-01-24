using Cavern.Utilities;
using System;
using System.IO;
using System.Windows.Controls;

namespace SpleeterToMultichannel {
    public class Spleet : IDisposable {
        public Instrument Bass { get; private set; }
        public Instrument Drums { get; private set; }
        public Instrument Other { get; private set; }
        public Instrument Piano { get; private set; }
        public Instrument Vocals { get; private set; }

        public string RenderPath {
            get {
                if (renderPath == null)
                    renderPath = Path.Combine(path, "render.wav");
                return renderPath;
            }
        }
        string renderPath;

        public long Length => Vocals.Length;
        public int SampleRate => Vocals.SampleRate;

        readonly string path;
        readonly TaskEngine engine;

        static float GetGain(Slider source) => (float)Math.Pow(10, source.Value * .05);

        static OutputMatrix GetMatrix(UpmixComboBox instrument) {
            UpmixOption ret = (UpmixOption)instrument.SelectedItem;
            return new OutputMatrix(ret);
        }

        public Spleet(string path, MainWindow window, TaskEngine engine) {
            this.path = path;
            this.engine = engine;
            Bass = new Instrument("bass", path, window.bassLFE.IsChecked.Value,
                GetGain(window.bassGain), GetMatrix(window.bass));
            Drums = new Instrument("drums", path, window.drumsLFE.IsChecked.Value,
                GetGain(window.drumsGain), GetMatrix(window.drums));
            Other = new Instrument("other", path, window.otherLFE.IsChecked.Value,
                GetGain(window.otherGain), GetMatrix(window.other));
            Piano = new Instrument("piano", path, window.pianoLFE.IsChecked.Value,
                GetGain(window.pianoGain), GetMatrix(window.piano));
            Vocals = new Instrument("vocals", path, window.vocalsLFE.IsChecked.Value,
                GetGain(window.vocalsGain), GetMatrix(window.vocals));
        }

        public void RenderTrack(Instrument instrument, Renderer target, float[] finalMix, double readStep, double mixStep) {
            float[] source = instrument.Read(engine, readStep);
            target.Render(source, finalMix, instrument, mixStep);
            instrument.Dispose();
        }

        public bool IsValid() =>
            File.Exists(Bass.Path) && File.Exists(Drums.Path) && File.Exists(Other.Path) && File.Exists(Vocals.Path);

        public void Dispose() {
            Bass.Dispose();
            Drums.Dispose();
            Other.Dispose();
            Piano.Dispose();
            Vocals.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}