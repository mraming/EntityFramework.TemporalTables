using EntityFramework.TemporalTables;
using System.Data.Entity.Migrations;

namespace EntityFramework.TemporalTable.Demo.Migrations {


    internal sealed class Configuration : DbMigrationsConfiguration<EntityFramework.TemporalTable.Demo.TemporalTableDbContext>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = false;
            SetSqlGenerator("System.Data.SqlClient", new TemporalTableSqlServerMigrationSqlGenerator());
        }

        protected override void Seed(EntityFramework.TemporalTable.Demo.TemporalTableDbContext context)
        {
            //  This method will be called after migrating to the latest version.

            //  You can use the DbSet<T>.AddOrUpdate() helper extension method 
            //  to avoid creating duplicate seed data. E.g.
            //
            //    context.People.AddOrUpdate(
            //      p => p.FullName,
            //      new Person { FullName = "Andrew Peters" },
            //      new Person { FullName = "Brice Lambson" },
            //      new Person { FullName = "Rowan Miller" }
            //    );
            //
        }
    }
}
