using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityFramework.TemporalTables {
    internal enum StoreFunctionKind {
        StoredProcedure,
        TableValuedFunction,
        ScalarUserDefinedFunction
    }
}
