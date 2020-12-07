using System.IO;
using System.Windows;
using System.Windows.Forms;

namespace SpleeterToMultichannel {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        string path = null;
        readonly Renderer renderer;

        public MainWindow() {
            InitializeComponent();
            TaskEngine task = new TaskEngine();
            task.SetProgressReporting(progress, progressLabel);
            renderer = new Renderer(this, task);
        }

        void OpenSpleeterOutput(object sender, RoutedEventArgs e) {
            FolderBrowserDialog browser = new FolderBrowserDialog();
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                browser.SelectedPath = path;
            if (browser.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                path = browser.SelectedPath;
                folder.Content = Path.GetFileName(path);
            }
        }

        void Process(object sender, RoutedEventArgs e) => renderer.Render(path);
    }
}