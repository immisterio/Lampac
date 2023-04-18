﻿using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.SISI;

namespace JinEnergy.SISI
{
    public class EpornerController : BaseController
    {
        [JSInvokable("epr")]
        async public static Task<dynamic> Index(string args)
        {
            string? search = arg("search", args);
            string? sort = arg("sort", args);
            int pg = int.Parse(arg("pg", args) ?? "1") + 1;

            string? html = await EpornerTo.InvokeHtml(AppInit.Eporner.corsHost(), search, sort, pg, url => JsHttpClient.Get(url));
            if (html == null)
                return OnError("html");

            return new
            {
                menu = EpornerTo.Menu(null, sort),
                list = EpornerTo.Playlist("epr/vidosik", html, picture => picture)
            };
        }


        [JSInvokable("epr/vidosik")]
        async public static Task<dynamic> Stream(string args)
        {
            var stream_links = await EpornerTo.StreamLinks(AppInit.Eporner.corsHost(), arg("uri", args), htmlurl => JsHttpClient.Get(htmlurl), jsonurl => JsHttpClient.Get(AppInit.Eporner.corsHost(jsonurl)));
            if (stream_links == null)
                return OnError("stream_links");

            return stream_links;
        }
    }
}