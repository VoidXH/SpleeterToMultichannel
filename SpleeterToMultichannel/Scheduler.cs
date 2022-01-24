using System.Collections.Generic;
using System.IO;

namespace SpleeterToMultichannel {
    public class Scheduler {
        List<Spleet> Sources { get; } = new List<Spleet>();

        readonly Renderer renderer;
        readonly MainWindow window;
        readonly TaskEngine engine;
        TaskGroup group;

        void RunNext(TaskEngine engine, int task) => engine.Run(() => renderer.Process(Sources[task], task, Sources.Count), group);

        public Scheduler(MainWindow window, TaskEngine engine) {
            this.window = window;
            this.engine = engine;
            if (window.multichannel.IsChecked.Value) // TODO: save decision
                renderer = new Renderer(window, engine);
            else
                renderer = new Recombiner(window, engine);
        }

        public void Run(string path) {
            if (path == null) {
                renderer.ProcessError("Please select a folder that contains a 4- or 5-stem Spleeter output.");
                return;
            }
            if (engine.IsOperationRunning) {
                renderer.ProcessError("Another process is already running.");
                return;
            }
            Spleet root = new(path, window, engine);
            Sources.Clear();
            if (root.IsValid())
                Sources.Add(root);
            else {
                List<string> crawl = new(Directory.GetDirectories(path));
                for (int i = 0; i < crawl.Count; ++i) {
                    Spleet source = new(crawl[i], window, engine);
                    if (source.IsValid())
                        Sources.Add(source);
                    else
                        crawl.AddRange(Directory.GetDirectories(crawl[i]));
                }
            }
            if (Sources.Count == 0) {
                renderer.ProcessError("No 4- or 5-stem Spleeter results were found in the selected folder or its subfolders.");
                return;
            }
            group = new TaskGroup(engine, RunNext, Sources.Count);
            group.Start();
        }
    }
}