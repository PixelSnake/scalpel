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

        public static Plugins.PluginLoader PluginLoader;
        public static ScalpelPlugin.Plugins.Plugin Formatter;

        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                PrintUsage();
                return;
            }

            ProjectFolder = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), args[0]);
            Console.WriteLine($"Input Directory: { ProjectFolder }");

            for (var i = 1; i < args.Length; ++i)
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

                            if (plugin == null) return;
                            Formatter = plugin;
                        }
                        break;

                    case "-i":
                    case "--include":
                        {
                            var arg = args[++i];
                            Filetypes = arg.Split(',');
                        }
                        break;
                }
            }

            if (Filetypes == null)
            {
                PrintUsage();
                Console.WriteLine("\n\t-i is mandatory.");
                return;
            }
            if (Formatter == null)
            {
                PrintUsage();
                Console.WriteLine("\n\t-f is mandatory.");
                return;
            }

            var parser = new DocParser.DocumentationParser(ProjectFolder, Filetypes);
            var docs = parser.Parse();

            foreach (var f in docs.Files)
            {
                if (f.Classes.Length > 0) Console.WriteLine(f.Path);
                foreach (var c in f.Classes)
                {
                    Console.WriteLine($"{ c.AccessLevel } { c.Modifier } class { c.Name }");
                    Console.WriteLine($"Author: { c.Info.Author ?? "?" }");
                    Console.WriteLine($"Summary: { c.Info.Summary }");
                }
            }

            Formatter.Convert(docs, OutFolder);

            Console.WriteLine("");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("\tscalpel project-path [arguments]");
        }
    }
}
