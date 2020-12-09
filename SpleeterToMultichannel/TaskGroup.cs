namespace SpleeterToMultichannel {
    public class TaskGroup {
        public delegate void NextTask(TaskEngine engine, int task);
        readonly TaskEngine engine;
        readonly NextTask onNextTask;
        readonly int count;

        int position;

        public TaskGroup(TaskEngine engine, NextTask handler, int count) {
            this.engine = engine;
            this.count = count;
            onNextTask = handler;
        }

        public void Start() => onNextTask(engine, 0);

        public void Next() {
            if (++position == count)
                return;
            onNextTask(engine, position);
        }
    }
}