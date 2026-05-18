using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Реализация <see cref="IProcurementService"/> (Phase 20 / Improvement #13).
    /// Управляет стейт-машинами плана-графика, процедуры, контракта и этапа,
    /// ведёт аудит, рассылает напоминания о приближающихся сроках.
    /// </summary>
    public sealed class ProcurementService : IProcurementService
    {
        private static readonly ProcurementPlanStatus[] PlanOrder =
        {
            ProcurementPlanStatus.Draft,
            ProcurementPlanStatus.Approved,
            ProcurementPlanStatus.Published,
            ProcurementPlanStatus.Closed
        };

        private readonly IProcurementPlanRepository _plans;
        private readonly IProcurementProcedureRepository _procedures;
        private readonly IContractRepository _contracts;
        private readonly IContractMilestoneRepository _milestones;
        private readonly IAuditLogRepository _audit;
        private readonly INotificationService _notifications;

        public ProcurementService(
            IProcurementPlanRepository plans,
            IProcurementProcedureRepository procedures,
            IContractRepository contracts,
            IContractMilestoneRepository milestones,
            IAuditLogRepository audit,
            INotificationService notifications)
        {
            _plans = plans ?? throw new ArgumentNullException(nameof(plans));
            _procedures = procedures ?? throw new ArgumentNullException(nameof(procedures));
            _contracts = contracts ?? throw new ArgumentNullException(nameof(contracts));
            _milestones = milestones ?? throw new ArgumentNullException(nameof(milestones));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        }

        // ==================================================================
        // План-график
        // ==================================================================

        public ProcurementPlan CreatePlan(
            string planNumber,
            int year,
            int draftedByEmployeeId,
            DateTime createdAt,
            string notes = null)
        {
            if (string.IsNullOrWhiteSpace(planNumber))
                throw new ArgumentException("Номер плана обязателен.", nameof(planNumber));
            if (year <= 0)
                throw new ArgumentOutOfRangeException(nameof(year), "Год плана должен быть положительным.");
            if (draftedByEmployeeId <= 0)
                throw new ArgumentOutOfRangeException(nameof(draftedByEmployeeId));

            var plan = new ProcurementPlan
            {
                PlanNumber = planNumber.Trim(),
                Year = year,
                CreatedAt = createdAt,
                Status = ProcurementPlanStatus.Draft,
                DraftedByEmployeeId = draftedByEmployeeId,
                Notes = notes
            };

            var saved = _plans.Add(plan);

            _audit.Add(new AuditLog
            {
                ActionType = AuditActionType.ProcurementPlanDrafted,
                Timestamp = DateTime.Now,
                UserId = draftedByEmployeeId,
                Details = string.Format(
                    CultureInfo.InvariantCulture,
                    "Создан проект плана-графика «{0}» на {1} г.",
                    saved.PlanNumber,
                    saved.Year)
            });

            return saved;
        }

        public ProcurementPlanItem AddPlanItem(int planId, ProcurementPlanItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            var plan = LoadPlanOrThrow(planId);
            if (plan.Status != ProcurementPlanStatus.Draft)
                throw new InvalidOperationException(
                    $"Позиции можно добавлять только в проект плана. Текущий статус: {plan.Status}.");
            if (string.IsNullOrWhiteSpace(item.Subject))
                throw new ArgumentException("Предмет закупки обязателен.", nameof(item));
            if (item.MaxPrice < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(item),
                    "Начальная (максимальная) цена контракта не может быть отрицательной.");
            if (item.PlannedQuarter < 1 || item.PlannedQuarter > 4)
                throw new ArgumentOutOfRangeException(
                    nameof(item),
                    "Плановый квартал должен быть в диапазоне 1..4.");

            item.ProcurementPlanId = planId;
            return _plans.AddItem(item);
        }

        public ProcurementPlan ApprovePlan(int planId, int approvedByEmployeeId, DateTime approvedAt)
        {
            var plan = LoadPlanOrThrow(planId);
            if (plan.Status != ProcurementPlanStatus.Draft)
                throw new InvalidOperationException(
                    $"Утвердить можно только проект плана. Текущий статус: {plan.Status}.");
            if (plan.Items == null || plan.Items.Count == 0)
                throw new InvalidOperationException(
                    "План без позиций не может быть утверждён.");

            plan.Status = ProcurementPlanStatus.Approved;
            plan.ApprovedByEmployeeId = approvedByEmployeeId;
            plan.ApprovedAt = approvedAt;
            _plans.Update(plan);

            _audit.Add(new AuditLog
            {
                ActionType = AuditActionType.ProcurementPlanApproved,
                Timestamp = DateTime.Now,
                UserId = approvedByEmployeeId,
                Details = string.Format(
                    CultureInfo.InvariantCulture,
                    "План «{0}» утверждён {1:yyyy-MM-dd}.",
                    plan.PlanNumber,
                    approvedAt)
            });

            return plan;
        }

        public ProcurementPlan PublishPlan(
            int planId,
            DateTime publishedAt,
            IEnumerable<int> notifyEmployeeIds = null)
        {
            var plan = LoadPlanOrThrow(planId);
            if (plan.Status != ProcurementPlanStatus.Approved)
                throw new InvalidOperationException(
                    $"Опубликовать можно только утверждённый план. Текущий статус: {plan.Status}.");

            plan.Status = ProcurementPlanStatus.Published;
            plan.PublishedAt = publishedAt;
            _plans.Update(plan);

            _audit.Add(new AuditLog
            {
                ActionType = AuditActionType.ProcurementPlanPublished,
                Timestamp = DateTime.Now,
                UserId = plan.ApprovedByEmployeeId,
                Details = string.Format(
                    CultureInfo.InvariantCulture,
                    "План «{0}» опубликован в ЕИС {1:yyyy-MM-dd}.",
                    plan.PlanNumber,
                    publishedAt)
            });

            if (notifyEmployeeIds != null)
            {
                var title = string.Format(
                    CultureInfo.InvariantCulture,
                    "План-график «{0}» опубликован",
                    plan.PlanNumber);
                var body = string.Format(
                    CultureInfo.InvariantCulture,
                    "План-график закупок «{0}» на {1} г. опубликован в ЕИС {2:yyyy-MM-dd}. Позиций: {3}.",
                    plan.PlanNumber,
                    plan.Year,
                    publishedAt,
                    plan.Items?.Count ?? 0);

                foreach (var employeeId in notifyEmployeeIds.Where(id => id > 0).Distinct())
                {
                    _notifications.Create(
                        recipientId: employeeId,
                        kind: NotificationKind.ProcurementPlanPublished,
                        title: title,
                        body: body,
                        createdAt: publishedAt);
                }
            }

            return plan;
        }

        public ProcurementPlan ClosePlan(int planId, DateTime closedAt)
        {
            var plan = LoadPlanOrThrow(planId);
            if (plan.Status != ProcurementPlanStatus.Published
                && plan.Status != ProcurementPlanStatus.Approved)
            {
                throw new InvalidOperationException(
                    $"Закрыть можно только утверждённый или опубликованный план. Текущий статус: {plan.Status}.");
            }

            plan.Status = ProcurementPlanStatus.Closed;
            plan.ClosedAt = closedAt;
            _plans.Update(plan);

            _audit.Add(new AuditLog
            {
                ActionType = AuditActionType.ProcurementPlanClosed,
                Timestamp = DateTime.Now,
                Details = string.Format(
                    CultureInfo.InvariantCulture,
                    "План «{0}» закрыт {1:yyyy-MM-dd}.",
                    plan.PlanNumber,
                    closedAt)
            });

            return plan;
        }

        // ==================================================================
        // Закупочная процедура
        // ==================================================================

        public ProcurementProcedure CreateProcedure(
            string noticeNumber,
            int procurementPlanItemId,
            int responsibleEmployeeId,
            ProcurementMethod method,
            decimal maxPrice)
        {
            if (string.IsNullOrWhiteSpace(noticeNumber))
                throw new ArgumentException("Реестровый номер извещения обязателен.", nameof(noticeNumber));
            if (procurementPlanItemId <= 0)
                throw new ArgumentOutOfRangeException(nameof(procurementPlanItemId));
            if (responsibleEmployeeId <= 0)
                throw new ArgumentOutOfRangeException(nameof(responsibleEmployeeId));
            if (maxPrice < 0)
                throw new ArgumentOutOfRangeException(nameof(maxPrice));

            var procedure = new ProcurementProcedure
            {
                NoticeNumber = noticeNumber.Trim(),
                ProcurementPlanItemId = procurementPlanItemId,
                ResponsibleEmployeeId = responsibleEmployeeId,
                Method = method,
                MaxPrice = maxPrice,
                Status = ProcurementProcedureStatus.Planned
            };

            var saved = _procedures.Add(procedure);

            _audit.Add(new AuditLog
            {
                ActionType = AuditActionType.ProcurementProcedureCreated,
                Timestamp = DateTime.Now,
                UserId = responsibleEmployeeId,
                Details = string.Format(
                    CultureInfo.InvariantCulture,
                    "Создана процедура «{0}» ({1}), НМЦК: {2:0.00} ₽.",
                    saved.NoticeNumber,
                    method,
                    maxPrice)
            });

            return saved;
        }

        public ProcurementProcedure ChangeProcedureStatus(
            int procedureId,
            ProcurementProcedureStatus targetStatus,
            DateTime at,
            string winner = null,
            string winnerInn = null,
            decimal? awardedPrice = null,
            int bidsReceived = 0)
        {
            var procedure = _procedures.Get(procedureId)
                ?? throw new InvalidOperationException(
                    $"Процедура #{procedureId} не найдена.");

            if (!IsProcedureTransitionAllowed(procedure.Status, targetStatus))
                throw new InvalidOperationException(
                    $"Недопустимый переход процедуры: {procedure.Status} → {targetStatus}.");

            var previous = procedure.Status;
            procedure.Status = targetStatus;

            switch (targetStatus)
            {
                case ProcurementProcedureStatus.Announced:
                    procedure.NoticePublishedAt = at;
                    break;
                case ProcurementProcedureStatus.BidsCollection:
                    procedure.BidsDeadline = at;
                    break;
                case ProcurementProcedureStatus.BidsEvaluation:
                    procedure.EvaluationDate = at;
                    if (bidsReceived > 0) procedure.BidsReceived = bidsReceived;
                    break;
                case ProcurementProcedureStatus.Awarded:
                    procedure.AwardedAt = at;
                    if (!string.IsNullOrWhiteSpace(winner)) procedure.Winner = winner.Trim();
                    if (!string.IsNullOrWhiteSpace(winnerInn)) procedure.WinnerInn = winnerInn.Trim();
                    if (awardedPrice.HasValue) procedure.AwardedPrice = awardedPrice.Value;
                    break;
                case ProcurementProcedureStatus.ContractSigned:
                case ProcurementProcedureStatus.Cancelled:
                case ProcurementProcedureStatus.Failed:
                    // Терминальные / квазитерминальные статусы — только смена флага.
                    break;
            }

            _procedures.Update(procedure);

            _audit.Add(new AuditLog
            {
                ActionType = AuditActionType.ProcurementProcedureStatusChanged,
                Timestamp = DateTime.Now,
                UserId = procedure.ResponsibleEmployeeId,
                Details = string.Format(
                    CultureInfo.InvariantCulture,
                    "Процедура «{0}»: {1} → {2} ({3:yyyy-MM-dd}).",
                    procedure.NoticeNumber,
                    previous,
                    targetStatus,
                    at)
            });

            return procedure;
        }

        private static bool IsProcedureTransitionAllowed(
            ProcurementProcedureStatus from,
            ProcurementProcedureStatus to)
        {
            if (from == to) return false;
            switch (from)
            {
                case ProcurementProcedureStatus.Planned:
                    return to == ProcurementProcedureStatus.Announced
                           || to == ProcurementProcedureStatus.Cancelled;
                case ProcurementProcedureStatus.Announced:
                    return to == ProcurementProcedureStatus.BidsCollection
                           || to == ProcurementProcedureStatus.Cancelled
                           || to == ProcurementProcedureStatus.Failed;
                case ProcurementProcedureStatus.BidsCollection:
                    return to == ProcurementProcedureStatus.BidsEvaluation
                           || to == ProcurementProcedureStatus.Cancelled
                           || to == ProcurementProcedureStatus.Failed;
                case ProcurementProcedureStatus.BidsEvaluation:
                    return to == ProcurementProcedureStatus.Awarded
                           || to == ProcurementProcedureStatus.Failed
                           || to == ProcurementProcedureStatus.Cancelled;
                case ProcurementProcedureStatus.Awarded:
                    return to == ProcurementProcedureStatus.ContractSigned
                           || to == ProcurementProcedureStatus.Cancelled;
                case ProcurementProcedureStatus.ContractSigned:
                case ProcurementProcedureStatus.Cancelled:
                case ProcurementProcedureStatus.Failed:
                    return false;
                default:
                    return false;
            }
        }

        // ==================================================================
        // Контракт
        // ==================================================================

        public Contract CreateContract(Contract draft, int authorEmployeeId)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            if (authorEmployeeId <= 0)
                throw new ArgumentOutOfRangeException(nameof(authorEmployeeId));
            if (string.IsNullOrWhiteSpace(draft.Title))
                throw new ArgumentException("Заголовок контракта обязателен.", nameof(draft));
            if (draft.Price < 0)
                throw new ArgumentOutOfRangeException(nameof(draft), "Цена контракта не может быть отрицательной.");

            draft.Type = DocumentType.Contract;
            draft.ContractStatus = ContractStatus.Draft;
            draft.Status = DocumentStatus.New;
            draft.AuthorId = authorEmployeeId;
            if (draft.CreationDate == default(DateTime))
                draft.CreationDate = DateTime.Now;

            var saved = _contracts.Add(draft);

            _audit.Add(new AuditLog
            {
                ActionType = AuditActionType.ContractDrafted,
                Timestamp = DateTime.Now,
                UserId = authorEmployeeId,
                Details = string.Format(
                    CultureInfo.InvariantCulture,
                    "Создан проект контракта «{0}», сумма {1:0.00} ₽.",
                    saved.Title,
                    saved.Price)
            });

            return saved;
        }

        public Contract SignContract(
            int contractId,
            DateTime signedAt,
            string contractNumber,
            string registryNumber,
            int actorEmployeeId)
        {
            if (string.IsNullOrWhiteSpace(contractNumber))
                throw new ArgumentException("Номер контракта обязателен.", nameof(contractNumber));

            var contract = LoadContractOrThrow(contractId);
            if (contract.ContractStatus != ContractStatus.Draft)
                throw new InvalidOperationException(
                    $"Подписать можно только проект контракта. Текущий статус: {contract.ContractStatus}.");

            contract.ContractStatus = ContractStatus.Signed;
            contract.SignedAt = signedAt;
            contract.ContractNumber = contractNumber.Trim();
            if (!string.IsNullOrWhiteSpace(registryNumber))
                contract.RegistryNumber = registryNumber.Trim();
            _contracts.Update(contract);

            _audit.Add(new AuditLog
            {
                ActionType = AuditActionType.ContractSigned,
                Timestamp = DateTime.Now,
                UserId = actorEmployeeId,
                Details = string.Format(
                    CultureInfo.InvariantCulture,
                    "Контракт «{0}» подписан {1:yyyy-MM-dd}, реестровый № {2}.",
                    contract.ContractNumber,
                    signedAt,
                    contract.RegistryNumber ?? "—")
            });

            return contract;
        }

        public Contract StartExecution(int contractId, DateTime executionStart)
        {
            var contract = LoadContractOrThrow(contractId);
            if (contract.ContractStatus != ContractStatus.Signed)
                throw new InvalidOperationException(
                    $"Начать исполнение можно только подписанного контракта. Текущий статус: {contract.ContractStatus}.");

            contract.ContractStatus = ContractStatus.InExecution;
            if (!contract.ExecutionStartDate.HasValue)
                contract.ExecutionStartDate = executionStart;
            _contracts.Update(contract);

            _audit.Add(new AuditLog
            {
                ActionType = AuditActionType.ContractExecutionStarted,
                Timestamp = DateTime.Now,
                Details = string.Format(
                    CultureInfo.InvariantCulture,
                    "Контракт «{0}» переведён в исполнение с {1:yyyy-MM-dd}.",
                    contract.ContractNumber ?? contract.Title,
                    contract.ExecutionStartDate)
            });

            return contract;
        }

        public Contract CompleteContract(int contractId, DateTime completedAt)
        {
            var contract = LoadContractOrThrow(contractId);
            if (contract.ContractStatus != ContractStatus.InExecution)
                throw new InvalidOperationException(
                    $"Завершить можно только исполняемый контракт. Текущий статус: {contract.ContractStatus}.");

            contract.ContractStatus = ContractStatus.Completed;
            contract.CompletedAt = completedAt;
            _contracts.Update(contract);

            _audit.Add(new AuditLog
            {
                ActionType = AuditActionType.ContractCompleted,
                Timestamp = DateTime.Now,
                Details = string.Format(
                    CultureInfo.InvariantCulture,
                    "Контракт «{0}» завершён {1:yyyy-MM-dd}.",
                    contract.ContractNumber ?? contract.Title,
                    completedAt)
            });

            return contract;
        }

        public Contract TerminateContract(int contractId, DateTime terminatedAt, string reason)
        {
            var contract = LoadContractOrThrow(contractId);
            if (contract.ContractStatus == ContractStatus.Completed
                || contract.ContractStatus == ContractStatus.Terminated)
            {
                throw new InvalidOperationException(
                    $"Контракт в статусе {contract.ContractStatus} не может быть расторгнут.");
            }

            contract.ContractStatus = ContractStatus.Terminated;
            contract.TerminatedAt = terminatedAt;
            if (!string.IsNullOrWhiteSpace(reason))
                contract.TerminationReason = reason.Trim();
            _contracts.Update(contract);

            _audit.Add(new AuditLog
            {
                ActionType = AuditActionType.ContractTerminated,
                Timestamp = DateTime.Now,
                Details = string.Format(
                    CultureInfo.InvariantCulture,
                    "Контракт «{0}» расторгнут {1:yyyy-MM-dd}. Причина: {2}.",
                    contract.ContractNumber ?? contract.Title,
                    terminatedAt,
                    string.IsNullOrWhiteSpace(reason) ? "не указана" : reason)
            });

            return contract;
        }

        public ContractMilestone AddMilestone(int contractId, ContractMilestone milestone)
        {
            if (milestone == null) throw new ArgumentNullException(nameof(milestone));
            if (string.IsNullOrWhiteSpace(milestone.Title))
                throw new ArgumentException("Описание этапа обязательно.", nameof(milestone));
            if (milestone.Amount < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(milestone),
                    "Сумма этапа не может быть отрицательной.");

            var contract = LoadContractOrThrow(contractId);
            if (contract.ContractStatus == ContractStatus.Completed
                || contract.ContractStatus == ContractStatus.Terminated)
            {
                throw new InvalidOperationException(
                    $"Этапы нельзя добавлять в контракт со статусом {contract.ContractStatus}.");
            }

            milestone.ContractId = contractId;
            if (milestone.Status == 0)
                milestone.Status = ContractMilestoneStatus.Planned;

            return _milestones.Add(milestone);
        }

        public ContractMilestone AcceptMilestone(
            int milestoneId,
            DateTime acceptedAt,
            string acceptanceActNumber,
            decimal? actualAmount = null)
        {
            if (string.IsNullOrWhiteSpace(acceptanceActNumber))
                throw new ArgumentException("Номер акта приёмки обязателен.", nameof(acceptanceActNumber));

            var milestone = _milestones.Get(milestoneId)
                ?? throw new InvalidOperationException($"Этап #{milestoneId} не найден.");

            if (milestone.Status == ContractMilestoneStatus.Accepted)
                throw new InvalidOperationException("Этап уже принят.");

            milestone.Status = ContractMilestoneStatus.Accepted;
            milestone.AcceptedAt = acceptedAt;
            milestone.AcceptanceActNumber = acceptanceActNumber.Trim();
            if (actualAmount.HasValue)
                milestone.Amount = actualAmount.Value;
            _milestones.Update(milestone);

            _audit.Add(new AuditLog
            {
                ActionType = AuditActionType.ContractMilestoneAccepted,
                Timestamp = DateTime.Now,
                Details = string.Format(
                    CultureInfo.InvariantCulture,
                    "Этап #{0} контракта #{1} принят {2:yyyy-MM-dd}, акт № {3}.",
                    milestone.SequenceNumber,
                    milestone.ContractId,
                    acceptedAt,
                    milestone.AcceptanceActNumber)
            });

            return milestone;
        }

        public ContractMilestone RejectMilestone(int milestoneId, string reason)
        {
            var milestone = _milestones.Get(milestoneId)
                ?? throw new InvalidOperationException($"Этап #{milestoneId} не найден.");

            if (milestone.Status == ContractMilestoneStatus.Accepted)
                throw new InvalidOperationException("Принятый этап нельзя отклонить.");

            milestone.Status = ContractMilestoneStatus.Rejected;
            if (!string.IsNullOrWhiteSpace(reason))
                milestone.Notes = string.IsNullOrWhiteSpace(milestone.Notes)
                    ? reason.Trim()
                    : milestone.Notes + Environment.NewLine + "Отклонён: " + reason.Trim();
            _milestones.Update(milestone);

            _audit.Add(new AuditLog
            {
                ActionType = AuditActionType.ContractMilestoneRejected,
                Timestamp = DateTime.Now,
                Details = string.Format(
                    CultureInfo.InvariantCulture,
                    "Этап #{0} контракта #{1} отклонён. Причина: {2}.",
                    milestone.SequenceNumber,
                    milestone.ContractId,
                    string.IsNullOrWhiteSpace(reason) ? "не указана" : reason)
            });

            return milestone;
        }

        // ==================================================================
        // Сканирование уведомлений
        // ==================================================================

        public void ScanUpcomingDeadlines(
            DateTime now,
            int milestoneDaysAhead = 7,
            int contractDaysAhead = 14)
        {
            if (milestoneDaysAhead < 0)
                throw new ArgumentOutOfRangeException(nameof(milestoneDaysAhead));
            if (contractDaysAhead < 0)
                throw new ArgumentOutOfRangeException(nameof(contractDaysAhead));

            var milestoneCount = 0;
            var contractCount = 0;

            foreach (var milestone in _milestones.ListUpcoming(now, milestoneDaysAhead))
            {
                var contract = _contracts.Get(milestone.ContractId);
                if (contract == null) continue;
                if (contract.ContractStatus != ContractStatus.Signed
                    && contract.ContractStatus != ContractStatus.InExecution)
                {
                    continue;
                }

                var recipientId = contract.AssignedEmployeeId ?? contract.AuthorId;
                if (!recipientId.HasValue || recipientId.Value <= 0) continue;

                if (HasNotificationForDay(
                        recipientId.Value,
                        NotificationKind.ContractMilestoneApproaching,
                        relatedDocumentId: contract.Id,
                        day: now.Date))
                {
                    continue;
                }

                var daysLeft = (milestone.PlannedDate.Date - now.Date).Days;
                var title = string.Format(
                    CultureInfo.InvariantCulture,
                    "Контракт «{0}»: этап #{1} через {2} дн.",
                    contract.ContractNumber ?? contract.Title,
                    milestone.SequenceNumber,
                    daysLeft);
                var body = string.Format(
                    CultureInfo.InvariantCulture,
                    "Плановый срок сдачи этапа «{0}» — {1:yyyy-MM-dd}. Сумма этапа: {2:0.00} ₽.",
                    milestone.Title,
                    milestone.PlannedDate,
                    milestone.Amount);

                _notifications.Create(
                    recipientId: recipientId.Value,
                    kind: NotificationKind.ContractMilestoneApproaching,
                    title: title,
                    body: body,
                    docId: contract.Id,
                    createdAt: now);
                milestoneCount++;
            }

            var threshold = now.Date.AddDays(contractDaysAhead);
            foreach (var contract in _contracts.ListByExecutionEndBefore(threshold))
            {
                if (contract.ContractStatus != ContractStatus.Signed
                    && contract.ContractStatus != ContractStatus.InExecution)
                {
                    continue;
                }
                if (!contract.ExecutionEndDate.HasValue) continue;
                if (contract.ExecutionEndDate.Value.Date < now.Date) continue;

                var recipientId = contract.AssignedEmployeeId ?? contract.AuthorId;
                if (!recipientId.HasValue || recipientId.Value <= 0) continue;

                if (HasNotificationForDay(
                        recipientId.Value,
                        NotificationKind.ContractExecutionEndApproaching,
                        relatedDocumentId: contract.Id,
                        day: now.Date))
                {
                    continue;
                }

                var daysLeft = (contract.ExecutionEndDate.Value.Date - now.Date).Days;
                var title = string.Format(
                    CultureInfo.InvariantCulture,
                    "Контракт «{0}»: окончание исполнения через {1} дн.",
                    contract.ContractNumber ?? contract.Title,
                    daysLeft);
                var body = string.Format(
                    CultureInfo.InvariantCulture,
                    "Плановая дата окончания исполнения контракта — {0:yyyy-MM-dd}. Сумма: {1:0.00} ₽.",
                    contract.ExecutionEndDate,
                    contract.Price);

                _notifications.Create(
                    recipientId: recipientId.Value,
                    kind: NotificationKind.ContractExecutionEndApproaching,
                    title: title,
                    body: body,
                    docId: contract.Id,
                    createdAt: now);
                contractCount++;
            }

            _audit.Add(new AuditLog
            {
                ActionType = AuditActionType.ProcurementNotificationScanCompleted,
                Timestamp = DateTime.Now,
                Details = string.Format(
                    CultureInfo.InvariantCulture,
                    "Сканирование закупок на {0:yyyy-MM-dd}: этапы +{1}, окончание исполнения +{2}.",
                    now,
                    milestoneCount,
                    contractCount)
            });
        }

        // ==================================================================
        // Helpers
        // ==================================================================

        private bool HasNotificationForDay(
            int recipientId,
            NotificationKind kind,
            int relatedDocumentId,
            DateTime day)
        {
            return _notifications
                .ListForUser(recipientId, unreadOnly: false)
                .Any(n => n.Kind == kind
                          && n.RelatedDocumentId == relatedDocumentId
                          && n.CreatedAt.Date == day);
        }

        private ProcurementPlan LoadPlanOrThrow(int planId)
        {
            var plan = _plans.Get(planId)
                ?? throw new InvalidOperationException($"План #{planId} не найден.");
            return plan;
        }

        private Contract LoadContractOrThrow(int contractId)
        {
            var contract = _contracts.Get(contractId)
                ?? throw new InvalidOperationException($"Контракт #{contractId} не найден.");
            return contract;
        }
    }
}
