using System.Threading.Tasks;

namespace MPCustom.ErrorPage
{
    public interface IErrorWebServer
    {
        bool IsRunning { get; }
        Task StartAsync(int port = 4030);
        Task StopAsync();
    }
}
