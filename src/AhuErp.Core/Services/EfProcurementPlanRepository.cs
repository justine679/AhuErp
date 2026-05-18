using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// EF6-реализация <see cref="IProcurementPlanRepository"/>
    /// (Phase 20 / Improvement #13).
    /// </summary>
    public sealed class EfProcurementPlanRepository : IProcurementPlanRepository
    {
        private readonly AhuDbContext _ctx;

        public EfProcurementPlanRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public ProcurementPlan Add(ProcurementPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (string.IsNullOrWhiteSpace(plan.PlanNumber))
                throw new ArgumentException("Номер плана обязателен.", nameof(plan));

            if (_ctx.ProcurementPlans.Any(p => p.PlanNumber == plan.PlanNumber))
                throw new InvalidOperationException(
                    $"План с номером «{plan.PlanNumber}» уже зарегистрирован.");

            _ctx.ProcurementPlans.Add(plan);
            _ctx.SaveChanges();
            return plan;
        }

        public ProcurementPlan Get(int id)
        {
            return _ctx.ProcurementPlans
                .Include(p => p.Items)
                .FirstOrDefault(p => p.Id == id);
        }

        public ProcurementPlan GetByPlanNumber(string planNumber)
        {
            if (string.IsNullOrWhiteSpace(planNumber)) return null;
            return _ctx.ProcurementPlans
                .Include(p => p.Items)
                .FirstOrDefault(p => p.PlanNumber == planNumber);
        }

        public IReadOnlyList<ProcurementPlan> List()
            => _ctx.ProcurementPlans
                .Include(p => p.Items)
                .OrderByDescending(p => p.Year)
                .ThenByDescending(p => p.Id)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<ProcurementPlan> ListByStatus(ProcurementPlanStatus status)
            => _ctx.ProcurementPlans
                .Include(p => p.Items)
                .Where(p => p.Status == status)
                .OrderByDescending(p => p.Year)
                .ThenByDescending(p => p.Id)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<ProcurementPlan> ListByYear(int year)
            => _ctx.ProcurementPlans
                .Include(p => p.Items)
                .Where(p => p.Year == year)
                .OrderByDescending(p => p.Id)
                .ToList()
                .AsReadOnly();

        public ProcurementPlan Update(ProcurementPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            _ctx.Entry(plan).State = EntityState.Modified;
            _ctx.SaveChanges();
            return plan;
        }

        public ProcurementPlanItem AddItem(ProcurementPlanItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (!_ctx.ProcurementPlans.Any(p => p.Id == item.ProcurementPlanId))
                throw new InvalidOperationException("Родительский план не найден.");

            _ctx.ProcurementPlanItems.Add(item);
            _ctx.SaveChanges();
            return item;
        }

        public void RemoveItem(int itemId)
        {
            var existing = _ctx.ProcurementPlanItems.Find(itemId);
            if (existing == null) return;
            _ctx.ProcurementPlanItems.Remove(existing);
            _ctx.SaveChanges();
        }
    }
}
