namespace AhuErp.Core.Models
{
    /// <summary>
    /// Phase 20 / Improvement #13 — способ определения поставщика по
    /// Федеральному закону от 05.04.2013 № 44-ФЗ (ст. 24).
    /// </summary>
    public enum ProcurementMethod
    {
        /// <summary>Закупка у единственного поставщика (ст. 93).</summary>
        SingleSupplier = 0,

        /// <summary>Электронный аукцион (ст. 49 ред. 360-ФЗ).</summary>
        ElectronicAuction = 1,

        /// <summary>Запрос котировок в электронной форме (ст. 50).</summary>
        RequestForQuotations = 2,

        /// <summary>Открытый конкурс в электронной форме (ст. 48).</summary>
        OpenTender = 3,

        /// <summary>Запрос предложений в электронной форме (ст. 50.1).</summary>
        RequestForProposals = 4,

        /// <summary>Закрытые способы (ст. 73), редко применяются.</summary>
        ClosedProcedure = 5,
    }
}
