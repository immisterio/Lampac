using Newtonsoft.Json.Linq;

namespace Shared.Engine.reCAPTCHA
{
    public interface IAnticaptchaTaskProtocol
    {
        JObject GetPostData();

        TaskResultResponse.SolutionData GetTaskSolution();
    }
}
