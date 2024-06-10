using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using IO = System.IO;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Lampac.Controllers
{
    public class AdminController : BaseController
    {
        #region auth
        [Route("admin/auth")]
        public ActionResult Authorization([FromForm]string parol)
        {
            if (!string.IsNullOrEmpty(parol) && IO.File.ReadAllText("passwd") == parol.Trim())
			{
				HttpContext.Response.Cookies.Append("passwd", parol.Trim());
				return Redirect("/admin/init");
			}

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
		<input type=""text"" name=""parol"" placeholder=""пароль из /home/lampac/passwd or /home/passwd""></input>
	</div>
	
	<button type=""submit"">войти</button>
	
</form>

</body>
</html>
";

            return Content(html, contentType: "text/html; charset=utf-8");
        }
        #endregion

        #region init
        [Route("admin/init")]
        public ActionResult Inithtml()
        {
            string html = @"
<!DOCTYPE html>
<html>
<head>
	<title>Редактор init.conf</title>
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
		<label>Ваш init.conf - <a href=""/admin/init/current"" target=""_blank"" style=""text-decoration: inherit; color: cornflowerblue;"">системный</a></label>
		<textarea id=""value"" name=""value"" rows=""30"">{conf}</textarea>
	</div>
	
	<button type=""submit"">Сохранить</button>
	
</form>

<script type=""text/javascript"">
	document.getElementById('form').addEventListener(""submit"", (e) => {
		let json = document.getElementById('value').value

		e.preventDefault()

		try{
			JSON.parse(json)

			let formData = new FormData()
				formData.append('json', json)

			fetch('/admin/init/save',{
			    method: ""POST"",
			    body: formData
			})
			.then((response)=>{
				if(response.ok) return response.text();  

				throw new Error('Не удалось сохранить настройки');
			 })  
			.then(()=>{
				alert('Сохранено')
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

            string conf = IO.File.Exists("init.conf") ? IO.File.ReadAllText("init.conf") : string.Empty;
            return Content(html.Replace("{conf}", conf), contentType: "text/html; charset=utf-8");
        }


        [Route("admin/init/save")]
        public ActionResult InitSave([FromForm]string json)
        {
            IO.File.WriteAllText("init.conf", json);
            return Content(json, contentType: "application/json; charset=utf-8");
        }


        [Route("admin/init/current")]
        public ActionResult InitCurrent()
        {
            return Content(JsonConvert.SerializeObject(AppInit.conf, Formatting.Indented), contentType: "application/json; charset=utf-8");
        }

        [Route("admin/init/example")]
        public ActionResult InitExample()
        {
            return Content(IO.File.Exists("example.conf") ? IO.File.ReadAllText("example.conf") : string.Empty);
        }
        #endregion

        #region manifest
        [Route("admin/manifest/install")]
        public ActionResult ManifestInstallHtml(string online, string sisi, string jac, string dlna, string tracks, string ts, string merch, string localua)
        {
            if (IO.File.Exists("module/manifest.json"))
				return Content("Изменить список установленных модулей можно в файле /home/lampac/module/manifest.json");

			if (HttpContext.Request.Method == "POST")
			{
				var modules = new List<string>(10);

				if (online == "on")
					modules.Add("{\"enable\":true,\"dll\":\"Online.dll\"}");

                if (sisi == "on")
                    modules.Add("{\"enable\":true,\"dll\":\"SISI.dll\"}");

                if (jac == "on")
                    modules.Add("{\"enable\":true,\"initspace\":\"Jackett.ModInit\",\"dll\":\"JacRed.dll\"}");

                if (dlna == "on")
                    modules.Add("{\"enable\":true,\"dll\":\"DLNA.dll\"}");

                if (tracks == "on")
                    modules.Add("{\"enable\":true,\"initspace\":\"Tracks.ModInit\",\"dll\":\"Tracks.dll\"}");

                if (ts == "on")
                    modules.Add("{\"enable\":true,\"initspace\":\"TorrServer.ModInit\",\"dll\":\"TorrServer.dll\"}");

                if (merch == "on")
                    modules.Add("{\"enable\":false,\"dll\":\"Merchant.dll\"}");

                IO.File.WriteAllText("module/manifest.json", $"[{string.Join(",", modules)}]");

                #region localua
                if (localua == "on")
                {
					if (IO.File.Exists("init.conf"))
					{
						string initconf = IO.File.ReadAllText("init.conf");
						if (initconf != null && initconf.StartsWith("{")) 
							IO.File.WriteAllText("init.conf", "{\"Kodik\":{\"streamproxy\":true,\"apnstream\":true},\"iRemux\":{\"streamproxy\":true,\"apnstream\":true},\"Zetflix\":{\"streamproxy\":true,\"apnstream\":true,\"hls\":false}," + initconf.Remove(0, 1));
                    }
					else
					{
						IO.File.WriteAllText("init.conf", @"{
  ""mikrotik"": true,
  ""typecache"": ""mem"",
  ""serverproxy"": {
    ""allow_tmdb"": true,
    ""verifyip"": false
  },
  ""Kodik"": {
    ""streamproxy"": true,
	""apnstream"": true
  },
  ""iRemux"": {
	""streamproxy"": true,
	""apnstream"": true
  },
  ""Zetflix"": {
	""streamproxy"": true,
	""apnstream"": true,
    ""hls"": false
  }
}
");
					}
                }
                #endregion

                Program.Reload();

                return Redirect("/");
            }

            string html = @"
<!DOCTYPE html>
<html>
<head>
	<title>Завершите настройку</title>
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
	select{
		margin: 10px;
		margin-left: 0px;
	}
	button{
		padding: 10px;
	}
	form > * + *{
		margin-top: 30px;
	}
	.flex{
		display: flex;
		align-items: center;
	}
</style>

<form method=""post"" action=""/admin/manifest/install"" id=""form"">
	<div>
		<label>Установка модулей</label>
		<div class=""flex"">
			<input name=""online"" type=""checkbox"" checked /> Онлайн балансеры Rezka, Filmix, etc
		</div>
		<div class=""flex"">
			<input name=""sisi"" type=""checkbox"" checked /> Клубничка 18+, PornHub, Xhamster, etc
		</div>
		<div class=""flex"">
			<input name=""dlna"" type=""checkbox"" checked /> DLNA - Загрузка торрентов и просмотр медиа файлов с локального устройства 
		</div>
		<div class=""flex"">
			<input name=""ts"" type=""checkbox"" checked /> TorrServer - возможность просматривать торренты в онлайн 
		</div>
		<div class=""flex"">
			<input name=""tracks"" type=""checkbox"" checked /> Tracks - Заменяет название аудиодорожек и субтитров с rus1, rus2 на читаемые LostFilm, HDRezka, etc
		</div>
		<div class=""flex"">
			<input name=""jac"" type=""checkbox"" /> Локальный jacred.xyz (не рекомендуется ставить локально из за частых операций с диском)
		</div>
		<div class=""flex"">
			<input name=""merch"" type=""checkbox"" /> Автоматизация оплаты FreeKassa, Streampay, Litecoin, CryptoCloud
		</div>

		<br><br>
		<label>Начальная настройка init.conf</label>
		<div class=""flex"">
			<input name=""localua"" type=""checkbox"" /> Я из Украины и lampac установлен локально
		</div>
	</div>
	
	<button type=""submit"">Завершить настройку</button>
</form>
</body>
</html>
";

            return Content(html, contentType: "text/html; charset=utf-8");
        }
        #endregion
    }
}