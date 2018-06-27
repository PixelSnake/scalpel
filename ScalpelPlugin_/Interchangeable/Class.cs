using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scalpel.Interchangeable
{
    public class Class : IInterchangeable
    {
        public static int ClassId = 0;
        public static Dictionary<string, Class> ByName = new Dictionary<string, Class>();

        public int Id = ClassId++;
        public string AccessLevel, Modifier, Name;
        public string[] BaseClasses, TypeParams;

        public bool IsGeneric { get => TypeParams?.Length > 0; }

        public DocumentationInfo Info { get; set; }
    }
}
