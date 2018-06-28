using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scalpel.Interchangeable
{
    public class Class : Datatype
    {
        public static Dictionary<string, Class> ByName = new Dictionary<string, Class>();

        public string AccessLevel, Modifier;
        public string[] BaseClasses, TypeParams;

        public Function[] Functions;

        public bool IsGeneric { get => TypeParams?.Length > 0; }

        public override string ToString()
        {
            return (Namespace.Name.Length > 0 ? Namespace.Name + "." : "") + Name;
        }
    }
}
