using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ProjectOxford.Vision;

namespace AssistantItemFinder.Content
{ 
    internal class WesteuropeFaceServiceClient : VisionServiceClient
    {
        protected override string ServiceHost
        {
            get
            {
                return "https://westeurope.api.cognitive.microsoft.com/vision/v1.0";
            }
        }

        public WesteuropeFaceServiceClient(string subscriptionKey) : base(subscriptionKey)
        {
        }

    }
}
