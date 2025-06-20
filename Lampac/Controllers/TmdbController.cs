﻿using Lampac.Engine;
using Microsoft.AspNetCore.Mvc;
using Shared.Engine;
using System.Web;

namespace Lampac.Controllers
{
    public class TmdbController : BaseController
    {
        [HttpGet]
        [Route("tmdbproxy.js")]
        [Route("tmdbproxy/js/{token}")]
        public ActionResult TmdbProxy(string token)
        {
            string file = FileCache.ReadAllText("plugins/tmdbproxy.js").Replace("{localhost}", host);
            file = file.Replace("{token}", HttpUtility.UrlEncode(token));

            return Content(file, contentType: "application/javascript; charset=utf-8");
        }
    }
}