using System;
using System.Collections.Generic;
using System.Text;

namespace ScalpelPlugin.Plugins
{
    public interface Plugin
    {
        void Convert(Scalpel.Interchangeable.Documentation documentation, string outPath);
    }
}
