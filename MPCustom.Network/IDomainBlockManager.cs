using System.Collections.Generic;

namespace MPCustom.Network
{
    public interface IDomainBlockManager
    {
        bool ApplyDomainBlocks(IEnumerable<string> domains);
        bool RemoveDomainBlocks();
        bool VerifyDomainBlocks(IEnumerable<string> domains);
        bool RepairDomainBlocks(IEnumerable<string> domains);
    }
}
