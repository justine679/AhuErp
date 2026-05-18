using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// EF6-реализация <see cref="IContractRepository"/> поверх TPH-таблицы Documents
    /// (Phase 20 / Improvement #13).
    /// </summary>
    public sealed class EfContractRepository : IContractRepository
    {
        private readonly AhuDbContext _ctx;

        public EfContractRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public Contract Add(Contract contract)
        {
            if (contract == null) throw new ArgumentNullException(nameof(contract));

            if (!string.IsNullOrWhiteSpace(contract.ContractNumber)
                && _ctx.Contracts.Any(c => c.ContractNumber == contract.ContractNumber))
            {
                throw new InvalidOperationException(
                    $"Контракт с номером «{contract.ContractNumber}» уже зарегистрирован.");
            }

            _ctx.Contracts.Add(contract);
            _ctx.SaveChanges();
            return contract;
        }

        public Contract Get(int id)
        {
            return _ctx.Contracts
                .Include(c => c.Milestones)
                .FirstOrDefault(c => c.Id == id);
        }

        public Contract GetByContractNumber(string contractNumber)
        {
            if (string.IsNullOrWhiteSpace(contractNumber)) return null;
            return _ctx.Contracts
                .Include(c => c.Milestones)
                .FirstOrDefault(c => c.ContractNumber == contractNumber);
        }

        public IReadOnlyList<Contract> List()
            => _ctx.Contracts
                .Include(c => c.Milestones)
                .OrderByDescending(c => c.Id)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<Contract> ListByStatus(ContractStatus status)
            => _ctx.Contracts
                .Include(c => c.Milestones)
                .Where(c => c.ContractStatus == status)
                .OrderByDescending(c => c.Id)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<Contract> ListByProcedure(int procedureId)
            => _ctx.Contracts
                .Include(c => c.Milestones)
                .Where(c => c.ProcurementProcedureId == procedureId)
                .OrderByDescending(c => c.Id)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<Contract> ListByExecutionEndBefore(DateTime threshold)
            => _ctx.Contracts
                .Include(c => c.Milestones)
                .Where(c => c.ExecutionEndDate.HasValue && c.ExecutionEndDate.Value <= threshold)
                .OrderBy(c => c.ExecutionEndDate)
                .ToList()
                .AsReadOnly();

        public Contract Update(Contract contract)
        {
            if (contract == null) throw new ArgumentNullException(nameof(contract));
            _ctx.Entry(contract).State = EntityState.Modified;
            _ctx.SaveChanges();
            return contract;
        }
    }
}
