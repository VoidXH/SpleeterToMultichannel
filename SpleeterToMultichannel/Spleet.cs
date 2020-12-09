using System.IO;

namespace SpleeterToMultichannel {
    public class Spleet {
        public string BassPath { get; private set; }

        public string DrumsPath { get; private set; }

        public string OtherPath { get; private set; }

        public string PianoPath {
            get {
                if (pianoPath == null)
                    pianoPath = Path.Combine(path, "piano.wav");
                return pianoPath;
            }
        }
        string pianoPath;

        public string VocalsPath { get; private set; }

        public string RenderPath {
            get {
                if (renderPath == null)
                    renderPath = Path.Combine(path, "render.wav");
                return renderPath;
            }
        }
        string renderPath;

        string path;

        public Spleet(string path) {
            this.path = path;
            BassPath = Path.Combine(path, "bass.wav");
            DrumsPath = Path.Combine(path, "drums.wav");
            OtherPath = Path.Combine(path, "other.wav");
            VocalsPath = Path.Combine(path, "vocals.wav");
        }

        public bool IsValid() => File.Exists(BassPath) && File.Exists(DrumsPath) && File.Exists(OtherPath) && File.Exists(VocalsPath);
    }
}