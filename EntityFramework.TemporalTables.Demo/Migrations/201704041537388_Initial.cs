namespace EntityFramework.TemporalTable.Demo.Migrations
{
    using System;
    using System.Collections.Generic;
    using System.Data.Entity.Infrastructure.Annotations;
    using System.Data.Entity.Migrations;
    
    public partial class Initial : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Products",
                c => new
                    {
                        Id = c.Guid(nullable: false),
                        Name = c.String(maxLength: 50),
                        EanCode = c.String(),
                        SysStartTime = c.DateTime(nullable: false),
                        SysEndTime = c.DateTime(nullable: false),
                    },
                annotations: new Dictionary<string, object>
                {
                    { "HistoryTableName", "DEFAULT" },
                })
                .PrimaryKey(t => t.Id);
            
        }
        
        public override void Down()
        {
            DropTable("dbo.Products",
                removedAnnotations: new Dictionary<string, object>
                {
                    { "HistoryTableName", "DEFAULT" },
                });
        }
    }
}
