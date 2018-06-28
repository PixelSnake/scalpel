using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scalpel.Interchangeable
{
    public abstract class Datatype : IInterchangeable
    {
        public DocumentationInfo Info { get; set; }
        public Namespace Namespace { get; set; }
    }
}
