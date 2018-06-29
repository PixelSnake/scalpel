using System;
using System.Collections.Generic;
using System.Text;

namespace ScalpelPlugin.Plugins
{
    public interface Plugin
    {
        PluginInfo Info { get; }
        void Convert(PluginParams p);
    }
}
