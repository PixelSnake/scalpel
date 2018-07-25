using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Xml;

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

            Directory.CreateDirectory(pluginPath);

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

                if (File.Exists(InputDirectory + "/scalpel.xml")) LoadConfig(InputDirectory);
            }
        }

        private void LoadConfig(string folder)
        {
            var config = folder + "/scalpel.xml";
            var doc = new XmlDocument();
            doc.Load(config);

            var project = doc["project"];

            InputDirectory = project.GetAttribute("input");
            OutputDirectory = project.GetAttribute("output");
            var plugin = project["format"].GetAttribute("value");
            ComboBoxPlugins.Text = plugin; // will trigger the plugin load via the combobox's change event

            TxtFormatOptions.Text = project["format"].GetAttribute("options");

            var endings = new List<string>();
            foreach (XmlElement ft in project.GetElementsByTagName("type"))
            {
                endings.Add(ft.GetAttribute("ending"));
            }
            TxtFileTypes.Text = string.Join(",", endings);
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
            LoadPlugin(ComboBoxPlugins.SelectedItem.ToString());
        }

        private void LoadPlugin(string plugin)
        {
            var scalpelPath = "../../../Scalpel/bin/Debug/";
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
            if (ComboBoxPlugins.SelectedItem == null)
            {
                System.Windows.Forms.MessageBox.Show("No output format selected!", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return;
            }
            if (InputDirectory == "")
            {
                System.Windows.Forms.MessageBox.Show("No input directory selected!", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return;
            }
            if (OutputDirectory == "")
            {
                System.Windows.Forms.MessageBox.Show("No output directory selected!", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return;
            }

            var scalpelPath = "../../../Scalpel/bin/Debug/";
            var plugin = ComboBoxPlugins.SelectedItem.ToString();

            var scalpel = new Process();
            var scalpelStartInfo = new ProcessStartInfo()
            {
                FileName = scalpelPath + "Scalpel.exe",
                WorkingDirectory = scalpelPath,
                Arguments = $"{InputDirectory} -o {OutputDirectory} -i \"{TxtFileTypes.Text}\" -f {plugin} -fp \"{ TxtFormatOptions.Text }\"",
                UseShellExecute = false
            };
            scalpel.StartInfo = scalpelStartInfo;
            scalpel.Start();
        }

        private void SaveConfig(object sender, RoutedEventArgs e)
        {
            if (InputDirectory == "")
            {
                System.Windows.Forms.MessageBox.Show("Please select an input directory", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return;
            }

            var doc = new XmlDocument();
            var decl = doc.CreateXmlDeclaration("1.0", "UTF-8", "");
            doc.AppendChild(decl);

            var project = doc.CreateElement("project");
            project.SetAttribute("input", InputDirectory);
            project.SetAttribute("output", OutputDirectory);

            foreach (var ft in TxtFileTypes.Text.Split(','))
            {
                var ftTag = doc.CreateElement("type");
                ftTag.SetAttribute("ending", ft);
                project.AppendChild(ftTag);
            }

            var formatTag = doc.CreateElement("format");
            formatTag.SetAttribute("value", ComboBoxPlugins.SelectedItem.ToString());
            formatTag.SetAttribute("options", TxtFormatOptions.Text);
            project.AppendChild(formatTag);

            doc.AppendChild(project);
            doc.Save(InputDirectory + "/scalpel.xml");

            System.Windows.Forms.MessageBox.Show("Config saved to \"" + InputDirectory + "\"", "Config saved", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var doc = new XmlDocument();
            var decl = doc.CreateXmlDeclaration("1.0", "UTF-8", "");
            doc.AppendChild(decl);

            var config = doc.CreateElement("config");

            var file = doc.CreateElement("folder");
            file.SetAttribute("path", InputDirectory);
            config.AppendChild(file);

            doc.AppendChild(config);

            doc.Save(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/config.xml");
        }

        private void Window_Activated(object sender, System.EventArgs e)
        {
            var config = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/config.xml";
            if (!File.Exists(config)) return;

            var doc = new XmlDocument();
            doc.Load(config);

            var folders = doc["config"].GetElementsByTagName("folder");
            var folder = (folders[0] as XmlElement).GetAttribute("path");
            if (File.Exists(folder + "/scalpel.xml")) LoadConfig(folder);
        }
    }
}
