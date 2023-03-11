using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shared.Engine.reCAPTCHA
{
    public class TaskResultResponse
    {
        public enum StatusType
        {
            Processing,
            Ready
        }

        public TaskResultResponse(dynamic json)
        {
            ErrorId = JsonHelper.ExtractInt(json, "errorId");

            if (ErrorId != null)
                if (ErrorId.Equals(0))
                {
                    Status = ParseStatus(JsonHelper.ExtractStr(json, "status"));

                    if (Status.Equals(StatusType.Ready))
                    {
                        Cost = JsonHelper.ExtractDouble(json, "cost");
                        Ip = JsonHelper.ExtractStr(json, "ip", null, true);
                        SolveCount = JsonHelper.ExtractInt(json, "solveCount", null, true);
                        CreateTime = UnixTimeStampToDateTime(JsonHelper.ExtractDouble(json, "createTime"));
                        EndTime = UnixTimeStampToDateTime(JsonHelper.ExtractDouble(json, "endTime"));

                        Solution = new SolutionData
                        {
                            Token = JsonHelper.ExtractStr(json, "solution", "token", true),
                            GRecaptchaResponse =
                                JsonHelper.ExtractStr(json, "solution", "gRecaptchaResponse", silent: true),
                            GRecaptchaResponseMd5 =
                                JsonHelper.ExtractStr(json, "solution", "gRecaptchaResponseMd5", silent: true),
                            Text = JsonHelper.ExtractStr(json, "solution", "text", silent: true),
                            Url = JsonHelper.ExtractStr(json, "solution", "url", silent: true),
                            Challenge = JsonHelper.ExtractStr(json, "solution", "challenge", silent: true),
                            Seccode = JsonHelper.ExtractStr(json, "solution", "seccode", silent: true),
                            Validate = JsonHelper.ExtractStr(json, "solution", "validate", silent: true),
                        };

                        try
                        {
                            Solution.CellNumbers = json["solution"]["cellNumbers"].ToObject<List<int>>();
                        }
                        catch
                        {
                            Solution.CellNumbers = new List<int>();
                        }

                        try
                        {
                            Solution.Answers = json.solution.answers;
                        }
                        catch
                        {
                            Solution.Answers = null;
                        }
                    }
                }
                else
                {
                    ErrorCode = JsonHelper.ExtractStr(json, "errorCode");
                    ErrorDescription = JsonHelper.ExtractStr(json, "errorDescription") ?? "(no error description)";
                }
        }

        public int? ErrorId { get; }
        public string ErrorCode { get; private set; }
        public string ErrorDescription { get; }
        public StatusType? Status { get; }
        public SolutionData Solution { get; }
        public double? Cost { get; private set; }
        public string Ip { get; private set; }

        /// <summary>
        ///     Task create time in UTC
        /// </summary>
        public DateTime? CreateTime { get; private set; }

        /// <summary>
        ///     Task end time in UTC
        /// </summary>
        public DateTime? EndTime { get; private set; }

        public int? SolveCount { get; private set; }

        private StatusType? ParseStatus(string status)
        {
            if (string.IsNullOrEmpty(status))
                return null;

            try
            {
                return (StatusType)Enum.Parse(
                    typeof(StatusType),
                    CultureInfo.CurrentCulture.TextInfo.ToTitleCase(status),
                    true
                );
            }
            catch
            {
                return null;
            }
        }

        private static DateTime? UnixTimeStampToDateTime(double? unixTimeStamp)
        {
            if (unixTimeStamp == null)
                return null;

            var dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

            return dtDateTime.AddSeconds((double)unixTimeStamp).ToUniversalTime();
        }

        public class SolutionData
        {
            public JObject Answers { get; internal set; } // Will be available for CustomCaptcha tasks only!
            public string GRecaptchaResponse { get; internal set; } // Will be available for Recaptcha tasks only!
            public string GRecaptchaResponseMd5 { get; internal set; } // for Recaptcha with isExtended=true property
            public string Text { get; internal set; } // Will be available for ImageToText tasks only!
            public string Url { get; internal set; }
            public string Token { get; internal set; } // Will be available for FunCaptcha tasks only!
            public string Challenge; // Will be available for GeeTest tasks only
            public string Seccode; // Will be available for GeeTest tasks only
            public string Validate; // Will be available for GeeTest tasks only
            public List<int> CellNumbers = new List<int>(); // Will be available for Square tasks only
        }
    }
}
