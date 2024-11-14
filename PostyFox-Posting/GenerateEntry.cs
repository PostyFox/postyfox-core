using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PostyFox_Posting
{
    public class GenerateEntry
    {
        public string PostId { get; set; }  
        public string User { get; set; }
        public string Template { get; set; }

        public string TriggerSource { get; set; }
        public DateTime? PostAt { get; set; }
    }
}
