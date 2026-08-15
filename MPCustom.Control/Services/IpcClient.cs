using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MPCustom.Control.Services
{
    public class IpcClient
    {
        private const string PipeName = "MPCustomPipe";

        public static async Task<string?> SendCommandAsync(string command, string payload = "")
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await client.ConnectAsync(1500);

                using var reader = new StreamReader(client, Encoding.UTF8, leaveOpen: true);
                using var writer = new StreamWriter(client, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

                var msg = new { Command = command, Payload = payload };
                var json = JsonSerializer.Serialize(msg);

                await writer.WriteLineAsync(json);
                return await reader.ReadLineAsync();
            }
            catch
            {
                return null; // Named pipe unavailable or service stopped
            }
        }
    }
}
