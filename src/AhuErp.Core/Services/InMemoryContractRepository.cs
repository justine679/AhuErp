using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// In-memory реализация <see cref="IContractRepository"/> (Phase 20 / Improvement #13).
    /// </summary>
    public sealed class InMemoryContractRepository : IContractRepository
    {
        private readonly Dictionary<int, Contract> _contracts = new Dictionary<int, Contract>();
        private int _nextId = 1;

        public Contract Add(Contract contract)
        {
            if (contract == null) throw new ArgumentNullException(nameof(contract));
            if (!string.IsNullOrWhiteSpace(contract.ContractNumber)
                && _contracts.Values.Any(c => c.ContractNumber == contract.ContractNumber))
            {
                throw new InvalidOperationException(
                    $"Контракт с номером «{contract.ContractNumber}» уже зарегистрирован.");
            }

            contract.Id = _nextId++;
            _contracts[contract.Id] = contract;
            return contract;
        }

        public Contract Get(int id)
            => _contracts.TryGetValue(id, out var c) ? c : null;

        public Contract GetByContractNumber(string contractNumber)
        {
            if (string.IsNullOrWhiteSpace(contractNumber)) return null;
            return _contracts.Values.FirstOrDefault(c => c.ContractNumber == contractNumber);
        }

        public IReadOnlyList<Contract> List()
            => _contracts.Values
                .OrderByDescending(c => c.Id)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<Contract> ListByStatus(ContractStatus status)
            => _contracts.Values
                .Where(c => c.ContractStatus == status)
                .OrderByDescending(c => c.Id)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<Contract> ListByProcedure(int procedureId)
            => _contracts.Values
                .Where(c => c.ProcurementProcedureId == procedureId)
                .OrderByDescending(c => c.Id)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<Contract> ListByExecutionEndBefore(DateTime threshold)
            => _contracts.Values
                .Where(c => c.ExecutionEndDate.HasValue && c.ExecutionEndDate.Value <= threshold)
                .OrderBy(c => c.ExecutionEndDate)
                .ToList()
                .AsReadOnly();

        public Contract Update(Contract contract)
        {
            if (contract == null) throw new ArgumentNullException(nameof(contract));
            if (!_contracts.ContainsKey(contract.Id))
                throw new InvalidOperationException("Контракт не найден.");
            _contracts[contract.Id] = contract;
            return contract;
        }
    }
}
