using Lampac.Engine;
using Lampac.Models.Module;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Engine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IO = System.IO;

namespace Lampac.Controllers
{
    public class AdminController : BaseController
    {
        #region admin / auth
        [Route("admin")]
        [Route("admin/auth")]
        public ActionResult Authorization([FromForm]string parol)
        {
			if (IO.File.ReadAllText("passwd") == "termux")
			{
                HttpContext.Response.Cookies.Append("passwd", "termux");
                return Content(IO.File.ReadAllText("wwwroot/control/index.html"), contentType: "text/html; charset=utf-8");
            }

            if (!string.IsNullOrEmpty(parol))
			{
                string ipKey = $"Accsdb:auth:IP:{requestInfo.IP}";
                if (!memoryCache.TryGetValue(ipKey, out HashSet<string> passwds))
                    passwds = new HashSet<string>();

                passwds.Add(parol);
                memoryCache.Set(ipKey, passwds, DateTime.Today.AddDays(1));

                if (passwds.Count > 5)
                    return Content("Too many attempts, try again tomorrow.");

                if (IO.File.ReadAllText("passwd") == parol.Trim())
				{
					HttpContext.Response.Cookies.Append("passwd", parol.Trim());
					return Redirect("/admin");
				}
            }

			if (HttpContext.Request.Cookies.TryGetValue("passwd", out string passwd) && passwd == FileCache.ReadAllText("passwd"))
				return Content(IO.File.ReadAllText("wwwroot/control/index.html"), contentType: "text/html; charset=utf-8");

            string html = @"
<!DOCTYPE html>
<html>
<head>
	<title>Authorization</title>
</head>
<body>

<style type=""text/css"">
	* {
	    box-sizing: border-box;
	    outline: none;
	}
	body{
		padding: 40px;
		font-family: sans-serif;
	}
	label{
		display: block;
		font-weight: 700;
		margin-bottom: 8px;
	}
	input,
	textarea,
	select{
		width: 340px;
		padding: 8px;
	}
	button{
		padding: 10px;
	}
	form > * + *{
		margin-top: 20px;
	}
</style>

<form method=""post"" action=""/admin/auth"" id=""form"">
	<div>
		<input type=""text"" name=""parol"" placeholder=""пароль из passwd""></input>
	</div>
	
	<button type=""submit"">войти</button>
	
</form>

<div style=""margin-top: 4em;""><b style=""color: cadetblue;"">Выполните одну из команд через ssh</b><br><br>
	cat /home/lampac/passwd<br><br>
	docker exec -it lampac cat passwd
</div>

</body>
</html>
";

            return Content(html, contentType: "text/html; charset=utf-8");
        }
        #endregion


        #region init
        [Route("admin/init/save")]
        public ActionResult InitSave([FromForm]string json)
        {
			try
            {
                JsonConvert.DeserializeObject<AppInit>(json);
            }
			catch (Exception ex) { return Json(new { error = true, ex = ex.Message }); }

            var jo = JsonConvert.DeserializeObject<JObject>(json);

			try
			{
				Directory.CreateDirectory("cache/backup/init");
                IO.File.WriteAllText($"cache/backup/init/{DateTime.Now.ToString("dd-MM-yyyy.HH")}.conf", JsonConvert.SerializeObject(jo, Formatting.Indented));
            }
			catch { }

			JToken users = null;
            var accsdbNode = jo["accsdb"] as JObject;
            if (accsdbNode != null)
            {
                var usersNode = accsdbNode["users"];
                if (usersNode != null)
                {
					users = usersNode.DeepClone();
                    accsdbNode.Remove("users");

                    IO.File.WriteAllText("users.json", JsonConvert.SerializeObject(users, Formatting.Indented));
                }
            }

            IO.File.WriteAllText("init.conf", JsonConvert.SerializeObject(jo, Formatting.Indented));

            return Json(new { success = true });
        }

        [Route("admin/init/custom")]
        public ActionResult InitCustom()
        {
			string json = IO.File.Exists("init.conf") ? IO.File.ReadAllText("init.conf") : null;
			if (json != null && !json.Trim().StartsWith("{"))
				json = "{" + json + "}";

            var ob = json != null ? JsonConvert.DeserializeObject<JObject>(json) : new JObject { };
            return ContentTo(JsonConvert.SerializeObject(ob));
        }

        [Route("admin/init/current")]
        public ActionResult InitCurrent()
        {
            return Content(JsonConvert.SerializeObject(AppInit.conf), contentType: "application/json; charset=utf-8");
        }

        [Route("admin/init/default")]
        public ActionResult InitDefault()
        {
            return Content(JsonConvert.SerializeObject(new AppInit()), contentType: "application/json; charset=utf-8");
        }

        [Route("admin/init/example")]
        public ActionResult InitExample()
        {
            return Content(IO.File.Exists("example.conf") ? IO.File.ReadAllText("example.conf") : string.Empty);
        }
        #endregion

        #region sync/init
        [Route("admin/sync/init")]
        public ActionResult Synchtml()
        {
            string html = @"
<!DOCTYPE html>
<html>
<head>
	<title>Редактор sync.conf</title>
</head>
<body>

<style type=""text/css"">
	* {
	    box-sizing: border-box;
	    outline: none;
	}
	body{
		padding: 40px;
		font-family: sans-serif;
	}
	label{
		display: block;
		font-weight: 700;
		margin-bottom: 8px;
	}
	input,
	textarea,
	select{
		width: 100%;
		padding: 10px;
	}
	button{
		padding: 10px;
	}
	form > * + *{
		margin-top: 30px;
	}
</style>

<form method=""post"" action="""" id=""form"">
	<div>
		<label>Ваш sync.conf
		<textarea id=""value"" name=""value"" rows=""30"">{conf}</textarea>
	</div>
	
	<button type=""submit"">Сохранить</button>
	
</form>

<script type=""text/javascript"">
	document.getElementById('form').addEventListener(""submit"", (e) => {
		let json = document.getElementById('value').value

		e.preventDefault()

		try{
			let formData = new FormData()
				formData.append('json', json)

			fetch('/admin/sync/init/save',{
			    method: ""POST"",
			    body: formData
			})
			.then((response)=>{
				if (!response.ok) {
					return response.json().then(err => {
						throw new Error(err.ex || 'Не удалось сохранить настройки');
					});
				}
				return response.json();
			 })  
			.then((data)=>{
				if (data.success) {
					alert('Сохранено');
				} else if (data.error) {
					throw new Error(data.ex); 
				} else {
					throw new Error('Не удалось сохранить настройки'); 
				}
			})
			.catch((e)=>{
				alert(e.message)
			})
		}
		catch(e){
			alert('Ошибка: ' + e.message)
		}
	})
</script>

</body>
</html>
";

            string conf = IO.File.Exists("sync.conf") ? IO.File.ReadAllText("sync.conf") : string.Empty;
            return Content(html.Replace("{conf}", conf), contentType: "text/html; charset=utf-8");
        }


        [Route("admin/sync/init/save")]
        public ActionResult SyncSave([FromForm] string json)
        {
            try
            {
                string testjson = json.Trim();
                if (!testjson.StartsWith("{"))
                    testjson = "{" + testjson + "}";

                JsonConvert.DeserializeObject<AppInit>(testjson);

            }
            catch (Exception ex) { return Json(new { error = true, ex = ex.Message }); }

            IO.File.WriteAllText("sync.conf", json);
            return Json(new { success = true });
        }
        #endregion

        #region manifest
        [Route("admin/manifest/install")]
        public ActionResult ManifestInstallHtml(string online, string sisi, string jac, string dlna, string tracks, string ts, string merch, string eng)
        {
			bool isEditManifest = false;

			if (IO.File.Exists("module/manifest.json"))
			{
				if (HttpContext.Request.Cookies.TryGetValue("passwd", out string passwd) && passwd == FileCache.ReadAllText("passwd")) 
				{
                    isEditManifest = true;
                }
				else
                    return Redirect("/admin");
            }

			if (HttpContext.Request.Method == "POST")
			{
				var modules = new List<string>(10);

				if (online == "on")
					modules.Add("{\"enable\":true,\"dll\":\"Online.dll\"}");

                if (sisi == "on")
                    modules.Add("{\"enable\":true,\"dll\":\"SISI.dll\"}");

				if (!string.IsNullOrEmpty(jac))
				{
					modules.Add("{\"enable\":true,\"initspace\":\"Jackett.ModInit\",\"dll\":\"JacRed.dll\"}");

					if (jac == "fdb")
                        IO.File.WriteAllText("module/JacRed.conf", "\"typesearch\": \"red\"");
                }

                if (dlna == "on")
                    modules.Add("{\"enable\":true,\"dll\":\"DLNA.dll\"}");

                if (tracks == "on")
                    modules.Add("{\"enable\":true,\"initspace\":\"Tracks.ModInit\",\"dll\":\"Tracks.dll\"}");

                if (ts == "on")
                    modules.Add("{\"enable\":true,\"initspace\":\"TorrServer.ModInit\",\"dll\":\"TorrServer.dll\"}");

                if (merch == "on")
                    modules.Add("{\"enable\":false,\"dll\":\"Merchant.dll\"}");

                IO.File.WriteAllText("module/manifest.json", $"[{string.Join(",", modules)}]");

                #region ENG
                if (eng == "on")
                {
					if (IO.File.Exists("init.conf"))
					{
						string initconf = IO.File.ReadAllText("init.conf").Trim();
						if (initconf != null)
						{
                            if (initconf.StartsWith("{"))
                                IO.File.WriteAllText("init.conf", "{\"firefox\":{\"enable\":true}," + initconf.Remove(0, 1));
							else
                                IO.File.WriteAllText("init.conf", "\"firefox\":{\"enable\":true}," + initconf);
                        }
                    }
					else
					{
						IO.File.WriteAllText("init.conf", "\"firefox\":{\"enable\":true}");
					}
                }
				#endregion

				if (isEditManifest)
				{
                    return Content("Перезагрузите lampac для изменения настроек", contentType: "text/plain; charset=utf-8");
				}
				else
				{
                    #region frontend cloudflare
                    if (HttpContext.Request.Headers.TryGetValue("CF-Connecting-IP", out var xip) && !string.IsNullOrEmpty(xip))
					{
                        if (IO.File.Exists("init.conf"))
                        {
                            string initconf = IO.File.ReadAllText("init.conf").Trim();
                            if (initconf != null)
                            {
                                if (initconf.StartsWith("{"))
                                    IO.File.WriteAllText("init.conf", "{\"frontend\":\"cloudflare\"," + initconf.Remove(0, 1));
                                else
                                    IO.File.WriteAllText("init.conf", "\"frontend\":\"cloudflare\"," + initconf);
                            }
                        }
                        else
                        {
                            IO.File.WriteAllText("init.conf", "\"frontend\":\"cloudflare\"");
                        }
                    }
                    #endregion

                    Program.Reload();
                    return Redirect("/admin/auth");
                }
            }

            #region renderHtml
            string renderHtml()
			{
				var modules = IO.File.Exists("module/manifest.json") ? JsonConvert.DeserializeObject<List<RootModule>>(IO.File.ReadAllText("module/manifest.json")) : null;

				string IsChecked(string name, string def)
				{
					if (modules == null)
						return def;	

                    bool res = modules.FirstOrDefault(m => m.dll == name)?.enable ?? false;
                    return res ? "checked" : string.Empty;
                }

                return $@"
<!DOCTYPE html>
<html>
<head>
	<title>Модули</title>
</head>
<body>

<style type='text/css'>
	* {{
	    box-sizing: border-box;
	    outline: none;
	}}
	body{{
		padding: 40px;
		font-family: sans-serif;
	}}
	label{{
		display: block;
		font-weight: 700;
		margin-bottom: 8px;
	}}
	input,
	select{{
		margin: 10px;
		margin-left: 0px;
	}}
	button{{
		padding: 10px;
	}}
	form > * + *{{
		margin-top: 30px;
	}}
	.flex{{
		display: flex;
		align-items: center;
	}}
</style>

<form method='post' action='/admin/manifest/install' id='form'>
	<div>
		<label>Установка модулей</label>
		<div class='flex'>
			<input name='online' type='checkbox' {IsChecked("Online.dll", "checked")} /> Онлайн балансеры Rezka, Filmix, etc
		</div>
		<div class='flex'>
			&nbsp; &nbsp; &nbsp; <input name='eng' type='checkbox' /> ENG балансеры, требуется дополнительно 1GB HDD и 1.5Gb RAM
		</div>
		<div class='flex'>
			<input name='sisi' type='checkbox' {IsChecked("SISI.dll", "checked")} /> Клубничка 18+, PornHub, Xhamster, etc
		</div>
		<div class='flex'>
			<input name='dlna' type='checkbox' {IsChecked("DLNA.dll", "checked")} /> DLNA - Загрузка торрентов и просмотр медиа файлов с локального устройства 
		</div>
		<div class='flex'>
			<input name='ts' type='checkbox' {IsChecked("TorrServer.dll", "checked")} /> TorrServer - возможность просматривать торренты в онлайн 
		</div>
		<div class='flex'>
			<input name='tracks' type='checkbox' {IsChecked("Tracks.dll", "checked")} /> Tracks - Заменяет название аудиодорожек и субтитров с rus1, rus2 на читаемые LostFilm, HDRezka, etc
		</div>
		<!--<div class='flex'>
			<input name='jac' type='checkbox' /> Локальный jacred.xyz (не рекомендуется ставить на домашние устройства из за частых операций с диском)
		</div>-->
		<div class='flex'>
			<input name='merch' type='checkbox' {IsChecked("Merchant.dll", "")} /> Автоматизация оплаты FreeKassa, Streampay, Litecoin, CryptoCloud
		</div>

		<br><br>
		<label>Поиск торрентов</label>
		<div class='flex'>
			<input name='jac' type='radio' value='webapi' checked /> Быстрый поиск по внешним базам JacRed, Rutor, Kinozal, NNM-Club, Rutracker, etc
		</div>
		<div class='flex'>
			<input name='jac' type='radio' value='fdb' /> Локальный jacred.xyz (не рекомендуется ставить на домашние устройства) - 10GB HDD
		</div>
	</div>
	
	<button type='submit'>{(isEditManifest ? "Изменить настройки" : "Завершить настройку")}</button></form></body></html>";
            }
            #endregion

            return Content(renderHtml(), contentType: "text/html; charset=utf-8");
        }
        #endregion
    }
}