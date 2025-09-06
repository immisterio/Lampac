namespace Shared.Models.Events
{
    public class EventsModel()
    {
        public string LoadKit { get; set; }

        public EventModelMiddleware Middleware { get; set; }

        public EventModelController Controller { get; set; }

        public EventHttp Http { get; set; }
    }

    public class EventModelController()
    {
        public string BadInitialization { get; set; }

        public EventModelAppReplace AppReplace { get; set; }
    }

    public class EventModelAppReplace()
    {
        public EventModelAppReplaceComand online { get; set; }

        public EventModelAppReplaceComand sisi { get; set; }

        public EventModelAppReplaceComand appjs { get; set; }

        public EventModelAppReplaceComand appcss { get; set; }
    }

    public class EventModelAppReplaceComand()
    {
        public Dictionary<string, string> list { get; set; }

        public Dictionary<string, string> regex { get; set; }

        public string eval { get; set; }
    }

    public class EventModelMiddleware()
    {
        public string first { get; set; }

        public string end { get; set; }
    }

    public class EventHttp()
    {
        public string Handler { get; set; }

        public string Headers { get; set; }

        public string Response { get; set; }
    }
}
