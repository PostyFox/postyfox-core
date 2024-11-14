using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Twitch.Net.EventSub.Models
{
    public class SubscribeCallbackResponse
    {
        public HttpStatusCode StatusCode { get; set; }
        public SubscribeCallbackModel? CallBack { get; set; }

        public SubscribeCallbackResponse(HttpStatusCode statusCode)
        {
            this.StatusCode = statusCode;
        }

        public SubscribeCallbackResponse(HttpStatusCode statusCode, SubscribeCallbackModel callBack)
        {
            this.StatusCode = statusCode;
            this.CallBack = callBack;
        }
    }
}
