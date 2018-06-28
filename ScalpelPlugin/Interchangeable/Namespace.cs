using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scalpel.Interchangeable
{
    public class Namespace
    {
        public string Name;
        public Namespace[] Children;
        public Datatype[] Datatypes;

        public override string ToString()
        {
            return $"{ Name } [{ Datatypes.Length }]";
        }
    }
}
