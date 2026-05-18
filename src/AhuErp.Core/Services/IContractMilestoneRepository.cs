using System;
using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Репозиторий этапов исполнения контрактов (Phase 20 / Improvement #13).
    /// </summary>
    public interface IContractMilestoneRepository
    {
        ContractMilestone Add(ContractMilestone milestone);

        ContractMilestone Get(int id);

        IReadOnlyList<ContractMilestone> ListByContract(int contractId);

        IReadOnlyList<ContractMilestone> ListUpcoming(DateTime asOf, int daysAhead);

        ContractMilestone Update(ContractMilestone milestone);

        void Remove(int milestoneId);
    }
}
