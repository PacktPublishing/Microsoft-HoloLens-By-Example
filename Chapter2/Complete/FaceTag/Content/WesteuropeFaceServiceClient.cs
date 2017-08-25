using Microsoft.ProjectOxford.Face;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace FaceTag.Content
{
    internal class WesteuropeFaceServiceClient : FaceServiceClient
    {
        protected override string ServiceHost
        {
            get
            {
                return "https://westeurope.api.cognitive.microsoft.com/face/v1.0";
            }
        }

        public WesteuropeFaceServiceClient(string subscriptionKey) : base(subscriptionKey)
        {
        }

    }
}
