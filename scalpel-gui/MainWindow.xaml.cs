using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace scalpel_gui
{
    public partial class MainWindow : Window
    {
        public static readonly DependencyProperty InputDirectoryProperty = DependencyProperty.Register("InputDirectory", typeof(string), typeof(MainWindow), new UIPropertyMetadata(string.Empty));
        public string InputDirectory
        {
            get { return (string)GetValue(InputDirectoryProperty); }
            set { SetValue(InputDirectoryProperty, value); }
        }

        public static readonly DependencyProperty OutputDirectoryProperty = DependencyProperty.Register("OutputDirectory", typeof(string), typeof(MainWindow), new UIPropertyMetadata(string.Empty));
        public string OutputDirectory
        {
            get { return (string)GetValue(OutputDirectoryProperty); }
            set { SetValue(OutputDirectoryProperty, value); }
        }

        public List<string> Plugins
        {
            get
            {
                return _plugins;
            }
            set
            {
                _plugins = value;
                foreach (var p in _plugins)
                {
                    ComboBoxPlugins.Items.Add(p);
                }
            }
        }
        private List<string> _plugins;


        public MainWindow()
        {
            InitializeComponent();
            LoadAndWatchPlugins();
        }

        private void LoadAndWatchPlugins()
        {
            var pluginPath = "../../../Scalpel/bin/Debug/plugins";

            var watcher = new FileSystemWatcher();
            watcher.Path = pluginPath;
            watcher.NotifyFilter = NotifyFilters.LastWrite;
            watcher.Filter = "*.dll";
            watcher.Changed += filesChanged;
            watcher.EnableRaisingEvents = true;

            filesChanged(null, null);

            void filesChanged(object sender, FileSystemEventArgs eventArgs)
            {
                var dlls = new List<string>();
                var dir = new DirectoryInfo(pluginPath);
                foreach (var f in dir.EnumerateFiles())
                {
                    if (f.Extension == ".dll") dlls.Add(f.Name.Substring(0, f.Name.Length - 4));
                }
                Plugins = dlls;
            }
        }

        private void InputBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            System.Windows.Forms.DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                InputDirectory = dialog.SelectedPath;
            }
        }

        private void OutputBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.SelectedPath = InputDirectory;
            System.Windows.Forms.DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                OutputDirectory = dialog.SelectedPath;
            }
        }

        private void ComboBoxPlugins_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var scalpelPath = "../../../Scalpel/bin/Debug/";
            var plugin = ComboBoxPlugins.SelectedItem.ToString();
            LabelPluginInformation.Content = "";

            var scalpel = new Process();
            var scalpelStartInfo = new ProcessStartInfo()
            {
                FileName = scalpelPath + "Scalpel.exe",
                WorkingDirectory = scalpelPath,
                Arguments = "-pi " + plugin,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            scalpel.OutputDataReceived += (s, _e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    LabelPluginInformation.Content += _e.Data + "\n";
                });
            };
            scalpel.StartInfo = scalpelStartInfo;
            scalpel.Start();
            scalpel.BeginOutputReadLine();
        }

        private void BuildDocs(object sender, RoutedEventArgs e)
        {
            var scalpelPath = "../../../Scalpel/bin/Debug/";
            var plugin = ComboBoxPlugins.SelectedItem.ToString();

            var scalpel = new Process();
            var scalpelStartInfo = new ProcessStartInfo()
            {
                FileName = scalpelPath + "Scalpel.exe",
                WorkingDirectory = scalpelPath,
                Arguments = $"{InputDirectory} -o {OutputDirectory} -i \"{TxtFileTypes.Text}\" -f {plugin} -fp \"-o\"",
                UseShellExecute = false
            };
            scalpel.StartInfo = scalpelStartInfo;
            scalpel.Start();
        }
    }
}
