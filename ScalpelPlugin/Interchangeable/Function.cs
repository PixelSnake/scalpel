using Scalpel.Interchangeable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scalpel.Interchangeable
{
    public class Function : ClassMember
    {
        public string ReturnTypeUnparsed;
        public Class ReturnType;
        public string[] TypeParams;

        public bool IsGeneric { get => TypeParams?.Length > 0; }
    }
}
