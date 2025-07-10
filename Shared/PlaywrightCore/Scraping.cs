using Lampac;
using Shared.Engine;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Http;
using Titanium.Web.Proxy.Models;

namespace Shared.PlaywrightCore
{
    public class Scraping : IDisposable
    {
        Process process;

        ExplicitProxyEndPoint explicitEndPoint;
        ProxyServer proxyServer;

        string patternUrl, headerKey;

        public bool IsCompleted { get; set; }

        public Action<SessionEventArgs> OnRequest { get; set; }

        public Action<SessionEventArgs> OnResponse { get; set; }


        public Scraping(string targetUrl, string patternUrl, string headerKey, string proxyBypassList = "*.example.com")
        {
            this.patternUrl = patternUrl;
            this.headerKey = headerKey;

            if (Chromium.Status == PlaywrightStatus.NoHeadless)
            {
                proxyServer = new ProxyServer();
                proxyServer.BeforeRequest += Request;
                proxyServer.BeforeResponse += Response;

                if (!File.Exists("data/titanium.pfx"))
                {
                    // Генерируем корневой сертификат (если еще не создан)
                    if (proxyServer.CertificateManager.RootCertificate == null)
                        proxyServer.CertificateManager.CreateRootCertificate();

                    // Получаем корневой сертификат
                    X509Certificate2 rootCert = proxyServer.CertificateManager.RootCertificate;

                    // Сохраняем в PFX-файл (с паролем)
                    byte[] certBytes = rootCert.Export(X509ContentType.Pkcs12, "35sd85454gfd");
                    File.WriteAllBytes("data/titanium.pfx", certBytes);
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && !File.Exists("/usr/local/share/ca-certificates/lampac_titanium.pfx"))
                    File.Copy("data/titanium.pfx", "/usr/local/share/ca-certificates/lampac_titanium.pfx", true);

                proxyServer.CertificateManager.LoadRootCertificate("data/titanium.pfx", "35sd85454gfd");
                explicitEndPoint = new ExplicitProxyEndPoint(System.Net.IPAddress.Loopback, 0, true);
                proxyServer.AddEndPoint(explicitEndPoint);
                proxyServer.Start();

                #region executablePath
                string executablePath = AppInit.conf.chromium.executablePath;

                if (string.IsNullOrEmpty(executablePath))
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                            executablePath = ".playwright\\chrome-win32\\chrome.exe";
                        else
                            executablePath = ".playwright\\chrome-win\\chrome.exe";
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        executablePath = ".playwright/chrome-mac/Chromium.app/Contents/MacOS/Chromium";
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        executablePath = ".playwright/chrome-linux/chrome";
                    }
                }

                if (string.IsNullOrEmpty(executablePath) || !File.Exists(executablePath))
                    return;
                #endregion

                int proxyPort = explicitEndPoint.Port;

                var startInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = $"--proxy-server=127.0.0.1:{proxyPort} " +
                                $"--proxy-bypass-list=\"localhost;127.0.0.1;*.microsoft.com;{proxyBypassList}\" " +
                                $"--ignore-certificate-errors " +
                                $"--ignore-ssl-errors " +
                                $"--no-first-run " +
                                $"--no-default-browser-check " +
                                $"--disable-background-mode " +
                                (AppInit.conf.chromium.DEV || AppInit.conf.chromium.Args == null ? "--window-position=100,100 " : $"--window-position=-2000,100 ") +
                                $"\"{targetUrl}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = false
                };

                process = Process.Start(startInfo);
                if (process == null)
                    return;
            }
        }


        async private Task Request(object sender, SessionEventArgs e)
        {
            var session = e.HttpClient.Request;

            OnRequest?.Invoke(e);

            if (IsCompleted)
            {
                e.Ok(string.Empty);
                return;
            }

            if (session.Method == "GET" && !string.IsNullOrEmpty(patternUrl) && Regex.IsMatch(session.Url, patternUrl))
            {
                IsCompleted = true;
                completionSource.TrySetResult(session);
                e.Ok(string.Empty);
                return;
            }

            if (!string.IsNullOrEmpty(headerKey))
            {
                foreach (var header in session.Headers)
                {
                    if (header.Name == headerKey)
                    {
                        IsCompleted = true;
                        completionSource.TrySetResult(session);
                        e.Ok(string.Empty);
                        return;
                    }
                }
            }

            if (AppInit.conf.chromium.consoleLog)
            {
                Console.WriteLine("=== HTTP ЗАПРОС ===");
                Console.WriteLine($"URL: {session.Url}");
                Console.WriteLine($"Метод: {session.Method}");
                Console.WriteLine("Заголовки:");
                foreach (var header in session.Headers)
                    Console.WriteLine($"  {header.Name}: {header.Value}");
                Console.WriteLine();
            }
        }

        async private Task Response(object sender, SessionEventArgs e)
        {
            OnResponse?.Invoke(e);

            if (AppInit.conf.chromium.consoleLog)
            {
                var session = e.HttpClient.Response;
                Console.WriteLine("=== HTTP ОТВЕТ ===");
                Console.WriteLine($"URL: {e.HttpClient.Request.Url}");
                Console.WriteLine($"Статус: {session.StatusCode} {session.StatusDescription}");
                Console.WriteLine("Заголовки:");
                foreach (var header in session.Headers)
                    Console.WriteLine($"  {header.Name}: {header.Value}");
                Console.WriteLine();
            }
        }


        #region WaitPageResult
        TaskCompletionSource<Request> completionSource { get; set; } = new TaskCompletionSource<Request>();

        async public Task<Request> WaitPageResult(int seconds = 10)
        {
            try
            {
                if (proxyServer == null)
                    return null;

                var completionTask = completionSource.Task;
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(seconds));

                var completedTask = await Task.WhenAny(completionTask, timeoutTask).ConfigureAwait(false);

                if (completedTask == completionTask)
                    return await completionTask;

                return null;
            }
            catch { return null; }
        }
        #endregion

        #region Dispose
        public void Dispose()
        {
            if (AppInit.conf.chromium.DEV)
                return;

            try
            {
                if (proxyServer != null)
                {
                    proxyServer.BeforeRequest -= Request;
                    proxyServer.BeforeResponse -= Response;

                    if (proxyServer.ProxyRunning)
                    {
                        proxyServer.Stop();
                        proxyServer.Dispose();
                    }
                }
            }
            catch { }


            try
            {
                if (process != null)
                {
                    process.Kill();
                    process.Close();
                    process.Dispose();
                }
            }
            catch { }
        }
        #endregion
    }
}
