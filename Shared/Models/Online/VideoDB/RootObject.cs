﻿namespace Shared.Model.Online.VideoDB
{
    public class RootObject
    {
        public string title { get; set; }

        public string file { get; set; }

        public string subtitle { get; set; }

        public List<Folder> folder { get; set; }
    }
}
