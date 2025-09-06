namespace Shared.Models.Events
{
    public class EventsModel()
    {
        public string LoadKit { get; set; }

        public EventModelController Controller { get; set; }
    }

    public class EventModelController()
    {
        public string BadInitialization { get; set; }
    }
}
