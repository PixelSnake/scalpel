using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scalpel.Interchangeable
{
    public class ClassMember : IInterchangeable
    {
        public static Dictionary<string, ClassMember> ByName = new Dictionary<string, ClassMember>();

        public string AccessLevel, Name, TypeUnparsed;
        public string[] Modifiers;

        public DocumentationInfo Info { get; set; }
        public Namespace Namespace { get; internal set; }
    }
}
