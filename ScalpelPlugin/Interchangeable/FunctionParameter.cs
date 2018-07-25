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

        public string Name, TypeUnparsed;
        public string[] Modifiers;
        public string DefaultValue;

        public override string ToString()
        {
            var modifiersString = String.Join(" ", Modifiers);
            modifiersString = modifiersString.Length > 0 ? modifiersString + " " : "";
            return $"{ modifiersString }{ TypeUnparsed } { Name }";
        }
    }
}
