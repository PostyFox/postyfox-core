using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PostyFox_NetCore
{
    public class ServiceDTO
    {
        public string? ServiceName { get; set; }
        public string? ServiceID { get; set; }
        public bool? IsEnabled { get; set; }
    }
}
