using System.Collections.Generic;
using System.IO;

namespace SpleeterToMultichannel {
    public class Scheduler {
        List<Spleet> Sources { get; } = new List<Spleet>();

        readonly MainWindow window;
        readonly TaskEngine engine;
        TaskGroup group;
        Renderer renderer;

        void RunNext(TaskEngine engine, int task) => engine.Run(() => renderer.Process(Sources[task], task, Sources.Count), group);

        public Scheduler(MainWindow window, TaskEngine engine) {
            this.window = window;
            this.engine = engine;
        }

        public void Run(string path) {
            if (engine.IsOperationRunning) {
                renderer.ProcessError("Another process is already running.");
                return;
            }
            Spleet root = new Spleet(path);
            if (root.IsValid())
                Sources.Add(root);
            else {
                List<string> crawl = new List<string>(Directory.GetDirectories(path));
                for (int i = 0; i < crawl.Count; ++i) {
                    Spleet source = new Spleet(crawl[i]);
                    if (source.IsValid())
                        Sources.Add(source);
                    else
                        crawl.AddRange(Directory.GetDirectories(crawl[i]));
                }
            }
            renderer = new Renderer(window, engine);
            if (Sources.Count == 0) {
                renderer.ProcessError("No 4- or 5-stem Spleeter results were found in the selected folder or its subfolders.");
                return;
            }
            group = new TaskGroup(engine, RunNext, Sources.Count);
            group.Start();
        }
    }
}