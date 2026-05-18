using System;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Закупочная процедура по 44-ФЗ (Phase 20 / Improvement #13) — конкретное
    /// размещение извещения в ЕИС с привязкой к позиции плана-графика.
    /// </summary>
    /// <remarks>
    /// Процедура намеренно не наследует <see cref="Document"/>: это запись
    /// в реестре закупок, не РКК-документ. Жизненный цикл — собственный
    /// (<see cref="ProcurementProcedureStatus"/>).
    /// </remarks>
    public class ProcurementProcedure
    {
        public int Id { get; set; }

        /// <summary>Реестровый номер извещения (например, «0173200001426000123»).</summary>
        [Required]
        [StringLength(64)]
        public string NoticeNumber { get; set; }

        /// <summary>FK на позицию плана-графика, в рамках которой проводится закупка.</summary>
        public int ProcurementPlanItemId { get; set; }
        public virtual ProcurementPlanItem ProcurementPlanItem { get; set; }

        /// <summary>Способ определения поставщика.</summary>
        public ProcurementMethod Method { get; set; } = ProcurementMethod.ElectronicAuction;

        /// <summary>Состояние процедуры.</summary>
        public ProcurementProcedureStatus Status { get; set; } = ProcurementProcedureStatus.Planned;

        /// <summary>
        /// Начальная (максимальная) цена контракта в рублях. На старте
        /// копируется из <see cref="ProcurementPlanItem.MaxPrice"/>.
        /// </summary>
        public decimal MaxPrice { get; set; }

        /// <summary>Дата размещения извещения в ЕИС.</summary>
        public DateTime? NoticePublishedAt { get; set; }

        /// <summary>Дата окончания приёма заявок (плановая).</summary>
        public DateTime? BidsDeadline { get; set; }

        /// <summary>Дата рассмотрения заявок (плановая).</summary>
        public DateTime? EvaluationDate { get; set; }

        /// <summary>Дата подведения итогов / определения победителя.</summary>
        public DateTime? AwardedAt { get; set; }

        /// <summary>Сотрудник, ответственный за процедуру (контрактный управляющий).</summary>
        public int ResponsibleEmployeeId { get; set; }
        public virtual Employee ResponsibleEmployee { get; set; }

        /// <summary>Количество поданных заявок (для статистики и определения «несостоявшихся»).</summary>
        public int BidsReceived { get; set; }

        /// <summary>Победитель процедуры (поставщик / подрядчик).</summary>
        [StringLength(512)]
        public string Winner { get; set; }

        /// <summary>ИНН победителя.</summary>
        [StringLength(12)]
        public string WinnerInn { get; set; }

        /// <summary>Цена контракта по итогам процедуры (с учётом снижения).</summary>
        public decimal? AwardedPrice { get; set; }

        /// <summary>Заметки (ссылки на протоколы, причины отмены и т.п.).</summary>
        [StringLength(4096)]
        public string Notes { get; set; }
    }
}
