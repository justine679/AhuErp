using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// EF6-реализация <see cref="IContractMilestoneRepository"/>
    /// (Phase 20 / Improvement #13).
    /// </summary>
    public sealed class EfContractMilestoneRepository : IContractMilestoneRepository
    {
        private readonly AhuDbContext _ctx;

        public EfContractMilestoneRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public ContractMilestone Add(ContractMilestone milestone)
        {
            if (milestone == null) throw new ArgumentNullException(nameof(milestone));
            if (!_ctx.Contracts.Any(c => c.Id == milestone.ContractId))
                throw new InvalidOperationException("Родительский контракт не найден.");

            _ctx.ContractMilestones.Add(milestone);
            _ctx.SaveChanges();
            return milestone;
        }

        public ContractMilestone Get(int id)
            => _ctx.ContractMilestones.Find(id);

        public IReadOnlyList<ContractMilestone> ListByContract(int contractId)
            => _ctx.ContractMilestones
                .Where(m => m.ContractId == contractId)
                .OrderBy(m => m.SequenceNumber)
                .ThenBy(m => m.Id)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<ContractMilestone> ListUpcoming(DateTime asOf, int daysAhead)
        {
            if (daysAhead < 0) throw new ArgumentOutOfRangeException(nameof(daysAhead));
            var threshold = asOf.Date.AddDays(daysAhead);
            return _ctx.ContractMilestones
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
            _ctx.Entry(milestone).State = EntityState.Modified;
            _ctx.SaveChanges();
            return milestone;
        }

        public void Remove(int milestoneId)
        {
            var existing = _ctx.ContractMilestones.Find(milestoneId);
            if (existing == null) return;
            _ctx.ContractMilestones.Remove(existing);
            _ctx.SaveChanges();
        }
    }
}
