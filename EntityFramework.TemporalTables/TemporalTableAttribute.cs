using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;
using System.Linq;

namespace EntityFramework.TemporalTables {

    /// <summary>
    /// Derivative of the <see cref="TableAttribute"/> to specify the name of the history table for System Versioned
    /// Temporal Tables
    /// </summary>
    /// <remarks>
    /// This attribute can be used on entity types that implement the <see cref="ITemporalTable"/> interface to override
    /// the default generated name of the history table (the default name is the singularized table name with the "History"
    /// suffix.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class TemporalTableAttribute : TableAttribute{

        /// <summary>
        /// Name of the history table where older version of the temporal table values are automatically being stored
        /// by SqlServer.
        /// </summary>
        public string HistoryTableName { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalTableAttribute"/> class with the specified table name
        /// and history table name.
        /// </summary>
        /// <param name="name">Name of the main temporal data table where the current version of the records are stored</param>
        /// <param name="historTableName">Name of the history table where old versions are automatically archived.</param>
        public TemporalTableAttribute(string name, string historTableName) : base(name) {
            HistoryTableName = historTableName;
        }
    }
}
