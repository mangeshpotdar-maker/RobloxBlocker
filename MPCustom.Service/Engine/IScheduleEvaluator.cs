using MPCustom.Core.Models;

namespace MPCustom.Service.Engine
{
    public interface IScheduleEvaluator
    {
        bool IsProtectionShouldBeActive(ProtectionConfig config);
    }
}
