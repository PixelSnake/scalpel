﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scalpel.Interchangeable
{
    public interface IInterchangeable
    {
        Namespace Namespace { get; }
        DocumentationInfo Info { get; }
    }
}
