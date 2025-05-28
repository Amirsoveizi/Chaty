using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Chaty.Models
{
    public class Group
    {
        [Key]
        public int GroupId { get; set; }

        [Required]
        [MaxLength(100)]
        public string GroupName { get; set; }
        public virtual ICollection<UserGroup> UserGroups { get; set; }
        public virtual ICollection<ChatMessage> ChatMessages { get; set; }

        public Group()
        {
            UserGroups = new HashSet<UserGroup>();
            ChatMessages = new HashSet<ChatMessage>();
        }
    }
}