using System.Collections.Generic;

namespace MPCustom.Service.Engine
{
    public interface IProcessProtectionService
    {
        List<string> DiscoverRobloxExecutables();
        int CheckAndTerminateRobloxProcesses(bool isProtectionActive);
    }
}
