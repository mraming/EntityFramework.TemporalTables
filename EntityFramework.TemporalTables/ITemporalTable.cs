using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityFramework.TemporalTables {
    /// <summary>
    /// Entity Classes that implement the <see cref="ITemporalTable"/> interface are created as System Versioned temporal
    /// table in SQL Server 2016.
    /// </summary>
    /// <remarks>
    /// By implementing the <see cref="ITemporalTable"/>, a System Versioned Temporal table will be used instead
    /// of a regular data table. This is only supported on SqlServer 2016 and up and Sql Azure.
    /// 
    /// To use this interface, the <see cref="TemporalTableConvention"/> EF Code First convention must be registerd with 
    /// the <see cref="System.Data.Entity.DbModelBuilder"/> by calling the <see cref="TemporalTableConvention.RegisterWithModelBuilder(DbModelBuilder)"/>
    /// method from a <see cref="DbContext.OnModelCreating(DbModelBuilder)"/> implementation so that the correct annotations
    /// are generated in the schema migration steps for use by the  <see cref="TemporalTableSqlServerMigrationSqlGenerator"/> class.
    /// 
    /// Also, a modified Migration SQL Generator class must be registerd in the constructor of the implementation (i.e. 
    /// derived class) of the <see cref="System.Data.Entity.Migrations.DbMigrationsConfiguration{TContext}"/> class:
    /// <code>SetSqlGenerator("System.Data.SqlClient", new TemporalTableSqlServerMigrationSqlGenerator());</code>
    /// 
    /// For entites that don't need to derive another base class, the <see cref="TemporalTable"/> base class can be used
    /// as it provides a default implementation of the <see cref="ITemporalTable"/> interface.
    /// </remarks>
    public interface ITemporalTable {
        DateTime SysStartTime { get; set; }

        DateTime SysEndTime { get; set; }
    }
}
