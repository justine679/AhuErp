using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Государственный / муниципальный контракт по 44-ФЗ
    /// (Phase 20 / Improvement #13). Наследуется от <see cref="Document"/>
    /// через TPH-дискриминатор: контракт — полноценный документ, проходит
    /// согласование, подписание, регистрацию в журналах РКК и попадает в
    /// архив (см. <see cref="DocumentType.Contract"/>).
    /// </summary>
    /// <remarks>
    /// Жизненный цикл по 44-ФЗ (Draft → Signed → InExecution → Completed,
    /// досрочно — Terminated) хранится в <see cref="ContractStatus"/>;
    /// унаследованный <see cref="Document.Status"/> отражает общий статус
    /// РКК (черновик / в работе / завершён) и обновляется параллельно
    /// сервисом закупок.
    /// </remarks>
    public class Contract : Document
    {
        /// <summary>Реестровый номер контракта в ЕИС (например, «2773601125726000045»).</summary>
        [StringLength(64)]
        public string RegistryNumber { get; set; }

        /// <summary>Внутренний номер контракта (печатается на бланке).</summary>
        [StringLength(64)]
        public string ContractNumber { get; set; }

        /// <summary>Дата заключения контракта (подписания обеими сторонами).</summary>
        public DateTime? SignedAt { get; set; }

        /// <summary>Плановая дата начала исполнения контракта.</summary>
        public DateTime? ExecutionStartDate { get; set; }

        /// <summary>Плановая дата окончания исполнения контракта.</summary>
        public DateTime? ExecutionEndDate { get; set; }

        /// <summary>Цена контракта в рублях.</summary>
        public decimal Price { get; set; }

        /// <summary>Источник финансирования.</summary>
        public FundingSource FundingSource { get; set; } = FundingSource.MunicipalBudget;

        /// <summary>Поставщик / подрядчик.</summary>
        [StringLength(512)]
        public string SupplierName { get; set; }

        /// <summary>ИНН поставщика.</summary>
        [StringLength(12)]
        public string SupplierInn { get; set; }

        /// <summary>КПП поставщика.</summary>
        [StringLength(9)]
        public string SupplierKpp { get; set; }

        /// <summary>FK на закупочную процедуру, по итогам которой заключён контракт (для способа Single Supplier — null).</summary>
        public int? ProcurementProcedureId { get; set; }
        public virtual ProcurementProcedure ProcurementProcedure { get; set; }

        /// <summary>Текущее состояние контракта (44-ФЗ-специфичное).</summary>
        public ContractStatus ContractStatus { get; set; } = ContractStatus.Draft;

        /// <summary>Дата фактического исполнения контракта (фиксируется в <see cref="ContractStatus.Completed"/>).</summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>Дата расторжения (если применимо).</summary>
        public DateTime? TerminatedAt { get; set; }

        /// <summary>Основание для расторжения (ст. 95 ч.X № 44-ФЗ).</summary>
        [StringLength(2048)]
        public string TerminationReason { get; set; }

        /// <summary>Этапы исполнения контракта (приёмочные акты).</summary>
        public virtual ICollection<ContractMilestone> Milestones { get; set; }
            = new HashSet<ContractMilestone>();

        public Contract()
        {
            Type = DocumentType.Contract;
        }
    }
}
