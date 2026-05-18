namespace AhuErp.Core.Models
{
    /// <summary>
    /// Phase 20 / Improvement #13 — статус этапа исполнения контракта.
    /// Жизненный цикл: <c>Planned → InProgress → (Accepted | Rejected)</c>.
    /// Из <c>Rejected</c> можно вернуть этап в <c>InProgress</c> для доработки;
    /// <c>Accepted</c> — терминал.
    /// </summary>
    public enum ContractMilestoneStatus
    {
        /// <summary>Этап запланирован, исполнение не начато.</summary>
        Planned = 0,

        /// <summary>Этап исполняется поставщиком.</summary>
        InProgress = 1,

        /// <summary>Этап принят заказчиком (подписан акт).</summary>
        Accepted = 2,

        /// <summary>Этап отклонён, требуется доработка / акт устранения.</summary>
        Rejected = 3,
    }
}
