using System;
using System.Collections.Concurrent;
using System.IO;
using Jackett;
using JacRed.Engine.CORE;
using JacRed.Models;

namespace JacRed.Engine
{
    public partial class FileDB : IDisposable
    {
        string fdbkey;

        public ConcurrentDictionary<string, TorrentDetails> Database = new ConcurrentDictionary<string, TorrentDetails>();

        FileDB(string key, bool empty = false)
        {
            fdbkey = key;
            string fdbpath = pathDb(key);
             
            if (!empty && File.Exists(fdbpath))
                Database = JsonStream.Read<ConcurrentDictionary<string, TorrentDetails>>(fdbpath) ?? new ConcurrentDictionary<string, TorrentDetails>();
        }
        

        public void Dispose()
        {
            if (Database.Count > 0)
                JsonStream.Write(pathDb(fdbkey), Database);

            if (openWriteTask.TryGetValue(fdbkey, out WriteTaskModel val))
            {
                val.openconnection -= 1;
                if (val.openconnection <= 0 && !ModInit.conf.Red.evercache.enable)
                    openWriteTask.TryRemove(fdbkey, out _);
            }
        }
    }
}
