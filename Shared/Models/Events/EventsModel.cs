namespace Shared.Models.Events
{
    public class EventsModel()
    {
        public string LoadKit { get; set; }

        public string PidTor { get; set; }

        public string StreamQualityTpl { get; set; }

        public string StreamQualityFirts { get; set; }

        public EventModelMiddleware Middleware { get; set; }

        public EventModelController Controller { get; set; }

        public EventModelHttp Http { get; set; }

        public EventModelRedApi RedApi { get; set; }

        public EventModelHybridCache HybridCache { get; set; }

        public EventModelProxyApi ProxyApi { get; set; }

        public EventModelTranscoding Transcoding { get; set; }

        public EventModelRch Rch { get; set; }

        public EventModelNws Nws { get; set; }
    }

    public class EventModelController()
    {
        public string BadInitialization { get; set; }

        public EventModelAppReplace AppReplace { get; set; }

        public string Externalids { get; set; }

        public string HostStreamProxy { get; set; }

        public string MyLocalIp { get; set; }

        public string HttpHeaders { get; set; }
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

    public class EventModelHttp()
    {
        public string Handler { get; set; }

        public string Headers { get; set; }

        public string Response { get; set; }
    }

    public class EventModelRedApi()
    {
        public string AddTorrents { get; set; }
    }

    public class EventModelHybridCache()
    {
        public string Read { get; set; }

        public string Write { get; set; }
    }

    public class EventModelProxyApi()
    {
        public string CreateHttpRequest { get; set; }
    }

    public class EventModelTranscoding()
    {
        public string CreateProcess { get; set; }
    }

    public class EventModelRch()
    {
        public string Registry { get; set; }

        public string Disconnected { get; set; }
    }

    public class EventModelNws()
    {
        public string Connected { get; set; }

        public string Disconnected { get; set; }
    }
}
