using Shared.Models.Module;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using IO = System.IO;
using Shared;
using System.Threading.Tasks;
using Shared.Engine;

namespace Lampac.Controllers
{
    public class AdminController : BaseController
    {
        #region admin / auth
        [Route("admin")]
        [Route("admin/auth")]
        public ActionResult Authorization([FromForm]string parol)
        {
			if (AppInit.rootPasswd == "termux")
			{
                HttpContext.Response.Cookies.Append("passwd", "termux");
                return renderAdmin();
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

                if (AppInit.rootPasswd == parol.Trim())
				{
					HttpContext.Response.Cookies.Append("passwd", parol.Trim());
					return Redirect("/admin");
				}
            }

			if (HttpContext.Request.Cookies.TryGetValue("passwd", out string passwd) && passwd == AppInit.rootPasswd)
				return renderAdmin();

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
		<input type=""text"" name=""parol"" placeholder=""пароль из файла passwd""></input>
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

        ActionResult renderAdmin()
		{
            string adminHtml = IO.File.Exists("wwwroot/mycontrol/index.html") ? IO.File.ReadAllText("wwwroot/mycontrol/index.html") : IO.File.ReadAllText("wwwroot/control/index.html");
            return Content(adminHtml, contentType: "text/html; charset=utf-8");
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
        public Task ManifestInstallHtml(string online, string sisi, string jac, string dlna, string tracks, string ts, string catalog, string merch, string eng)
        {
            HttpContext.Response.ContentType = "text/html; charset=utf-8";

            if (AppInit.rootPasswd == "termux")
                return HttpContext.Response.WriteAsync("В termux операция недоступна");

            bool isEditManifest = false;

			if (IO.File.Exists("module/manifest.json"))
			{
                if (HttpContext.Request.Cookies.TryGetValue("passwd", out string passwd) && passwd == AppInit.rootPasswd)
                {
                    isEditManifest = true;
                }
                else
                {
                    HttpContext.Response.Redirect("/admin");
                    return Task.CompletedTask;
                }
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

                    #region JacRed.conf
                    if (jac == "fdb")
                    {
                        var jacPath = "module/JacRed.conf";

                        JObject jj;
                        if (IO.File.Exists(jacPath))
                        {
                            string txt = IO.File.ReadAllText(jacPath).Trim();
                            if (string.IsNullOrEmpty(txt))
                                jj = new JObject();
                            else
                            {
                                if (!txt.StartsWith("{"))
                                    txt = "{" + txt + "}";

                                try
                                {
                                    jj = JsonConvert.DeserializeObject<JObject>(txt) ?? new JObject();
                                }
                                catch
                                {
                                    jj = new JObject();
                                }
                            }
                        }
                        else
                        {
                            jj = new JObject();
                        }

                        jj["typesearch"] = "red";
                        IO.File.WriteAllText(jacPath, JsonConvert.SerializeObject(jj, Formatting.Indented));
                    }
                    #endregion
                }

                if (dlna == "on")
                    modules.Add("{\"enable\":true,\"dll\":\"DLNA.dll\"}");

                if (tracks == "on")
                    modules.Add("{\"enable\":true,\"initspace\":\"Tracks.ModInit\",\"dll\":\"Tracks.dll\"}");

                if (ts == "on")
                    modules.Add("{\"enable\":true,\"initspace\":\"TorrServer.ModInit\",\"dll\":\"TorrServer.dll\"}");

                if (catalog == "on")
                    modules.Add("{\"enable\":true,\"initspace\":\"Catalog.ModInit\",\"dll\":\"Catalog.dll\"}");

                if (merch == "on")
                    modules.Add("{\"enable\":false,\"dll\":\"Merchant.dll\"}");

                IO.File.WriteAllText("module/manifest.json", $"[{string.Join(",", modules)}]");

                if (eng != "on")
                    UpdateInitConf(j => j["disableEng"] = true);

                if (isEditManifest)
                {
                    return HttpContext.Response.WriteAsync("Перезагрузите lampac для изменения настроек");
                }
                else
                {
                    #region frontend cloudflare
                    if (HttpContext.Request.Headers.TryGetValue("CF-Connecting-IP", out var xip) && !string.IsNullOrEmpty(xip))
                    {
                        UpdateInitConf(j =>
                        {
                            var listen = j["listen"] as JObject;
                            if (listen == null)
                            {
                                listen = new JObject();
                                j["listen"] = listen;
                            }

                            listen["frontend"] = "cloudflare";
                        });
                    }
                    #endregion

                    #region htmlSuccess
                    string passwdTxt = IO.File.Exists("passwd") ? IO.File.ReadAllText("passwd").Trim() : string.Empty;

                    #region shared_passwd
                    string sharedBlock = string.Empty;
                    if (IsLocalIp(requestInfo.IP) == false && IO.File.Exists("isdocker") == false)
                    {
                        string shared_passwd = CrypTo.unic(6).ToLower();

                        UpdateInitConf(j =>
                        {
                            var accsdb = j["accsdb"] as JObject;
                            if (accsdb == null)
                            {
                                accsdb = new JObject();
                                j["accsdb"] = accsdb;
                            }

                            accsdb["enable"] = true;
                            accsdb["shared_passwd"] = shared_passwd;
                        });

                        sharedBlock = $@"<div class=""block""><b>Авторизация в Lampa</b><br /><br />
                            Пароль: {shared_passwd}
                            <br><br>
                            Отключить авторизацию можно в init.conf (accsdb) или {host}/admin (пользователи) 
                        </div><hr />";
                    }
                    #endregion

                    string htmlSuccesds = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"" />
    <title>Настройка завершена</title>
</head>
<body>

<style type=""text/css"">
    * {{ box-sizing: border-box; outline: none; }}
    body {{ padding: 40px; font-family: sans-serif; }}
    h1 {{ color: #2b7a78; margin-bottom: 1em; text-align: center; }}
    hr {{ margin-top: 1em; margin-bottom: 2em; }}
    .block {{ margin-top: 20px; }}
    pre {{ background: #f5f5f5; padding: 12px; border-radius: 6px; white-space: pre-wrap; word-break: break-all; }}
</style>

<h1>Настройка завершена</h1>

{sharedBlock}

<div class=""block"">
    <b>Админ панель</b><br /><br />
    Aдрес: {host}/admin<br />
    Пароль: {passwdTxt}
</div>

<hr />

<div class=""block"">
    <div style=""margin-top:10px""> 
        <b>Media Station X</b><br /><br />
        Settings -> Start Parameter -> Setup<br />
        Enter current ip address and port: {HttpContext.Request.Host.Value}<br /><br />
        Убрать/Добавить адреса можно в /home/lampac/msx.json
    </div>
</div>

<hr />

<div class=""block"">
    <b>Виджет для Samsung</b><br /><br />
    {host}/samsung.wgt
</div>

<hr />

<div class=""block"">
    <b>Для android apk</b><br /><br />
    Зажмите кнопку назад и введите новый адрес: {host}
</div>

<hr />

<div class=""block"">
    <b>Плагины для Lampa</b><br /><br />
    Заходим в настройки - расширения, жмем на кнопку ""добавить плагин"". В окне ввода вписываем адрес плагина {host}/on.js и перезагружаем виджет удерживая кнопку ""назад"" пока виджет не закроется.
</div>

<hr />

<div class=""block"">
    <b>TorrServer (если установлен)</b><br /><br />
    {host}/ts
</div>

</body>
</html>";

                    return HttpContext.Response.WriteAsync(htmlSuccesds).ContinueWith(t => Program.Reload());
                    #endregion
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
    <meta charset=""utf-8"" />
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
			&nbsp; &nbsp; &nbsp; <input name='eng' type='checkbox' checked /> ENG балансеры
		</div>
		<div class='flex'>
			<input name='sisi' type='checkbox' {IsChecked("SISI.dll", "checked")} /> Клубничка 18+, PornHub, Xhamster, etc
		</div>
		<div class='flex'>
			<input name='catalog' type='checkbox' {IsChecked("Catalog.dll", "checked")} /> Альтернативные источники каталога cub и tmdb
		</div>
		<div class='flex'>
			<input name='dlna' type='checkbox' {IsChecked("DLNA.dll", "checked")} /> DLNA - Загрузка торрентов и просмотр медиа файлов с локального устройства 
		</div>
		<div class='flex'>
			<input name='ts' type='checkbox' {IsChecked("TorrServer.dll", "checked")} /> TorrServer - возможность просматривать торренты в онлайн 
		</div>
		<div class='flex'>
			<input name='tracks' type='checkbox' {IsChecked("Tracks.dll", "checked")} /> Tracks - транскодинг видео и замена названий аудиодорожек с rus1, rus2 на читаемые LostFilm, HDRezka, etc
		</div>
		<div class='flex'>
			<input name='merch' type='checkbox' {IsChecked("Merchant.dll", "")} /> Автоматизация оплаты FreeKassa, Streampay, Litecoin, CryptoCloud
		</div>

		<br><br>
		<label>Поиск торрентов</label>
		<div class='flex'>
			<input name='jac' type='radio' value='webapi' checked /> Быстрый поиск по внешним базам JacRed, Rutor, Kinozal, NNM-Club, Rutracker, etc
		</div>
		<div class='flex'>
			<input name='jac' type='radio' value='fdb' /> Локальный jacred.xyz (не рекомендуется ставить на домашние устройства) - 2GB HDD
		</div>
	</div>
	
	<button type='submit'>{(isEditManifest ? "Изменить настройки" : "Завершить настройку")}</button></form></body></html>";
            }
            #endregion

            return HttpContext.Response.WriteAsync(renderHtml());
        }
        #endregion


        #region UpdateInitConf
        void UpdateInitConf(Action<JObject> modify)
        {
            JObject jo;

            if (IO.File.Exists("init.conf"))
            {
                string initconf = IO.File.ReadAllText("init.conf").Trim();
                if (string.IsNullOrEmpty(initconf))
                    jo = new JObject();

                else
                {
                    if (!initconf.StartsWith("{"))
                        initconf = "{" + initconf + "}";

                    try
                    {
                        jo = JsonConvert.DeserializeObject<JObject>(initconf) ?? new JObject();
                    }
                    catch
                    {
                        jo = new JObject();
                    }
                }
            }
            else
            {
                jo = new JObject();
            }

            modify?.Invoke(jo);

            IO.File.WriteAllText("init.conf", JsonConvert.SerializeObject(jo, Formatting.Indented));
        }
        #endregion

        #region IsLocalIp
        bool IsLocalIp(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip))
                return false;

            // Если ip приходит в формате IPv4-mapped IPv6 (::ffff:192.168.0.1)
            var lastColon = ip.LastIndexOf(':');
            if (lastColon != -1 && ip.Contains("."))
                ip = ip.Substring(lastColon + 1);

            if (!System.Net.IPAddress.TryParse(ip, out var addr))
                return false;

            // loopback (127.0.0.0/8 и ::1)
            if (System.Net.IPAddress.IsLoopback(addr))
                return true;

            if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) // IPv4
            {
                var b = addr.GetAddressBytes(); // [a,b,c,d]
                                                // 10.0.0.0/8
                if (b[0] == 10) return true;
                // 127.0.0.0/8
                if (b[0] == 127) return true;
                // 192.168.0.0/16
                if (b[0] == 192 && b[1] == 168) return true;
                // 172.16.0.0/12  => 172.16.0.0 - 172.31.255.255
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;

                return false;
            }

            if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6) // IPv6
            {
                var b = addr.GetAddressBytes();
                // unique local fc00::/7 (first byte 0xfc or 0xfd)
                if ((b[0] & 0xfe) == 0xfc) return true;
                // ::1 handled by IsLoopback above
                return false;
            }

            return false;
        }
        #endregion
    }
}