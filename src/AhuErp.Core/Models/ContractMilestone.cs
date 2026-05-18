using System;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Этап исполнения контракта (Phase 20 / Improvement #13) — отдельная
    /// поставка / акт выполненных работ с собственным сроком и суммой.
    /// </summary>
    public class ContractMilestone
    {
        public int Id { get; set; }

        /// <summary>FK на родительский контракт. Каскадное удаление вместе с контрактом.</summary>
        public int ContractId { get; set; }
        public virtual Contract Contract { get; set; }

        /// <summary>Порядковый номер этапа.</summary>
        public int SequenceNumber { get; set; }

        /// <summary>Краткое описание этапа.</summary>
        [Required]
        [StringLength(512)]
        public string Title { get; set; }

        /// <summary>Плановая дата сдачи этапа.</summary>
        public DateTime PlannedDate { get; set; }

        /// <summary>Фактическая дата сдачи (после подписания акта).</summary>
        public DateTime? AcceptedAt { get; set; }

        /// <summary>Сумма этапа в рублях.</summary>
        public decimal Amount { get; set; }

        /// <summary>Состояние этапа.</summary>
        public ContractMilestoneStatus Status { get; set; } = ContractMilestoneStatus.Planned;

        /// <summary>Номер акта приёмки (если этап принят).</summary>
        [StringLength(64)]
        public string AcceptanceActNumber { get; set; }

        /// <summary>Заметки (причины отклонения, замечания).</summary>
        [StringLength(2048)]
        public string Notes { get; set; }
    }
}
