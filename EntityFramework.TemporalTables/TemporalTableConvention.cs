using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure.DependencyResolution;
using System.Data.Entity.ModelConfiguration.Conventions;
using System.Data.Entity.ModelConfiguration.Utilities;
using System.Data.Entity.Utilities;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace EntityFramework.TemporalTables {
    /// <summary>
    /// Code First conventions that finds the EDM types that implement the <see cref="ITemporalTable"/> interface and
    /// then adds an "HistoryTableName" annotation to the table which  causes the
    /// <see cref="TemporalTableSqlServerMigrationSqlGenerator"/> to create a temporal table rather than a regular table
    /// for the entity.
    /// </summary>
    /// <remarks>
    /// When an entity class implementes the ITemporalTable interface, then we look for the <see cref="TemporalTableAttribute"/>
    /// attribute on the entity class. If the attribute is found, then the HistoryTableName annotation is set to the
    /// history table name specified through the attribute; when the attribute is not found, then the HistoryTableName
    /// annotation is set to DEFAULT; the <see cref="TemporalTableSqlServerMigrationSqlGenerator"/> will then repalce the
    /// DEFAULT name with a naming convention: The singularized entity table name with a "History" suffix (e.g. ProductHistory
    /// when the table name is Products).
    /// </remarks>
    public class TemporalTableConvention : Convention {
        /// <summary>
        /// Mapping from entity name (which should be unique in EF Code First) to CLR type object.
        /// </summary>
        /// <remarks>
        /// While finding all the entity types that must map to a temporal table, we build this mapping.
        /// The mapping is then used by the <see cref="TemporalTableTvfConvention"/> convention as it needs access to
        /// the CLR type 
        /// </remarks>
        private static Dictionary<string, Type> mapping = new Dictionary<string, Type>();

        internal static Type GetClrType(string entityName) {
            return mapping[entityName];
        }


        internal static bool IsTemporalTableType(string entityName) {
            return mapping.ContainsKey(entityName);
        }

        /// <summary>
        /// Constructs a convention that will create an "HistoryTableName" table annotation when an entity type implements
        /// the ITemporalTable interface
        /// </summary>
        public TemporalTableConvention() {
            Types().Having(t => t.GetInterfaces().Where(i => i == typeof(ITemporalTable)).ToList()).Configure(
                (ctc, lst) => {
                    if (lst.Any()) {
                        // For some reason I don't understand (EF bug/inefficiency?) this gets called multiple times
                        // for the same type.
                        if (!mapping.ContainsKey(ctc.ClrType.Name)) mapping.Add(ctc.ClrType.Name, ctc.ClrType);
                        // The presence of the HistoryTableName annotation is also the flag to indicate to the
                        // TemporalTableSqlServerMigrationSqlGenerator class that a temporal table must be used.
                        // So we have to check for a TemporalTableAttribute on the class and use the history table
                        // name from there. When not present, then we set the HistoryTableName annotation value
                        // to DEFAULT, in which case the history table will be named as the singularized temporal table
                        // name with the History Suffix (e.g. for a Products table, it will be ProductHistory).
                        var tta = ctc.ClrType.GetCustomAttributes(typeof(TemporalTableAttribute), true);
                        if (tta != null && tta.Count() > 0) {
                            var attrib = (TemporalTableAttribute)tta[0];
                            ctc.HasTableAnnotation("HistoryTableName", string.IsNullOrWhiteSpace(attrib.HistoryTableName) ? "DEFAULT" : attrib.HistoryTableName);
                        } else {
                            ctc.HasTableAnnotation("HistoryTableName", "DEFAULT");
                        }
                        // Tell entity framework that the database inserts and updates the value of the period columns whenever
                        // the record is inserted or updated.
                        ctc.Property("SysStartTime").HasDatabaseGeneratedOption(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Computed);
                        ctc.Property("SysEndTime").HasDatabaseGeneratedOption(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Computed);
                    }
                });
        }

        /// <summary>
        /// Register the <see cref="TemporalTableConvention"/> convention with the <see cref="DbModelBuilder"/>.
        /// </summary>
        /// <remarks>
        /// This should be called from the <see cref="DbContext.OnModelCreating(DbModelBuilder)"/> override of the
        /// <see cref="DbContext"/> implementation.
        /// </remarks>
        public static void RegisterWithModelBuilder(DbModelBuilder builder) {
            builder.Conventions.Add(new TemporalTableConvention());
            builder.Conventions.Add(new TemporalTableTvfConvention());
        }
    }
}
