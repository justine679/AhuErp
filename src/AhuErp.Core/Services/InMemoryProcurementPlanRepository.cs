using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// In-memory реализация <see cref="IProcurementPlanRepository"/> для тестов
    /// (Phase 20 / Improvement #13). Идентификаторы выдаются автоинкрементом,
    /// уникальность <see cref="ProcurementPlan.PlanNumber"/> проверяется на уровне
    /// репозитория, чтобы поведение совпадало с уникальным индексом в EF6.
    /// </summary>
    public sealed class InMemoryProcurementPlanRepository : IProcurementPlanRepository
    {
        private readonly Dictionary<int, ProcurementPlan> _plans = new Dictionary<int, ProcurementPlan>();
        private readonly Dictionary<int, ProcurementPlanItem> _items = new Dictionary<int, ProcurementPlanItem>();
        private int _nextPlanId = 1;
        private int _nextItemId = 1;

        public ProcurementPlan Add(ProcurementPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (string.IsNullOrWhiteSpace(plan.PlanNumber))
                throw new ArgumentException("Номер плана обязателен.", nameof(plan));
            if (_plans.Values.Any(p => p.PlanNumber == plan.PlanNumber))
                throw new InvalidOperationException(
                    $"План с номером «{plan.PlanNumber}» уже зарегистрирован.");

            plan.Id = _nextPlanId++;
            _plans[plan.Id] = plan;

            if (plan.Items != null)
            {
                foreach (var item in plan.Items.ToList())
                {
                    item.ProcurementPlanId = plan.Id;
                    AddItem(item);
                }
            }
            return plan;
        }

        public ProcurementPlan Get(int id)
        {
            if (!_plans.TryGetValue(id, out var plan)) return null;
            HydrateItems(plan);
            return plan;
        }

        public ProcurementPlan GetByPlanNumber(string planNumber)
        {
            if (string.IsNullOrWhiteSpace(planNumber)) return null;
            var plan = _plans.Values.FirstOrDefault(p => p.PlanNumber == planNumber);
            if (plan != null) HydrateItems(plan);
            return plan;
        }

        public IReadOnlyList<ProcurementPlan> List()
        {
            return _plans.Values
                .OrderByDescending(p => p.Year)
                .ThenByDescending(p => p.Id)
                .Select(p => { HydrateItems(p); return p; })
                .ToList()
                .AsReadOnly();
        }

        public IReadOnlyList<ProcurementPlan> ListByStatus(ProcurementPlanStatus status)
        {
            return _plans.Values
                .Where(p => p.Status == status)
                .OrderByDescending(p => p.Year)
                .ThenByDescending(p => p.Id)
                .Select(p => { HydrateItems(p); return p; })
                .ToList()
                .AsReadOnly();
        }

        public IReadOnlyList<ProcurementPlan> ListByYear(int year)
        {
            return _plans.Values
                .Where(p => p.Year == year)
                .OrderByDescending(p => p.Id)
                .Select(p => { HydrateItems(p); return p; })
                .ToList()
                .AsReadOnly();
        }

        public ProcurementPlan Update(ProcurementPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (!_plans.ContainsKey(plan.Id))
                throw new InvalidOperationException("План не найден.");
            _plans[plan.Id] = plan;
            return plan;
        }

        public ProcurementPlanItem AddItem(ProcurementPlanItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (!_plans.ContainsKey(item.ProcurementPlanId))
                throw new InvalidOperationException("Родительский план не найден.");

            item.Id = _nextItemId++;
            _items[item.Id] = item;
            return item;
        }

        public void RemoveItem(int itemId)
        {
            _items.Remove(itemId);
        }

        private void HydrateItems(ProcurementPlan plan)
        {
            var items = _items.Values
                .Where(i => i.ProcurementPlanId == plan.Id)
                .OrderBy(i => i.LineNumber)
                .ThenBy(i => i.Id)
                .ToList();
            plan.Items = new HashSet<ProcurementPlanItem>(items);
        }
    }
}
