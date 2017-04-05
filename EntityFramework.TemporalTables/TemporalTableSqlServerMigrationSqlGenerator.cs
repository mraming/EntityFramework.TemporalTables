using System;
using System.Data.Entity.Migrations.Model;
using System.Data.Entity.Migrations.Utilities;
using System.Data.Entity.SqlServer;
using System.Linq;


using EntityFramework.TemporalTables.Utilities;
using System.Data.Entity.Infrastructure.Pluralization;
using System.Data.Entity;
using System.Data.Entity.Infrastructure.Annotations;

namespace EntityFramework.TemporalTables {

    /// <summary>
    /// Specialization of the <see cref="SqlServerMigrationSqlGenerator"/> provider supporting entities that implement the
    /// <see cref="ITemporalTable"/> interface to create system versioned temporal tables in SqlServer 2016 and Sql Azure.
    /// This class converts provider agnostic migration operations into SQL commands that can be run against Microsoft SQL
    /// Server 2016 (and up) and Sql Azure.
    /// </summary>
    /// <remarks>
    /// When creating a temporal table, we automatically modify the <see cref="ITemporalTable.SysStartTime"/> and
    /// <see cref="ITemporalTable.SysEndTime"/> columns to database generated values as below:
    /// <code>
    /// [SysStartTime] DATETIME2 (7) GENERATED ALWAYS AS ROW START
    /// [SysEndTime] DATETIME2 (7) GENERATED ALWAYS AS ROW END
    /// PERIOD FOR SYSTEM_TIME ([SysStartTime], [SysEndTime])
    /// </code>
    /// 
    /// These column names cannot currently be changed through additional annotations to keep things simple.
    /// 
    /// This class will also automatically generate a table valued function (TVF) that's used to provide an IQueryable
    /// that selects the data AS OF a specific UTC system time. These functions are prefixed with the "efftt" prefix (EntityFramework.TemporalTable).
    /// 
    /// This Migration SQL Generator class must be registerd in the constructor of the implementation (i.e. 
    /// derived class) of the <see cref="System.Data.Entity.Migrations.DbMigrationsConfiguration{TContext}"/> class:
    /// <code>SetSqlGenerator("System.Data.SqlClient", new TemporalTableSqlServerMigrationSqlGenerator());</code>
    /// </remarks>
    public class TemporalTableSqlServerMigrationSqlGenerator : SqlServerMigrationSqlGenerator {
        private static readonly IPluralizationService pluralizationService = (IPluralizationService)DbConfiguration.DependencyResolver.GetService(typeof(IPluralizationService), null);

        /// <summary>
        /// Writes CREATE TABLE SQL to the target writer, adding support for Temporal Tables for entities implementing the
        /// <see cref="ITemporalTable"/> interface.
        /// </summary>
        /// <remarks>
        /// The <see cref="TemporalTableAttribute"/> can be used to override the default temporal table name. When the
        /// attribute is NOT defined on the entity, then the HistoryTableName annotation is set to DEFAULT. 
        /// The <see cref="getHistoryTableName(string, string)"/> will then implement a default naming convention for the
        /// temporal table history table using the singularized table name of the main table with the "History" suffix.
        /// 
        /// When the operation does not have a "HistoryTableName" annotation then this is a regular table and the base
        /// class implementation is being used. If there is a HistoryTableName annotation, then a new implementation is 
        /// a modified copy of the source code of the base class implementation.
        /// </remarks>
        /// <param name="createTableOperation"> The operation to produce SQL for. </param>
        /// <param name="writer"> The target writer. </param>
        protected override void WriteCreateTable(CreateTableOperation createTableOperation, IndentedTextWriter writer) {
            Check.NotNull(createTableOperation, "createTableOperation");
            Check.NotNull(writer, "writer");

            object objHistoryTable;

            // If we don't have an IsTemporalTable annotation (which we use as the flag to indicate that this must be a temporal table)
            // then we simply use the base class to limit the scope of this custom code.
            if (createTableOperation.Annotations != null && createTableOperation.Annotations.TryGetValue("HistoryTableName", out objHistoryTable) && !string.IsNullOrWhiteSpace(objHistoryTable as string)) {
                string historyTable = objHistoryTable as string;

                // This code is copied and modified from the EF Source code of our base class.
                writer.WriteLine("CREATE TABLE " + Name(createTableOperation.Name) + " (");
                writer.Indent++;
                
                // Skipping the SysStartTime and SysEndTime columns as these require additional options for system versioned
                // temporal tables.
                createTableOperation.Columns.Where(col => col.Name != "SysStartTime" && col.Name != "SysEndTime").Each(
                    (c, i) => {
                        Generate(c, writer);

                        // No need for checking whether the comma is required as we always write 2 additional columns.
                        writer.WriteLine(",");
                    });

                // Write the period start (closed) and end (open) columns for the used for the validity period.
                // For now, we're using fixed names to keep things simple.
                writer.WriteLine("[SysStartTime] DATETIME2 (7) GENERATED ALWAYS AS ROW START,");
                writer.WriteLine("[SysEndTime] DATETIME2 (7) GENERATED ALWAYS AS ROW END,");
                writer.WriteLine("PERIOD FOR SYSTEM_TIME ([SysStartTime], [SysEndTime]) ");

                if (createTableOperation.PrimaryKey != null) {
                    writer.WriteLine(",");
                    writer.Write("CONSTRAINT ");
                    writer.Write(Quote(createTableOperation.PrimaryKey.Name));
                    writer.Write(" PRIMARY KEY ");

                    if (!createTableOperation.PrimaryKey.IsClustered) {
                        writer.Write("NONCLUSTERED ");
                    }

                    writer.Write("(");
                    writer.Write(createTableOperation.PrimaryKey.Columns.Join(Quote));
                    writer.WriteLine(")");
                } else {
                    writer.WriteLine();
                }

                writer.Indent--;
                writer.Write($") WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = {getHistoryTableName(createTableOperation.Name, historyTable)}));");
            } else {
                base.WriteCreateTable(createTableOperation, writer);
            }
        }

        protected override void Generate(CreateTableOperation createTableOperation) {
            // Base class implementation first, which will call the WiteCreateTable override to create a temporal table
            // if the annotation is present. Once that's done, we then need to create a Table Valued Function (TVF) in
            // the database to select the values from the temporal table as of a particular System Date and Time (in UTC)
            base.Generate(createTableOperation);
            object annotationValue;
            if (createTableOperation.Annotations.TryGetValue("HistoryTableName", out annotationValue)) {
                Statement(getCreateAsOfTvfFunction(createTableOperation.Name));
            }
        }


        private string getCreateAsOfTvfFunction(string tableName) {
            var dbName = DatabaseName.Parse(tableName);
            return $"CREATE FUNCTION {getSchemaPrefixedAsOfTvfName(dbName)} (@utcSystemTime DATETIME2) RETURNS TABLE AS RETURN SELECT * FROM {tableName} FOR SYSTEM_TIME AS OF @utcSystemTime";
        }

        /// <summary>
        /// Override of base class implementation that implements support for handling changes to the HistoryTableName
        /// annotation (which is typically changed via the <see cref="TemporalTableAttribute"/> attribute on entities.
        /// </summary>
        protected override void Generate(AlterTableOperation alterTableOperation) {
            AnnotationValues annotation;
            if (alterTableOperation.Annotations.TryGetValue("HistoryTableName", out annotation)) {
                // There are 3 possibilities when this method is called:
                // 1. HistoryTable annotation value is changed, so history table is renamed.
                // 2. HistoryTable annotation is removed, so table is converted to a normal table.
                // 3. HistoryTable annotation is added, so table is converted to a Temporal table.

                var oldHistoryTableName = annotation.OldValue as string;
                var newHistoryTableName = annotation.NewValue as string;


                if (!string.IsNullOrWhiteSpace(oldHistoryTableName) && !string.IsNullOrWhiteSpace(newHistoryTableName)) {
                    // Rename the History Table
                    // We need to strip the schema name part of the new name.
                    var newDbName = getHistoryTableName(alterTableOperation.Name, newHistoryTableName);
                    Statement($"EXEC sp_rename '{getHistoryTableName(alterTableOperation.Name, oldHistoryTableName)}', '{newDbName.Name}';");
                } else if (string.IsNullOrWhiteSpace(oldHistoryTableName) && !string.IsNullOrWhiteSpace(newHistoryTableName)) {
                    throw new NotSupportedException("Cannot change an existing table into a Temporal Table because SysStartTime is not available for existing records.");
                    // We have several options here for future enhancement:
                    // - First check if the table isn't empty by any chance, in which case there is no problem adding the columns.
                    // - Second, check if the table doesn't already have the SysStartTime and SysEndTime period columns as that would also allow us to (re) add the temporal table.
                    //   Part of this could be to not drop these columns and the history table when it already exists.
                    //// Turn this into a temporal table
                    //// Need to add the Period Start- and End columns as part of this process
                    //using (var writer = Writer()) {
                    //    writer.WriteLine($"ALTER TABLE {Name(alterTableOperation.Name)} ADD ");
                    //    writer.WriteLine("[SysStartTime] DATETIME2 (7) GENERATED ALWAYS AS ROW START,");
                    //    writer.WriteLine("[SysEndTime] DATETIME2 (7) GENERATED ALWAYS AS ROW END,");
                    //    writer.WriteLine("PERIOD FOR SYSTEM_TIME ([SysStartTime], [SysEndTime]); ");
                    //    Statement(writer);
                    //}
                    //Statement($"ALTER TABLE {Name(alterTableOperation.Name)} SET ( SYSTEM_VERSIONING = ON(HISTORY_TABLE = {getSchemaPrefixedHistoryTableName(alterTableOperation.Name, newHistoryTableName)}));");
                    //
                    // TODO: Create the TVF to get the table data as of a particular system date and time in UTC.
                } else if (!string.IsNullOrWhiteSpace(oldHistoryTableName) && string.IsNullOrWhiteSpace(newHistoryTableName)) {
                    // Change Temporal table into normal table
                    Statement($"ALTER TABLE {Name(alterTableOperation.Name)} SET(SYSTEM_VERSIONING = OFF);");
                    // Consider not dropping the history table and period columns so we can add system versioning back at a later time.
                    Statement($"DROP TABLE {getHistoryTableName(alterTableOperation.Name, oldHistoryTableName)};");
                    Statement($"ALTER TABLE {Name(alterTableOperation.Name)} DROP PERIOD FOR SYSTEM_TIME;");
                    Statement($"ALTER TABLE {Name(alterTableOperation.Name)} DROP COLUMN SysStartTime, COLUMN SysEndTime;");
                } else {
                    throw new InvalidOperationException("Old and New value of HistoryTableName annotation cannot both be null, empty or whitespace only.");
                }
            }
            base.Generate(alterTableOperation);
        }

        private string getSchemaPrefixedAsOfTvfName(DatabaseName dbName) {
            return $"{dbName.Schema}.{getAsOfTvfName(dbName)}";
        }

        private string getAsOfTvfName(DatabaseName dbName) {
            return $"eftt{dbName.Name}AsOf";
        }


        protected override void Generate(RenameTableOperation renameTableOperation) {
            base.Generate(renameTableOperation);

            // If this table is a temporal table, then we also need to rename the TVF to get the data as of a particular
            // system date and time. Challenge is that we don't have easy access to the entity class or the EDM model.
            // So we'll simply check if there is function with a name that matches the original table name and then
            // rename it if it exists.
            using (var writer = Writer()) {
                DatabaseName dbOldName = DatabaseName.Parse(renameTableOperation.Name);
                var oldName = getSchemaPrefixedAsOfTvfName(dbOldName);
                var newName = getAsOfTvfName(new DatabaseName(renameTableOperation.NewName, dbOldName.Schema));

                writer.WriteLine();
                writer.Write("IF object_id('");
                writer.Write(oldName);
                writer.WriteLine("') IS NOT NULL BEGIN");
                writer.Indent++;
                writer.WriteLine($"DROP FUNCTION {oldName};");
                writer.Indent--;
                writer.WriteLine("END");

                Statement(writer);
                Statement(getCreateAsOfTvfFunction(renameTableOperation.NewName));
            }
        }

        /// <summary>
        /// Extract the schema name from the table name (or use dbo is not present) and prefix the history table name
        /// with that schema name, unless the history table name already has a schema name prefix.
        /// </summary>
        private DatabaseName getHistoryTableName(string tableName, string historyTableName) {
            var dbTableName = DatabaseName.Parse(tableName);
            if (string.IsNullOrWhiteSpace(historyTableName) || historyTableName == "DEFAULT") {
                // Use a default name: The singularized table name with the "History" suffix.
                historyTableName = pluralizationService.Singularize(dbTableName.Name) + "History";
            }

            var dbHistoryTableName = DatabaseName.Parse(historyTableName);

            if (!string.IsNullOrWhiteSpace(dbHistoryTableName.Schema)) return dbHistoryTableName;

            string schemaName = dbTableName.Schema;
            if (string.IsNullOrWhiteSpace(schemaName)) schemaName = "dbo"; // Default to the dbo schema.

            return new DatabaseName(dbHistoryTableName.Name, schemaName);
        }
    }
}
