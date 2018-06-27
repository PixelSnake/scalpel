using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScalpelPlugin.Syntax.Elements
{
    public class Text : InlineElement
    {
        public Text(string s)
        {
            Value = s;
        }

        public override string ToString()
        {
            return Value;
        }
    }
}
