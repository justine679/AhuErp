using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// In-memory реализация <see cref="IProcurementProcedureRepository"/>
    /// (Phase 20 / Improvement #13).
    /// </summary>
    public sealed class InMemoryProcurementProcedureRepository : IProcurementProcedureRepository
    {
        private readonly Dictionary<int, ProcurementProcedure> _procedures
            = new Dictionary<int, ProcurementProcedure>();
        private int _nextId = 1;

        public ProcurementProcedure Add(ProcurementProcedure procedure)
        {
            if (procedure == null) throw new ArgumentNullException(nameof(procedure));
            if (string.IsNullOrWhiteSpace(procedure.NoticeNumber))
                throw new ArgumentException("Реестровый номер извещения обязателен.", nameof(procedure));
            if (_procedures.Values.Any(p => p.NoticeNumber == procedure.NoticeNumber))
                throw new InvalidOperationException(
                    $"Процедура с номером извещения «{procedure.NoticeNumber}» уже зарегистрирована.");

            procedure.Id = _nextId++;
            _procedures[procedure.Id] = procedure;
            return procedure;
        }

        public ProcurementProcedure Get(int id)
            => _procedures.TryGetValue(id, out var p) ? p : null;

        public ProcurementProcedure GetByNoticeNumber(string noticeNumber)
        {
            if (string.IsNullOrWhiteSpace(noticeNumber)) return null;
            return _procedures.Values.FirstOrDefault(p => p.NoticeNumber == noticeNumber);
        }

        public IReadOnlyList<ProcurementProcedure> List()
            => _procedures.Values
                .OrderByDescending(p => p.Id)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<ProcurementProcedure> ListByStatus(ProcurementProcedureStatus status)
            => _procedures.Values
                .Where(p => p.Status == status)
                .OrderByDescending(p => p.Id)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<ProcurementProcedure> ListByPlanItem(int planItemId)
            => _procedures.Values
                .Where(p => p.ProcurementPlanItemId == planItemId)
                .OrderByDescending(p => p.Id)
                .ToList()
                .AsReadOnly();

        public ProcurementProcedure Update(ProcurementProcedure procedure)
        {
            if (procedure == null) throw new ArgumentNullException(nameof(procedure));
            if (!_procedures.ContainsKey(procedure.Id))
                throw new InvalidOperationException("Процедура не найдена.");
            _procedures[procedure.Id] = procedure;
            return procedure;
        }
    }
}
