using System;
using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Сервис управления закупками по 44-ФЗ (Phase 20 / Improvement #13):
    /// планы-графики, закупочные процедуры, контракты, этапы исполнения,
    /// напоминания о приближающихся датах. Все стейт-машины
    /// (план / процедура / контракт / этап) живут в этом сервисе и пишут
    /// аудит-журнал; уведомления создаются идемпотентно (1 раз в день).
    /// </summary>
    public interface IProcurementService
    {
        // ---- План-график (ст. 16 № 44-ФЗ) ----

        ProcurementPlan CreatePlan(
            string planNumber,
            int year,
            int draftedByEmployeeId,
            DateTime createdAt,
            string notes = null);

        ProcurementPlanItem AddPlanItem(int planId, ProcurementPlanItem item);

        ProcurementPlan ApprovePlan(int planId, int approvedByEmployeeId, DateTime approvedAt);

        /// <summary>
        /// Переводит план в <see cref="ProcurementPlanStatus.Published"/>: создаёт
        /// разовое уведомление <see cref="NotificationKind.ProcurementPlanPublished"/>
        /// для участников закупочной комиссии (передаются явным списком).
        /// </summary>
        ProcurementPlan PublishPlan(
            int planId,
            DateTime publishedAt,
            IEnumerable<int> notifyEmployeeIds = null);

        ProcurementPlan ClosePlan(int planId, DateTime closedAt);

        // ---- Закупочная процедура ----

        ProcurementProcedure CreateProcedure(
            string noticeNumber,
            int procurementPlanItemId,
            int responsibleEmployeeId,
            ProcurementMethod method,
            decimal maxPrice);

        ProcurementProcedure ChangeProcedureStatus(
            int procedureId,
            ProcurementProcedureStatus targetStatus,
            DateTime at,
            string winner = null,
            string winnerInn = null,
            decimal? awardedPrice = null,
            int bidsReceived = 0);

        // ---- Контракт ----

        Contract CreateContract(
            Contract draft,
            int authorEmployeeId);

        Contract SignContract(
            int contractId,
            DateTime signedAt,
            string contractNumber,
            string registryNumber,
            int actorEmployeeId);

        Contract StartExecution(int contractId, DateTime executionStart);

        Contract CompleteContract(int contractId, DateTime completedAt);

        Contract TerminateContract(int contractId, DateTime terminatedAt, string reason);

        ContractMilestone AddMilestone(int contractId, ContractMilestone milestone);

        ContractMilestone AcceptMilestone(
            int milestoneId,
            DateTime acceptedAt,
            string acceptanceActNumber,
            decimal? actualAmount = null);

        ContractMilestone RejectMilestone(int milestoneId, string reason);

        // ---- Сканирование уведомлений ----

        /// <summary>
        /// Идемпотентный обход контрактов / этапов. Создаёт
        /// <see cref="NotificationKind.ContractMilestoneApproaching"/> за
        /// <paramref name="milestoneDaysAhead"/> дней до планового срока этапа
        /// и <see cref="NotificationKind.ContractExecutionEndApproaching"/>
        /// за <paramref name="contractDaysAhead"/> дней до планового
        /// окончания исполнения контракта. Повторный вызов в рамках того
        /// же логического дня записей не дублирует.
        /// </summary>
        /// <param name="now">Логическая дата отсчёта.</param>
        /// <param name="milestoneDaysAhead">Окно для этапов, по умолчанию 7 дней.</param>
        /// <param name="contractDaysAhead">Окно для контракта, по умолчанию 14 дней.</param>
        void ScanUpcomingDeadlines(
            DateTime now,
            int milestoneDaysAhead = 7,
            int contractDaysAhead = 14);
    }
}
