using System.Collections.Generic;

namespace MPCustom.Network
{
    public interface IFirewallRuleManager
    {
        bool EnsureRulesExist(IEnumerable<string> executablePaths);
        bool RemoveAllRules();
        bool VerifyRulesExist(IEnumerable<string> executablePaths);
        bool RepairRules(IEnumerable<string> executablePaths);
    }
}
