using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// План-график закупок по 44-ФЗ (Phase 20 / Improvement #13). Соответствует
    /// требованиям ст. 16 Федерального закона № 44-ФЗ и Постановлению
    /// Правительства РФ от 30.09.2019 № 1279.
    /// </summary>
    /// <remarks>
    /// План закупок намеренно не наследует <see cref="Document"/>: это внутренний
    /// планово-экономический документ, не проходящий обычный РКК-маршрут.
    /// Жизненный цикл — собственный (<see cref="ProcurementPlanStatus"/>).
    /// Состав плана хранится в <see cref="ProcurementPlanItem"/>.
    /// </remarks>
    public class ProcurementPlan
    {
        public int Id { get; set; }

        /// <summary>Регистрационный номер плана (например, «ПЗ-2026-001»).</summary>
        [Required]
        [StringLength(64)]
        public string PlanNumber { get; set; }

        /// <summary>Год, на который составлен план.</summary>
        public int Year { get; set; }

        /// <summary>Дата формирования проекта плана.</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>Состояние плана (черновик → утверждён → опубликован → закрыт).</summary>
        public ProcurementPlanStatus Status { get; set; } = ProcurementPlanStatus.Draft;

        /// <summary>Сотрудник, составивший проект плана.</summary>
        public int DraftedByEmployeeId { get; set; }
        public virtual Employee DraftedByEmployee { get; set; }

        /// <summary>Сотрудник, утвердивший план (руководитель / зам по АХЧ).</summary>
        public int? ApprovedByEmployeeId { get; set; }
        public virtual Employee ApprovedByEmployee { get; set; }

        /// <summary>Дата утверждения (фиксируется в момент перевода в <see cref="ProcurementPlanStatus.Approved"/>).</summary>
        public DateTime? ApprovedAt { get; set; }

        /// <summary>Дата публикации в ЕИС (фиксируется в <see cref="ProcurementPlanStatus.Published"/>).</summary>
        public DateTime? PublishedAt { get; set; }

        /// <summary>Дата закрытия плана (по окончании года или досрочно).</summary>
        public DateTime? ClosedAt { get; set; }

        /// <summary>Заметки (обоснования, ссылки на протокол согласования).</summary>
        [StringLength(4096)]
        public string Notes { get; set; }

        public virtual ICollection<ProcurementPlanItem> Items { get; set; }
            = new HashSet<ProcurementPlanItem>();
    }
}
