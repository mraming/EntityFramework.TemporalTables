using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityFramework.TemporalTables {

    /// <summary>
    /// Base class for entities that need to be stored in System Versions temporal tables, providing a default implementation
    /// of the <see cref="ITemporalTable"/> interface
    /// </summary>
    /// <remarks>
    /// The 'marker' for entities that must be stored in system versioned temporal tables is the <see cref="ITemporalTable"/>
    /// inerface, so that entites that need to be stored in temporal tables can inherit from any base class (multiple interface
    /// implementations are allowed, but not multiple base class inheritance). However, for entities that don't need a base class,
    /// this <see cref="TemporalTable"/> class provides a default implementation for convenience.
    /// </remarks>
    public abstract class TemporalTable : ITemporalTable {
        public DateTime SysStartTime { get; set ; }
        public DateTime SysEndTime { get; set; }
    }
}
