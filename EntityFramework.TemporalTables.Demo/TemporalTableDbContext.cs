using EntityFramework.TemporalTable.Demo.DomainEntities;
using EntityFramework.TemporalTables;
using System.Data.Entity;

namespace EntityFramework.TemporalTable.Demo {
    public class TemporalTableDbContext : DbContext {

        public DbSet<Product> Products { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder) {

            TemporalTableConvention.RegisterWithModelBuilder(modelBuilder);
            base.OnModelCreating(modelBuilder);
        }

    }
}