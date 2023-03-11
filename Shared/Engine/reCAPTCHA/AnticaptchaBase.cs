using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;

namespace Shared.Engine.reCAPTCHA
{
    public abstract class AnticaptchaBase
    {
        public enum ProxyTypeOption
        {
            Http,
            Socks4,
            Socks5
        }

        private const string Host = "api.anti-captcha.com";
        private const SchemeType Scheme = SchemeType.Https;
        public string ErrorMessage { get; private set; }
        public int TaskId { get; private set; }
        public string ClientKey { set; private get; }
        public TaskResultResponse TaskInfo { get; protected set; }
        public abstract JObject GetPostData();

        public bool CreateTask()
        {
            var taskJson = GetPostData();

            if (taskJson == null)
                return false;

            var jsonPostData = new JObject();
            jsonPostData["clientKey"] = ClientKey;
            jsonPostData["task"] = taskJson;

            dynamic postResult = JsonPostRequest(ApiMethod.CreateTask, jsonPostData);

            if (postResult == null || postResult.Equals(false))
                return false;

            var response = new CreateTaskResponse(postResult);

            if (!response.ErrorId.Equals(0))
            {
                ErrorMessage = response.ErrorDescription;
                return false;
            }

            if (response.TaskId == null)
                return false;

            TaskId = (int)response.TaskId;
            return true;
        }

        public bool WaitForResult(int maxSeconds = 120, int currentSecond = 0)
        {
            if (currentSecond >= maxSeconds)
                return false;

            if (currentSecond.Equals(0))
            {
                Thread.Sleep(3000);
            }
            else
            {
                Thread.Sleep(1000);
            }

            var jsonPostData = new JObject();
            jsonPostData["clientKey"] = ClientKey;
            jsonPostData["taskId"] = TaskId;

            dynamic postResult = JsonPostRequest(ApiMethod.GetTaskResult, jsonPostData);

            if (postResult == null || postResult.Equals(false))
                return false;

            TaskInfo = new TaskResultResponse(postResult);

            if (!TaskInfo.ErrorId.Equals(0))
            {
                ErrorMessage = TaskInfo.ErrorDescription;
                return false;
            }

            if (TaskInfo.Status.Equals(TaskResultResponse.StatusType.Processing))
                return WaitForResult(maxSeconds, currentSecond + 1);

            if (TaskInfo.Status.Equals(TaskResultResponse.StatusType.Ready))
            {
                if (TaskInfo.Solution.GRecaptchaResponse == null && TaskInfo.Solution.Text == null
                    && TaskInfo.Solution.Answers == null && TaskInfo.Solution.Token == null &&
                    TaskInfo.Solution.Challenge == null && TaskInfo.Solution.Seccode == null &&
                    TaskInfo.Solution.Validate == null && TaskInfo.Solution.CellNumbers.Count == 0)
                {
                    return false;
                }

                return true;
            }

            ErrorMessage = "An unknown API status, please update your software";
            return false;
        }

        private dynamic JsonPostRequest(ApiMethod methodName, JObject jsonPostData)
        {
            string error;
            var methodNameStr = char.ToLowerInvariant(methodName.ToString()[0]) + methodName.ToString().Substring(1);

            dynamic data = HttpHelper.Post(
                new Uri(Scheme + "://" + Host + "/" + methodNameStr),
                JsonConvert.SerializeObject(jsonPostData, Formatting.Indented),
                out error
            );

            if (string.IsNullOrEmpty(error))
                if (data == null)
                    ErrorMessage = "Got empty or invalid response from API";
                else
                    return data;
            else
                ErrorMessage = "HTTP or JSON error: " + error;

            return false;
        }

        private enum ApiMethod
        {
            CreateTask,
            GetTaskResult,
            GetBalance
        }

        private enum SchemeType
        {
            Http,
            Https
        }
    }
}
