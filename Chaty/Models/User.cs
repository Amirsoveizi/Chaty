using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Chaty.Models
{
    public class User
    {
        [Key]
        public string UserId { get; set; } 

        [Required]
        [Index(IsUnique = true)]
        [MaxLength(256)]
        public string UserName { get; set; }

        // Navigation Properties
        public virtual ICollection<UserGroup> UserGroups { get; set; }
        public virtual ICollection<ChatMessage> SentMessages { get; set; }
        public virtual ICollection<ChatMessage> ReceivedPrivateMessages { get; set; }

        public User()
        {
            UserId = System.Guid.NewGuid().ToString(); 
            UserGroups = new HashSet<UserGroup>();
            SentMessages = new HashSet<ChatMessage>();
            ReceivedPrivateMessages = new HashSet<ChatMessage>();
        }
    }
}