namespace AhuErp.Core.Models
{
    /// <summary>
    /// Phase 20 / Improvement #13 — статус годового плана-графика закупок.
    /// </summary>
    public enum ProcurementPlanStatus
    {
        /// <summary>Черновик плана (готовится контрактной службой).</summary>
        Draft = 0,

        /// <summary>План утверждён руководителем учреждения.</summary>
        Approved = 1,

        /// <summary>План опубликован в ЕИС (zakupki.gov.ru).</summary>
        Published = 2,

        /// <summary>План закрыт (год завершён или отменён).</summary>
        Closed = 3,
    }
}
