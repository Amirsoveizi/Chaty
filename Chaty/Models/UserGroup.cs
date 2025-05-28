using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Chaty.Models
{
    public class UserGroup
    {
        [Key, Column(Order = 0)]
        public string UserId { get; set; }

        [Key, Column(Order = 1)]
        public int GroupId { get; set; }
        public virtual User User { get; set; }
        public virtual Group Group { get; set; }
    }
}