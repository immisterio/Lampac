﻿using Jackett;
using JacRed.Models;
using JacRed.Models.AppConf;
using Lampac.Engine;
using Lampac.Models.AppConf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JacRed.Engine
{
    public class JacBaseController : BaseController
    {
        public static RedConf red => ModInit.conf.Red;

        public static JacConf jackett => ModInit.conf.Jackett;


        async public static Task<bool> Joinparse(ConcurrentBag<TorrentDetails> torrents, Func<ValueTask<List<TorrentDetails>>> parse)
        {
            var result = await parse();

            if (result != null && result.Count > 0)
            {
                foreach (TorrentDetails torrent in result)
                    torrents.Add(torrent);

                return true;
            }

            return false;
        }

        public static void consoleErrorLog(string plugin)
        {
            Console.WriteLine($"JacRed: InternalServerError - {plugin}");
        }
    }
}
