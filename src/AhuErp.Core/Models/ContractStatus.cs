namespace AhuErp.Core.Models
{
    /// <summary>
    /// Phase 20 / Improvement #13 — статус государственного / муниципального
    /// контракта по 44-ФЗ.
    /// Жизненный цикл: <c>Draft → Signed → InExecution → Completed</c>;
    /// досрочное расторжение по соглашению / решению суда / одностороннее —
    /// <c>Terminated</c>.
    /// </summary>
    public enum ContractStatus
    {
        /// <summary>Черновик контракта (проект, подготовка к подписанию).</summary>
        Draft = 0,

        /// <summary>Контракт подписан обеими сторонами, исполнение не начато.</summary>
        Signed = 1,

        /// <summary>Контракт в исполнении (этапы сдаются и принимаются).</summary>
        InExecution = 2,

        /// <summary>Контракт исполнен полностью.</summary>
        Completed = 3,

        /// <summary>Контракт расторгнут досрочно (ст. 95 № 44-ФЗ).</summary>
        Terminated = 4,
    }
}
