using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Репозиторий контрактов по 44-ФЗ (Phase 20 / Improvement #13).
    /// Хранятся как TPH-подкласс <see cref="Document"/> с дискриминатором «Contract».
    /// </summary>
    public interface IContractRepository
    {
        Contract Add(Contract contract);

        Contract Get(int id);

        Contract GetByContractNumber(string contractNumber);

        IReadOnlyList<Contract> List();

        IReadOnlyList<Contract> ListByStatus(ContractStatus status);

        IReadOnlyList<Contract> ListByProcedure(int procedureId);

        IReadOnlyList<Contract> ListByExecutionEndBefore(System.DateTime threshold);

        Contract Update(Contract contract);
    }
}
