using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Scalpel.Plugins
{
    public class PluginLoader
    {
        public ScalpelPlugin.Plugins.Plugin LoadPlugin(string name)
        {
            try
            {
                var asm = Assembly.LoadFrom($"plugins/{ name }.dll");
                var plugin = asm.GetType("Plugin");
                var instance = Activator.CreateInstance(plugin) as ScalpelPlugin.Plugins.Plugin;

                return instance;
            }
            catch (Exception e)
            {
                Console.WriteLine($"FATAL: Could not load plugin \"{ name }\": { e.Message }");
                return null;
            }
        }
    }
}
