using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;

namespace Chaty.Models
{
    public class ChatContext : DbContext
    {
        public ChatContext() : base("name=DefaultConnection") { }

        public DbSet<User> Users { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<UserGroup> UserGroups { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<UserGroup>()
                .HasKey(ug => new { ug.UserId, ug.GroupId });

            modelBuilder.Entity<UserGroup>()
                .HasRequired(ug => ug.User)
                .WithMany(u => u.UserGroups)
                .HasForeignKey(ug => ug.UserId)
                .WillCascadeOnDelete(false); 

            modelBuilder.Entity<UserGroup>()
                .HasRequired(ug => ug.Group)
                .WithMany(g => g.UserGroups)
                .HasForeignKey(ug => ug.GroupId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ChatMessage>()
                .HasRequired(m => m.Sender)
                .WithMany(u => u.SentMessages)
                .HasForeignKey(m => m.SenderId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ChatMessage>()
                .HasOptional(m => m.Receiver)
                .WithMany(u => u.ReceivedPrivateMessages)
                .HasForeignKey(m => m.ReceiverId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ChatMessage>()
                .HasOptional(m => m.TargetGroup) 
                .WithMany(g => g.ChatMessages)
                .HasForeignKey(m => m.GroupId)
                .WillCascadeOnDelete(false);
        }
    }
}