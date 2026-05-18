namespace AhuErp.Core.Models
{
    /// <summary>
    /// Тип документа, используется для фильтрации по подсистемам.
    /// Значения сохраняются как int — добавление новых членов в конец
    /// не требует миграции EF6.
    /// </summary>
    public enum DocumentType
    {
        General = 0,
        Office = 1,
        Archive = 2,
        It = 3,
        Fleet = 4,

        /// <summary>Входящий документ отдела документационного обеспечения.</summary>
        Incoming = 5,

        /// <summary>Внутренний документ отдела документационного обеспечения.</summary>
        Internal = 6,

        /// <summary>Архивный запрос (используется <see cref="ArchiveRequest"/>).</summary>
        ArchiveRequest = 7,

        /// <summary>
        /// Контракт по 44-ФЗ (государственный или муниципальный) — используется
        /// классом <see cref="Contract"/> (Phase 20 / Improvement #13).
        /// </summary>
        Contract = 8
    }
}
