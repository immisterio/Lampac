using Newtonsoft.Json.Linq;
using System;

namespace Shared.Engine.reCAPTCHA
{
    public class reCAPTCHAv2 : AnticaptchaBase, IAnticaptchaTaskProtocol
    {
        public Uri WebsiteUrl { protected get; set; }
        public string WebsiteKey { protected get; set; }
        public string WebsiteSToken { protected get; set; }

        public override JObject GetPostData()
        {
            return new JObject
            {
                {"type", "NoCaptchaTaskProxyless"},
                {"websiteURL", WebsiteUrl},
                {"websiteKey", WebsiteKey},
                {"websiteSToken", WebsiteSToken}
            };
        }

        public TaskResultResponse.SolutionData GetTaskSolution()
        {
            return TaskInfo.Solution;
        }
    }
}
