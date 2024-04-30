using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PostyFox_Posting
{
    public class QueueEntry
    {
        public string RootPostId { get; set; }
        public string PostId { get; set; }  
        public string User { get; set; }
        public string? TargetPlatformServiceId { get; set; }
        public int Status { get; set; }
        public List<string> Media { get; set; }
        public DateTime? PostAt { get; set; }
    }
}
