using Scalpel.Interchangeable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scalpel.Interchangeable
{
    public class Function : IInterchangeable
    {
        public static int ClassId = 0;
        public static Dictionary<string, Function> ByName = new Dictionary<string, Function>();

        public int Id = ClassId++;
        public string AccessLevel, Modifier, ReturnTypeUnparsed, Name;
        public Class ReturnType;
        public string[] TypeParams;

        public bool IsGeneric { get => TypeParams?.Length > 0; }

        public DocumentationInfo Info { get; set; }
    }
}
