namespace AhuErp.Core.Models
{
    /// <summary>
    /// Phase 20 / Improvement #13 — источник финансирования закупки.
    /// </summary>
    public enum FundingSource
    {
        /// <summary>Федеральный бюджет.</summary>
        FederalBudget = 0,

        /// <summary>Бюджет субъекта Российской Федерации.</summary>
        RegionalBudget = 1,

        /// <summary>Местный (муниципальный) бюджет.</summary>
        MunicipalBudget = 2,

        /// <summary>Внебюджетные источники (приносящая доход деятельность).</summary>
        OffBudget = 3,

        /// <summary>Смешанное финансирование из нескольких источников.</summary>
        Mixed = 4,
    }
}
