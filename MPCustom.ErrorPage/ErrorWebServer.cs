using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MPCustom.Core.Logging;

namespace MPCustom.ErrorPage
{
    public class ErrorWebServer : IErrorWebServer
    {
        private HttpListener? _listener;
        private CancellationTokenSource? _cts;
        private readonly ILoggerService _logger;
        private bool _isRunning;

        public bool IsRunning => _isRunning;

        public ErrorWebServer(ILoggerService logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task StartAsync(int port = 4030)
        {
            if (_isRunning) return Task.CompletedTask;

            try
            {
                _listener = new HttpListener();

                // Listen on configured port
                _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                _listener.Prefixes.Add($"http://localhost:{port}/");

                // Also attempt listening on port 80 for HTTP browser traffic redirected via hosts
                if (port != 80)
                {
                    try
                    {
                        _listener.Prefixes.Add("http://127.0.0.1:80/");
                        _listener.Prefixes.Add("http://localhost:80/");
                    }
                    catch
                    {
                        // Ignore if port 80 is occupied by another local service
                    }
                }

                _listener.Start();

                _cts = new CancellationTokenSource();
                _isRunning = true;

                _logger.LogInfo("ErrorPageServer", $"Generic Error HTTP Web Server started on port {port} (and HTTP port 80 where available)");

                _ = ListenLoopAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError("ErrorPageServer", "Failed to start HTTP Error Web Server", ex);
                _isRunning = false;
            }

            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            if (!_isRunning) return Task.CompletedTask;

            try
            {
                _cts?.Cancel();
                _listener?.Stop();
                _listener?.Close();
                _logger.LogInfo("ErrorPageServer", "Generic Error HTTP Web Server stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError("ErrorPageServer", "Error stopping HTTP Error Web Server", ex);
            }
            finally
            {
                _isRunning = false;
            }

            return Task.CompletedTask;
        }

        private async Task ListenLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _listener != null && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = ProcessRequestAsync(context);
                }
                catch (HttpListenerException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError("ErrorPageServer", "Error receiving request", ex);
                }
            }
        }

        private async Task ProcessRequestAsync(HttpListenerContext context)
        {
            try
            {
                var response = context.Response;
                response.StatusCode = (int)HttpStatusCode.Forbidden; // HTTP 403
                response.ContentType = "text/html; charset=utf-8";

                string htmlContent = GetGenericErrorHtml();
                byte[] buffer = Encoding.UTF8.GetBytes(htmlContent);

                response.ContentLength64 = buffer.Length;
                using var output = response.OutputStream;
                await output.WriteAsync(buffer, 0, buffer.Length);
            }
            catch
            {
                // Silence client disconnection errors
            }
        }

        public static string GetGenericErrorHtml()
        {
            return @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>This site can’t be reached</title>
    <style>
        body {
            font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif;
            background-color: #f7f9fa;
            color: #202124;
            margin: 0;
            padding: 0;
            display: flex;
            align-items: center;
            justify-content: center;
            height: 100vh;
        }
        .container {
            max-width: 540px;
            padding: 40px;
            background: #ffffff;
            border: 1px solid #e0e0e0;
            border-radius: 8px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.05);
        }
        .icon {
            width: 48px;
            height: 48px;
            fill: #ea4335;
            margin-bottom: 20px;
        }
        h1 {
            font-size: 22px;
            font-weight: 500;
            margin: 0 0 12px 0;
            color: #202124;
        }
        p {
            font-size: 14px;
            line-height: 1.6;
            color: #5f6368;
            margin: 0 0 24px 0;
        }
        .error-code {
            font-size: 13px;
            font-weight: 600;
            color: #70757a;
            letter-spacing: 0.5px;
            text-transform: uppercase;
            border-top: 1px solid #f1f3f4;
            padding-top: 16px;
        }
    </style>
</head>
<body>
    <div class=""container"">
        <svg class=""icon"" viewBox=""0 0 24 24"">
            <path d=""M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z""/>
        </svg>
        <h1>This site can’t be reached</h1>
        <p>The connection was unsuccessful. Please check your network connection, proxy, and firewall settings.</p>
        <div class=""error-code"">HTTP ERROR 403</div>
    </div>
</body>
</html>";
        }
    }
}
