namespace AhuErp.Core.Models
{
    /// <summary>
    /// Phase 20 / Improvement #13 — статус процедуры закупки.
    /// Жизненный цикл: <c>Planned → Announced → BidsCollection →
    /// BidsEvaluation → Awarded → ContractSigned</c>; терминальные
    /// <c>Cancelled / Failed</c> могут быть достигнуты из любого
    /// нетерминального статуса.
    /// </summary>
    public enum ProcurementProcedureStatus
    {
        /// <summary>Запланирована, ещё не объявлена в ЕИС.</summary>
        Planned = 0,

        /// <summary>Объявлена в ЕИС, ждём начала приёма заявок.</summary>
        Announced = 1,

        /// <summary>Идёт сбор заявок участников.</summary>
        BidsCollection = 2,

        /// <summary>Заявки рассматриваются и оцениваются.</summary>
        BidsEvaluation = 3,

        /// <summary>Определён победитель, заключение контракта в процессе.</summary>
        Awarded = 4,

        /// <summary>Контракт по процедуре подписан.</summary>
        ContractSigned = 5,

        /// <summary>Процедура отменена заказчиком (ст. 36 № 44-ФЗ).</summary>
        Cancelled = 6,

        /// <summary>Процедура признана несостоявшейся.</summary>
        Failed = 7,
    }
}
