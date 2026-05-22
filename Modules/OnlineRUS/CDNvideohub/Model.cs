namespace CDNvideohub;

public class RootObject
{
    public bool isSerial { get; set; }

    public Item[] items { get; set; }
}

public class Item
{
    public short season { get; set; }

    public short episode { get; set; }

    public string voiceStudio { get; set; }

    public string voiceType { get; set; }

    public string vkId { get; set; }
}
