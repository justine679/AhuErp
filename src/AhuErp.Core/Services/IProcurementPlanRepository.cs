using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Репозиторий планов-графиков закупок по 44-ФЗ (Phase 20 / Improvement #13).
    /// Все операции загружают позиции плана (<see cref="ProcurementPlanItem"/>)
    /// вместе с заголовком, чтобы DOCX-печать и журналы не делали лишних
    /// круговых запросов.
    /// </summary>
    public interface IProcurementPlanRepository
    {
        ProcurementPlan Add(ProcurementPlan plan);

        ProcurementPlan Get(int id);

        ProcurementPlan GetByPlanNumber(string planNumber);

        IReadOnlyList<ProcurementPlan> List();

        IReadOnlyList<ProcurementPlan> ListByStatus(ProcurementPlanStatus status);

        IReadOnlyList<ProcurementPlan> ListByYear(int year);

        ProcurementPlan Update(ProcurementPlan plan);

        ProcurementPlanItem AddItem(ProcurementPlanItem item);

        void RemoveItem(int itemId);
    }
}
