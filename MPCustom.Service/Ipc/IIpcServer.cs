using System.Threading;
using System.Threading.Tasks;

namespace MPCustom.Service.Ipc
{
    public interface IIpcServer
    {
        Task StartAsync(CancellationToken cancellationToken);
        void Stop();
    }
}
