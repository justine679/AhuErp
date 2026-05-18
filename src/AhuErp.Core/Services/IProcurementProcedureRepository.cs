using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Репозиторий закупочных процедур по 44-ФЗ (Phase 20 / Improvement #13).
    /// </summary>
    public interface IProcurementProcedureRepository
    {
        ProcurementProcedure Add(ProcurementProcedure procedure);

        ProcurementProcedure Get(int id);

        ProcurementProcedure GetByNoticeNumber(string noticeNumber);

        IReadOnlyList<ProcurementProcedure> List();

        IReadOnlyList<ProcurementProcedure> ListByStatus(ProcurementProcedureStatus status);

        IReadOnlyList<ProcurementProcedure> ListByPlanItem(int planItemId);

        ProcurementProcedure Update(ProcurementProcedure procedure);
    }
}
