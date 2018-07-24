using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scalpel.Interchangeable
{
    public class FunctionParameter
    {
        public DocumentationInfo Info { get; set; }

        public string Name, DatatypeUnparsed;
        public string[] Modifiers;
        public string DefaultValue;
    }
}
