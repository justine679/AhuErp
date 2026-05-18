using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Строка плана-графика закупок (Phase 20 / Improvement #13) — одна позиция
    /// плана соответствует одной закупке: предмет, объём, источник финансирования,
    /// способ определения поставщика, плановый квартал размещения извещения.
    /// </summary>
    public class ProcurementPlanItem
    {
        public int Id { get; set; }

        /// <summary>FK на родительский план. Каскадное удаление вместе с планом.</summary>
        public int ProcurementPlanId { get; set; }
        public virtual ProcurementPlan ProcurementPlan { get; set; }

        /// <summary>Порядковый номер позиции в плане (печатается в DOCX).</summary>
        public int LineNumber { get; set; }

        /// <summary>Идентификационный код закупки (ИКЗ).</summary>
        [StringLength(36)]
        public string PurchaseCode { get; set; }

        /// <summary>Предмет закупки.</summary>
        [Required]
        [StringLength(1024)]
        public string Subject { get; set; }

        /// <summary>Код ОКПД2 / ТРУ.</summary>
        [StringLength(32)]
        public string OkpdCode { get; set; }

        /// <summary>
        /// Начальная (максимальная) цена контракта в рублях. Точность 18,2 —
        /// соответствует требованиям ЕИС.
        /// </summary>
        public decimal MaxPrice { get; set; }

        /// <summary>Источник финансирования.</summary>
        public FundingSource FundingSource { get; set; } = FundingSource.MunicipalBudget;

        /// <summary>Способ определения поставщика.</summary>
        public ProcurementMethod Method { get; set; } = ProcurementMethod.ElectronicAuction;

        /// <summary>Плановый квартал размещения извещения (1..4).</summary>
        public int PlannedQuarter { get; set; }

        /// <summary>Обоснование выбора способа закупки (для единственного поставщика — ссылка на п. ст. 93).</summary>
        [StringLength(2048)]
        public string Justification { get; set; }
    }
}
