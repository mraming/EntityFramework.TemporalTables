using System;
using System.ComponentModel.DataAnnotations;
using EntityFramework.TemporalTables;

namespace EntityFramework.TemporalTable.Demo.DomainEntities {

    //[TemporalTable("Products", "ProductHistory")]
    public class Product : EntityFramework.TemporalTables.TemporalTable {
        public Guid Id { get; set; }

        [StringLength(50)]
        public string Name { get; set; }

        public string EanCode { get; set; }
    }
}
