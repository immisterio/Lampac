using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared;

namespace Lampac.Controllers
{
    public class WebLogController : BaseController
    {
        [HttpGet]
        [AllowAnonymous]
        [Route("weblog")]
        public ActionResult WebLog(string token, string pattern, string receive = "http")
        {
            if (!AppInit.conf.weblog.enable)
                return Content("Включите weblog в init.conf\n\n\"weblog\": {\n   \"enable\": true\n}", contentType: "text/plain; charset=utf-8");

            if (!string.IsNullOrEmpty(AppInit.conf.weblog.token) && token != AppInit.conf.weblog.token)
                return Content("Используйте /weblog?token=my_key\n\n\"weblog\": {\n   \"enable\": true,\n   \"token\": \"my_key\"\n}", contentType: "text/plain; charset=utf-8");

            string html = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <title>weblog</title>
    <style>
        details summary {{
          font-weight: bold;
          font-size: 1.2em;
          color: #2772d2;
          cursor: pointer;
          list-style: none;
          user-select: none;
          position: relative;
        }}

        details summary:hover {{
          color: #1a4bb8;
        }}
    </style>
</head>
<body style='margin: 0px;'>
    <div id='controls' style='margin-bottom: 1em; background: #f0f0f0; padding: 10px; border-bottom: 1px solid #ccc;'>
        <label style='margin-right: 20px;'>Запросы:
            <select id='receiveSelect' style='padding: 0px 5px 0px 0px;'>
                <option value='http' {(receive == "http" ? "selected" : "")}>Исходящие</option>
                <option value='request' {(receive == "request" ? "selected" : "")}>Входящие</option>
            </select>
        </label>
        <label for='patternInput'>Фильтр: </label>
        <input type='text' id='patternInput' placeholder='rezka.ag' value='{pattern ?? ""}' style='margin-right: 20px;' />
    </div>
    <div id='log'></div>
    <script src='/js/nws-client-es5.js'></script>
    <script>
        let pattern = document.getElementById('patternInput').value.trim();
        let receive = document.getElementById('receiveSelect').value;

        document.getElementById('patternInput').addEventListener('input', e => {{
            pattern = e.target.value.trim();
        }});
        document.getElementById('receiveSelect').addEventListener('change', e => {{
            receive = e.target.value;
        }});

        function send(message) {{
            if (pattern && message.indexOf(pattern) === -1) return;

            var messageHtml = message.replace(/</g, '&lt;').replace(/>/g, '&gt;');

            const markers = [
              {{ text: 'CurrentUrl: ', caseSensitive: true }},
              {{ text: 'StatusCode: ', caseSensitive: true }},
              {{ text: '&lt;!doctype html&gt;', caseSensitive: false }}
            ];

            for (const marker of markers) {{
              let searchText = marker.text;
              let messageText = messageHtml;
  
              if (!marker.caseSensitive)
                messageText = messageHtml.toLowerCase();
  
              const index = messageText.indexOf(searchText);
              if (index !== -1) {{
                messageHtml = messageHtml.slice(0, index) 
                  + '<details><summary>Показать содержимое</summary>'
                  + messageHtml.slice(index) 
                  + '</details>';
                break;
              }}
            }}

            var par = document.getElementById('log');

            let messageElement = document.createElement('hr');
            messageElement.style.cssText = ' margin-bottom: 2.5em; margin-top: 2.5em;';
            par.insertBefore(messageElement, par.children[0]);

            messageElement = document.createElement('pre');
            messageElement.style.cssText = 'padding: 10px; background: cornsilk; white-space: pre-wrap; word-wrap: break-word;';
            if (messageHtml.indexOf('<details>') !== -1)
                messageElement.innerHTML = messageHtml;
            else 
                messageElement.innerText = message;
            par.insertBefore(messageElement, par.children[0]);
        }}


        let outageReported = false;
        function reportOutageOnce(message) {{
            if (!outageReported) {{
                send(message);
                outageReported = true;
            }}
        }}

        const client = new NativeWsClient(""/nws"", {{
            autoReconnect: true,
            reconnectDelay: 2000,

            onOpen: function () {{
                send('WebSocket connected');
                outageReported = false;
                client.invoke('RegistryWebLog', '{token}');
            }},

            onClose: function () {{
                reportOutageOnce('Connection closed');
            }},

            onError: function (err) {{
                reportOutageOnce('Connection error: ' + (err && err.message ? err.message : String(err)));
            }}
        }});

        client.on('Receive', function (message, e) {{
            if (receive === e) send(message);
        }});

        client.connect();
    </script>
</body>
</html>";

            return Content(html, "text/html; charset=utf-8");
        }
    }
}