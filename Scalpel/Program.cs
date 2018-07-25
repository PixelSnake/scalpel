using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scalpel
{
    public class Program
    {
        public static string OutFolder, ProjectFolder, Format;
        public static string[] Filetypes;
        public static string[] FormatParams = new string[] { };

        public static Plugins.PluginLoader PluginLoader;
        public static ScalpelPlugin.Plugins.Plugin Formatter;

        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                PrintUsage();
                return;
            }

            if (System.IO.Directory.Exists(args[0]))
            {
                ProjectFolder = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), args[0]);
                Console.WriteLine($"Input Directory: { ProjectFolder }");
            }

            ParseArguments(args, ProjectFolder == null ? 0 : 1);

            if (ProjectFolder != null && Filetypes == null)
            {
                PrintUsage();
                Console.WriteLine("\n\t-i is mandatory when project path is specified.");
                return;
            }
            if (ProjectFolder != null && Formatter == null)
            {
                PrintUsage();
                Console.WriteLine("\n\t-f is mandatory when project path is specified.");
                return;
            }

            // If no path is specified, we dont parse anything
            if (ProjectFolder == null) return;

            var parser = new DocParser.DocumentationParser(ProjectFolder, Filetypes);
            var docs = parser.Parse();

            foreach (var f in docs.Namespaces)
            {
                if (f.Datatypes.Length > 0) Console.WriteLine(f.Name);
                foreach (var c in f.Datatypes)
                {
                    if (c is Interchangeable.Class)
                    {
                        var _class = c as Interchangeable.Class;
                        Console.WriteLine($"\t{ _class.AccessLevel } { _class.Modifier } class { _class.Name }");
                        Console.WriteLine($"\t\tSummary: { _class.Info.Summary }");
                    }
                }
                Console.WriteLine();
            }

            Formatter.Convert(new ScalpelPlugin.Plugins.PluginParams()
            {
                Arguments = FormatParams,
                Documentation = docs,
                TargetDirectory = OutFolder
            });

            Console.ReadKey();
        }

        static void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("\tscalpel project-path [arguments]");
        }

        static void ParseArguments(string[] args, int start = 1)
        {
            for (var i = start; i < args.Length; ++i)
            {
                var p = args[i];

                switch (p)
                {
                    default:
                        Console.WriteLine($"Unrecongnized flag \"{ p }\"");
                        break;

                    case "-o":
                    case "--out":
                        {
                            var arg = args[++i];
                            OutFolder = arg;

                            Console.WriteLine($"Output Directory: { OutFolder }");
                        }
                        break;

                    case "-f":
                    case "--format":
                        {
                            var arg = args[++i];
                            Format = arg;

                            if (PluginLoader == null) PluginLoader = new Plugins.PluginLoader();
                            var plugin = PluginLoader.LoadPlugin(Format);

                            if (plugin == null)
                            {
                                Console.WriteLine($"FATAL: Plugin \"{ arg }\" cannot be found. Terminating.");
                                return;
                            }
                            Formatter = plugin;
                        }
                        break;

                    case "-fp":
                    case "--format-params":
                        {
                            var arg = args[++i];
                            FormatParams = arg.Split(',').Select(param => param.Trim()).ToArray();
                        }
                        break;

                    case "-i":
                    case "--include":
                        {
                            var arg = args[++i];
                            Filetypes = arg.Split(',');
                        }
                        break;

                    case "-pi":
                    case "--plugin-info":
                        {
                            var arg = args[++i];
                            if (PluginLoader == null) PluginLoader = new Plugins.PluginLoader();
                            var plugin = PluginLoader.LoadPlugin(arg);

                            if (plugin == null) return;
                            if (plugin.Info == null)
                            {
                                Console.WriteLine("This plugin does not provide any information about itself");
                                break;
                            }

                            Console.WriteLine(plugin.Info.Name + " Version " + plugin.Info.Version);
                            Console.WriteLine(plugin.Info.Description);
                            Console.WriteLine($"Author: { plugin.Info.Author } ({ plugin.Info.EMail })");
                            Console.WriteLine($"Web: { plugin.Info.Web }");
                            Console.WriteLine($"Repository: { plugin.Info.Repository }");
                            Console.WriteLine($"Issues: { plugin.Info.Issues }");
                        }
                        break;
                }
            }
        }
    }
}
