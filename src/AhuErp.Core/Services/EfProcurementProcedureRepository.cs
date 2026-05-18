using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// EF6-реализация <see cref="IProcurementProcedureRepository"/>
    /// (Phase 20 / Improvement #13).
    /// </summary>
    public sealed class EfProcurementProcedureRepository : IProcurementProcedureRepository
    {
        private readonly AhuDbContext _ctx;

        public EfProcurementProcedureRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public ProcurementProcedure Add(ProcurementProcedure procedure)
        {
            if (procedure == null) throw new ArgumentNullException(nameof(procedure));
            if (string.IsNullOrWhiteSpace(procedure.NoticeNumber))
                throw new ArgumentException("Реестровый номер извещения обязателен.", nameof(procedure));

            if (_ctx.ProcurementProcedures.Any(p => p.NoticeNumber == procedure.NoticeNumber))
                throw new InvalidOperationException(
                    $"Процедура с номером извещения «{procedure.NoticeNumber}» уже зарегистрирована.");

            _ctx.ProcurementProcedures.Add(procedure);
            _ctx.SaveChanges();
            return procedure;
        }

        public ProcurementProcedure Get(int id)
        {
            return _ctx.ProcurementProcedures
                .Include(p => p.ProcurementPlanItem)
                .FirstOrDefault(p => p.Id == id);
        }

        public ProcurementProcedure GetByNoticeNumber(string noticeNumber)
        {
            if (string.IsNullOrWhiteSpace(noticeNumber)) return null;
            return _ctx.ProcurementProcedures
                .Include(p => p.ProcurementPlanItem)
                .FirstOrDefault(p => p.NoticeNumber == noticeNumber);
        }

        public IReadOnlyList<ProcurementProcedure> List()
            => _ctx.ProcurementProcedures
                .OrderByDescending(p => p.Id)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<ProcurementProcedure> ListByStatus(ProcurementProcedureStatus status)
            => _ctx.ProcurementProcedures
                .Where(p => p.Status == status)
                .OrderByDescending(p => p.Id)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<ProcurementProcedure> ListByPlanItem(int planItemId)
            => _ctx.ProcurementProcedures
                .Where(p => p.ProcurementPlanItemId == planItemId)
                .OrderByDescending(p => p.Id)
                .ToList()
                .AsReadOnly();

        public ProcurementProcedure Update(ProcurementProcedure procedure)
        {
            if (procedure == null) throw new ArgumentNullException(nameof(procedure));
            _ctx.Entry(procedure).State = EntityState.Modified;
            _ctx.SaveChanges();
            return procedure;
        }
    }
}
