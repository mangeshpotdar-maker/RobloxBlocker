using MPCustom.Core.Models;

namespace MPCustom.Core.Config
{
    public interface IConfigRepository
    {
        ProtectionConfig GetConfig();
        void SaveConfig(ProtectionConfig config);
        void UpdateConfig(Action<ProtectionConfig> updateAction);
        string ConfigFilePath { get; }
    }
}
