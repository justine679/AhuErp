using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// In-memory реализация <see cref="IContractMilestoneRepository"/>
    /// (Phase 20 / Improvement #13).
    /// </summary>
    public sealed class InMemoryContractMilestoneRepository : IContractMilestoneRepository
    {
        private readonly Dictionary<int, ContractMilestone> _milestones
            = new Dictionary<int, ContractMilestone>();
        private int _nextId = 1;

        public ContractMilestone Add(ContractMilestone milestone)
        {
            if (milestone == null) throw new ArgumentNullException(nameof(milestone));
            milestone.Id = _nextId++;
            _milestones[milestone.Id] = milestone;
            return milestone;
        }

        public ContractMilestone Get(int id)
            => _milestones.TryGetValue(id, out var m) ? m : null;

        public IReadOnlyList<ContractMilestone> ListByContract(int contractId)
            => _milestones.Values
                .Where(m => m.ContractId == contractId)
                .OrderBy(m => m.SequenceNumber)
                .ThenBy(m => m.Id)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<ContractMilestone> ListUpcoming(DateTime asOf, int daysAhead)
        {
            if (daysAhead < 0) throw new ArgumentOutOfRangeException(nameof(daysAhead));
            var threshold = asOf.Date.AddDays(daysAhead);
            return _milestones.Values
                .Where(m => m.Status != ContractMilestoneStatus.Accepted
                            && m.PlannedDate >= asOf.Date
                            && m.PlannedDate <= threshold)
                .OrderBy(m => m.PlannedDate)
                .ToList()
                .AsReadOnly();
        }

        public ContractMilestone Update(ContractMilestone milestone)
        {
            if (milestone == null) throw new ArgumentNullException(nameof(milestone));
            if (!_milestones.ContainsKey(milestone.Id))
                throw new InvalidOperationException("Этап не найден.");
            _milestones[milestone.Id] = milestone;
            return milestone;
        }

        public void Remove(int milestoneId)
        {
            _milestones.Remove(milestoneId);
        }
    }
}
