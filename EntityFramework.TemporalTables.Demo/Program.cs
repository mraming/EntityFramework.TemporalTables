//using EntityFramework.TemporalTable.Demo./*DomainEntities*/
using EntityFramework.TemporalTable.Demo.Migrations;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EntityFramework.TemporalTables;
using EntityFramework.TemporalTable.Demo.DomainEntities;

namespace EntityFramework.TemporalTable.Demo {
    class Program {
        static void Main(string[] args) {
            using (var ctx = new TemporalTableDbContext()) {

                Database.SetInitializer(new MigrateDatabaseToLatestVersion<TemporalTableDbContext, Configuration>());

                //ctx.Products.Add(new Product { Id = Guid.NewGuid(), Name = "TestProduct" });

                //ctx.SaveChanges();

                var id = new Guid("038fe225-964b-48da-8467-7cce810eea5b");
                var query = ctx.Products.AsOf(new DateTime(2017, 04, 04, 15, 38, 21)).Where(p => p.Id == id);

                foreach (var p in query) Console.WriteLine($"Name = {p.Name}, EanCode = {p.EanCode}");
            }
        }
    }
}
