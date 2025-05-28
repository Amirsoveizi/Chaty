namespace Chaty.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class EnhancedChatModels : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.ChatMessages",
                c => new
                    {
                        MessageId = c.Int(nullable: false, identity: true),
                        MessageContent = c.String(nullable: false),
                        Timestamp = c.DateTime(nullable: false),
                        SenderId = c.String(nullable: false, maxLength: 128),
                        SenderUserName = c.String(),
                        ReceiverId = c.String(maxLength: 128),
                        GroupId = c.Int(),
                    })
                .PrimaryKey(t => t.MessageId)
                .ForeignKey("dbo.Users", t => t.ReceiverId)
                .ForeignKey("dbo.Users", t => t.SenderId)
                .ForeignKey("dbo.Groups", t => t.GroupId)
                .Index(t => t.SenderId)
                .Index(t => t.ReceiverId)
                .Index(t => t.GroupId);
            
            CreateTable(
                "dbo.Users",
                c => new
                    {
                        UserId = c.String(nullable: false, maxLength: 128),
                        UserName = c.String(nullable: false, maxLength: 256),
                    })
                .PrimaryKey(t => t.UserId)
                .Index(t => t.UserName, unique: true);
            
            CreateTable(
                "dbo.UserGroups",
                c => new
                    {
                        UserId = c.String(nullable: false, maxLength: 128),
                        GroupId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.UserId, t.GroupId })
                .ForeignKey("dbo.Groups", t => t.GroupId)
                .ForeignKey("dbo.Users", t => t.UserId)
                .Index(t => t.UserId)
                .Index(t => t.GroupId);
            
            CreateTable(
                "dbo.Groups",
                c => new
                    {
                        GroupId = c.Int(nullable: false, identity: true),
                        GroupName = c.String(nullable: false, maxLength: 100),
                    })
                .PrimaryKey(t => t.GroupId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.ChatMessages", "GroupId", "dbo.Groups");
            DropForeignKey("dbo.ChatMessages", "SenderId", "dbo.Users");
            DropForeignKey("dbo.ChatMessages", "ReceiverId", "dbo.Users");
            DropForeignKey("dbo.UserGroups", "UserId", "dbo.Users");
            DropForeignKey("dbo.UserGroups", "GroupId", "dbo.Groups");
            DropIndex("dbo.UserGroups", new[] { "GroupId" });
            DropIndex("dbo.UserGroups", new[] { "UserId" });
            DropIndex("dbo.Users", new[] { "UserName" });
            DropIndex("dbo.ChatMessages", new[] { "GroupId" });
            DropIndex("dbo.ChatMessages", new[] { "ReceiverId" });
            DropIndex("dbo.ChatMessages", new[] { "SenderId" });
            DropTable("dbo.Groups");
            DropTable("dbo.UserGroups");
            DropTable("dbo.Users");
            DropTable("dbo.ChatMessages");
        }
    }
}
