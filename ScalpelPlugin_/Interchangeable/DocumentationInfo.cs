using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scalpel.Interchangeable
{
    public class DocumentationInfo
    {
        public ScalpelPlugin.Syntax.FormattedText Summary;
        public string Author;
        public Dictionary<string, string> TypeParamDescription;
    }
}
