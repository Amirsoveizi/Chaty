using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Chaty.Models
{
    public class ChatMessage
    {
        [Key]
        public int MessageId { get; set; }

        [Required]
        public string MessageContent { get; set; }

        public DateTime Timestamp { get; set; }

        [Required]
        public string SenderId { get; set; }
        [ForeignKey("SenderId")]
        public virtual User Sender { get; set; }

        public string SenderUserName { get; set; }


        public string ReceiverId { get; set; }
        [ForeignKey("ReceiverId")]
        public virtual User Receiver { get; set; }

        public int? GroupId { get; set; }
        [ForeignKey("GroupId")]
        public virtual Group TargetGroup { get; set; } 

        public ChatMessage()
        {
            Timestamp = DateTime.UtcNow;
        }
    }
}