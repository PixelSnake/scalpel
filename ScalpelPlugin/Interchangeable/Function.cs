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
        public Class Type;
        public string[] TypeParams;
        public FunctionParameter[] Params;

        public bool IsGeneric { get => TypeParams?.Length > 0; }

        public override string ToString()
        {
            return $"{ AccessLevel } { String.Join(" ", Modifiers) } { TypeUnparsed } { Name }({ String.Join(", ", Params.Select(p => p.ToString())) })";
        }
    }
}
