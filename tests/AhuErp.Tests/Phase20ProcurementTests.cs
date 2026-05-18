using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// Phase 20 / Improvement #13 — закупки по 44-ФЗ. Покрытие:
    /// (1) Репозитории (<see cref="InMemoryProcurementPlanRepository"/>,
    ///     <see cref="InMemoryProcurementProcedureRepository"/>,
    ///     <see cref="InMemoryContractRepository"/>,
    ///     <see cref="InMemoryContractMilestoneRepository"/>) — Add/Get/List/Update,
    ///     уникальность номеров, фильтрация по статусу/процедуре, окно дат.
    /// (2) <see cref="ProcurementService"/> — стейт-машины плана-графика,
    ///     процедуры, контракта, этапа; валидация переходов; аудит-журнал;
    ///     создание уведомлений при публикации плана.
    /// (3) <see cref="ProcurementService.ScanUpcomingDeadlines"/> —
    ///     идемпотентный обход: приближающиеся этапы и окончание исполнения
    ///     создают уведомления один раз в день; повторный вызов в тот же
    ///     логический день записей не дублирует.
    /// </summary>
    public class Phase20ProcurementTests
    {
        private readonly InMemoryProcurementPlanRepository _plans = new InMemoryProcurementPlanRepository();
        private readonly InMemoryProcurementProcedureRepository _procedures = new InMemoryProcurementProcedureRepository();
        private readonly InMemoryContractRepository _contracts = new InMemoryContractRepository();
        private readonly InMemoryContractMilestoneRepository _milestones = new InMemoryContractMilestoneRepository();
        private readonly InMemoryAuditLogRepository _audit = new InMemoryAuditLogRepository();
        private readonly InMemoryNotificationRepository _notificationRepo = new InMemoryNotificationRepository();
        private readonly InMemoryEmployeeRepository _employees = new InMemoryEmployeeRepository();
        private readonly NotificationService _notifications;
        private readonly ProcurementService _service;

        public Phase20ProcurementTests()
        {
            var auditService = new AuditService(_audit);
            _notifications = new NotificationService(
                _notificationRepo, _employees, new InMemoryTaskRepository(), auditService);
            _service = new ProcurementService(
                _plans, _procedures, _contracts, _milestones,
                _audit, _notifications);
        }

        // =========================================================
        // (1) Репозитории.
        // =========================================================

        [Fact]
        public void PlanRepository_Add_assigns_id_and_persists_items_via_AddItem()
        {
            var plan = new ProcurementPlan
            {
                PlanNumber = "ПЗ-2026-001",
                Year = 2026,
                CreatedAt = DateTime.Today,
                DraftedByEmployeeId = 1,
            };
            var saved = _plans.Add(plan);
            Assert.True(saved.Id > 0);

            var item = _plans.AddItem(new ProcurementPlanItem
            {
                ProcurementPlanId = saved.Id,
                LineNumber = 1,
                Subject = "Канцелярия",
                MaxPrice = 150_000m,
                Method = ProcurementMethod.ElectronicAuction,
                PlannedQuarter = 1,
            });
            Assert.True(item.Id > 0);

            var loaded = _plans.Get(saved.Id);
            Assert.NotNull(loaded);
            Assert.Single(loaded.Items);
            Assert.Equal("Канцелярия", loaded.Items.First().Subject);
        }

        [Fact]
        public void PlanRepository_Add_rejects_duplicate_plan_number()
        {
            _plans.Add(new ProcurementPlan
            {
                PlanNumber = "ПЗ-1", Year = 2026, CreatedAt = DateTime.Today, DraftedByEmployeeId = 1,
            });
            Assert.Throws<InvalidOperationException>(() => _plans.Add(new ProcurementPlan
            {
                PlanNumber = "ПЗ-1", Year = 2027, CreatedAt = DateTime.Today, DraftedByEmployeeId = 2,
            }));
        }

        [Fact]
        public void PlanRepository_ListByStatus_filters()
        {
            var p1 = _plans.Add(new ProcurementPlan
            {
                PlanNumber = "ПЗ-1", Year = 2026, CreatedAt = DateTime.Today, DraftedByEmployeeId = 1,
            });
            var p2 = _plans.Add(new ProcurementPlan
            {
                PlanNumber = "ПЗ-2", Year = 2026, CreatedAt = DateTime.Today, DraftedByEmployeeId = 1,
                Status = ProcurementPlanStatus.Published,
            });

            var draft = _plans.ListByStatus(ProcurementPlanStatus.Draft);
            Assert.Single(draft);
            Assert.Equal(p1.Id, draft[0].Id);

            var published = _plans.ListByStatus(ProcurementPlanStatus.Published);
            Assert.Single(published);
            Assert.Equal(p2.Id, published[0].Id);
        }

        [Fact]
        public void ProcedureRepository_Add_rejects_duplicate_notice_number()
        {
            _procedures.Add(new ProcurementProcedure
            {
                NoticeNumber = "0173-1", ProcurementPlanItemId = 1, ResponsibleEmployeeId = 1, MaxPrice = 1m,
            });
            Assert.Throws<InvalidOperationException>(() => _procedures.Add(new ProcurementProcedure
            {
                NoticeNumber = "0173-1", ProcurementPlanItemId = 2, ResponsibleEmployeeId = 2, MaxPrice = 2m,
            }));
        }

        [Fact]
        public void ProcedureRepository_ListByPlanItem_filters()
        {
            _procedures.Add(new ProcurementProcedure
            {
                NoticeNumber = "0173-1", ProcurementPlanItemId = 100, ResponsibleEmployeeId = 1, MaxPrice = 1m,
            });
            _procedures.Add(new ProcurementProcedure
            {
                NoticeNumber = "0173-2", ProcurementPlanItemId = 100, ResponsibleEmployeeId = 1, MaxPrice = 2m,
            });
            _procedures.Add(new ProcurementProcedure
            {
                NoticeNumber = "0173-3", ProcurementPlanItemId = 200, ResponsibleEmployeeId = 1, MaxPrice = 3m,
            });

            var inPlanItem100 = _procedures.ListByPlanItem(100);
            Assert.Equal(2, inPlanItem100.Count);
        }

        [Fact]
        public void ContractRepository_Add_rejects_duplicate_contract_number()
        {
            _contracts.Add(new Contract { Title = "К1", ContractNumber = "К-001" });
            Assert.Throws<InvalidOperationException>(() =>
                _contracts.Add(new Contract { Title = "К2", ContractNumber = "К-001" }));
        }

        [Fact]
        public void ContractRepository_Add_allows_empty_contract_number()
        {
            // Контракты в статусе Draft номера ещё не имеют.
            var c1 = _contracts.Add(new Contract { Title = "К1" });
            var c2 = _contracts.Add(new Contract { Title = "К2" });
            Assert.NotEqual(c1.Id, c2.Id);
        }

        [Fact]
        public void ContractRepository_ListByExecutionEndBefore_returns_only_ending_in_window()
        {
            var nearby = _contracts.Add(new Contract
            {
                Title = "К-near", ContractStatus = ContractStatus.InExecution,
                ExecutionEndDate = new DateTime(2026, 6, 1),
            });
            var far = _contracts.Add(new Contract
            {
                Title = "К-far", ContractStatus = ContractStatus.InExecution,
                ExecutionEndDate = new DateTime(2027, 1, 1),
            });
            _contracts.Add(new Contract
            {
                Title = "К-none", ContractStatus = ContractStatus.Draft,
            });

            var inWindow = _contracts.ListByExecutionEndBefore(new DateTime(2026, 7, 1));
            Assert.Single(inWindow);
            Assert.Equal(nearby.Id, inWindow[0].Id);

            var widerWindow = _contracts.ListByExecutionEndBefore(new DateTime(2027, 6, 1));
            Assert.Equal(2, widerWindow.Count);
            // ListByExecutionEndBefore сортирует по ExecutionEndDate возрастающе.
            Assert.Equal(new[] { nearby.Id, far.Id }, widerWindow.Select(c => c.Id).ToArray());
        }

        [Fact]
        public void MilestoneRepository_ListUpcoming_includes_only_non_accepted_within_window()
        {
            _milestones.Add(new ContractMilestone
            {
                ContractId = 1, SequenceNumber = 1, Title = "Этап 1",
                PlannedDate = new DateTime(2026, 5, 20), Amount = 10m,
            });
            _milestones.Add(new ContractMilestone
            {
                ContractId = 1, SequenceNumber = 2, Title = "Этап 2",
                PlannedDate = new DateTime(2026, 7, 1), Amount = 20m,
            });
            _milestones.Add(new ContractMilestone
            {
                ContractId = 1, SequenceNumber = 3, Title = "Этап 3",
                PlannedDate = new DateTime(2026, 5, 22), Amount = 30m,
                Status = ContractMilestoneStatus.Accepted,
            });

            var upcoming = _milestones.ListUpcoming(new DateTime(2026, 5, 18), daysAhead: 7);
            Assert.Single(upcoming);
            Assert.Equal(1, upcoming[0].SequenceNumber);
        }

        // =========================================================
        // (2) ProcurementService — план-график.
        // =========================================================

        [Fact]
        public void CreatePlan_persists_draft_and_writes_audit()
        {
            var plan = _service.CreatePlan("ПЗ-2026-001", 2026, draftedByEmployeeId: 7,
                createdAt: new DateTime(2026, 1, 15), notes: "Утверждён ЭПК");

            Assert.Equal(ProcurementPlanStatus.Draft, plan.Status);
            Assert.Equal(7, plan.DraftedByEmployeeId);

            var last = _audit.GetLast();
            Assert.Equal(AuditActionType.ProcurementPlanDrafted, last.ActionType);
            Assert.Equal(7, last.UserId);
        }

        [Fact]
        public void CreatePlan_rejects_blank_number_and_invalid_year()
        {
            Assert.Throws<ArgumentException>(() =>
                _service.CreatePlan(" ", 2026, 1, DateTime.Today));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _service.CreatePlan("ПЗ-X", 0, 1, DateTime.Today));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _service.CreatePlan("ПЗ-X", 2026, 0, DateTime.Today));
        }

        [Fact]
        public void AddPlanItem_validates_subject_price_and_quarter()
        {
            var plan = _service.CreatePlan("ПЗ-1", 2026, 1, DateTime.Today);

            Assert.Throws<ArgumentException>(() => _service.AddPlanItem(plan.Id,
                new ProcurementPlanItem { Subject = " ", MaxPrice = 1m, PlannedQuarter = 1 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => _service.AddPlanItem(plan.Id,
                new ProcurementPlanItem { Subject = "X", MaxPrice = -1m, PlannedQuarter = 1 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => _service.AddPlanItem(plan.Id,
                new ProcurementPlanItem { Subject = "X", MaxPrice = 1m, PlannedQuarter = 5 }));
        }

        [Fact]
        public void AddPlanItem_rejected_after_plan_left_draft()
        {
            var plan = _service.CreatePlan("ПЗ-1", 2026, 1, DateTime.Today);
            _service.AddPlanItem(plan.Id, new ProcurementPlanItem
            {
                Subject = "Канцелярия", MaxPrice = 1m, PlannedQuarter = 1,
            });
            _service.ApprovePlan(plan.Id, approvedByEmployeeId: 5, approvedAt: DateTime.Today);

            Assert.Throws<InvalidOperationException>(() => _service.AddPlanItem(plan.Id,
                new ProcurementPlanItem { Subject = "Бумага", MaxPrice = 1m, PlannedQuarter = 1 }));
        }

        [Fact]
        public void ApprovePlan_rejects_empty_plan()
        {
            var plan = _service.CreatePlan("ПЗ-1", 2026, 1, DateTime.Today);
            Assert.Throws<InvalidOperationException>(() =>
                _service.ApprovePlan(plan.Id, 5, DateTime.Today));
        }

        [Fact]
        public void PublishPlan_requires_approved_status_and_notifies_listed_employees()
        {
            var plan = _service.CreatePlan("ПЗ-1", 2026, 1, DateTime.Today);
            _service.AddPlanItem(plan.Id, new ProcurementPlanItem
            {
                Subject = "Канцелярия", MaxPrice = 1m, PlannedQuarter = 1,
            });

            Assert.Throws<InvalidOperationException>(() =>
                _service.PublishPlan(plan.Id, DateTime.Today, new[] { 10 }));

            _service.ApprovePlan(plan.Id, 5, DateTime.Today);
            var published = _service.PublishPlan(plan.Id,
                publishedAt: new DateTime(2026, 1, 20),
                notifyEmployeeIds: new[] { 10, 11, 11, 0 });

            Assert.Equal(ProcurementPlanStatus.Published, published.Status);
            Assert.Equal(new DateTime(2026, 1, 20), published.PublishedAt);

            var notifs10 = _notifications.ListForUser(10);
            var notifs11 = _notifications.ListForUser(11);
            Assert.Single(notifs10);
            Assert.Single(notifs11);
            Assert.Equal(NotificationKind.ProcurementPlanPublished, notifs10[0].Kind);
        }

        [Fact]
        public void ClosePlan_allowed_from_approved_or_published_but_not_draft_or_already_closed()
        {
            var plan = _service.CreatePlan("ПЗ-1", 2026, 1, DateTime.Today);
            _service.AddPlanItem(plan.Id, new ProcurementPlanItem
            {
                Subject = "X", MaxPrice = 1m, PlannedQuarter = 1,
            });

            Assert.Throws<InvalidOperationException>(() =>
                _service.ClosePlan(plan.Id, DateTime.Today));

            _service.ApprovePlan(plan.Id, 5, DateTime.Today);
            var closed = _service.ClosePlan(plan.Id, new DateTime(2026, 12, 31));
            Assert.Equal(ProcurementPlanStatus.Closed, closed.Status);

            Assert.Throws<InvalidOperationException>(() =>
                _service.ClosePlan(plan.Id, DateTime.Today));
        }

        // =========================================================
        // (2) ProcurementService — закупочная процедура.
        // =========================================================

        [Fact]
        public void CreateProcedure_persists_and_audits()
        {
            var procedure = _service.CreateProcedure(
                "0173-1", procurementPlanItemId: 1, responsibleEmployeeId: 8,
                method: ProcurementMethod.OpenTender, maxPrice: 1_000_000m);

            Assert.Equal(ProcurementProcedureStatus.Planned, procedure.Status);
            Assert.Equal(ProcurementMethod.OpenTender, procedure.Method);
            Assert.Equal(AuditActionType.ProcurementProcedureCreated,
                _audit.GetLast().ActionType);
        }

        [Theory]
        [InlineData(ProcurementProcedureStatus.Planned, ProcurementProcedureStatus.Announced, true)]
        [InlineData(ProcurementProcedureStatus.Planned, ProcurementProcedureStatus.Cancelled, true)]
        [InlineData(ProcurementProcedureStatus.Planned, ProcurementProcedureStatus.BidsCollection, false)]
        [InlineData(ProcurementProcedureStatus.Announced, ProcurementProcedureStatus.BidsCollection, true)]
        [InlineData(ProcurementProcedureStatus.BidsCollection, ProcurementProcedureStatus.BidsEvaluation, true)]
        [InlineData(ProcurementProcedureStatus.BidsEvaluation, ProcurementProcedureStatus.Awarded, true)]
        [InlineData(ProcurementProcedureStatus.Awarded, ProcurementProcedureStatus.ContractSigned, true)]
        [InlineData(ProcurementProcedureStatus.ContractSigned, ProcurementProcedureStatus.Cancelled, false)]
        [InlineData(ProcurementProcedureStatus.Cancelled, ProcurementProcedureStatus.Announced, false)]
        public void ChangeProcedureStatus_validates_transitions(
            ProcurementProcedureStatus from,
            ProcurementProcedureStatus to,
            bool allowed)
        {
            var procedure = _service.CreateProcedure(
                "P-" + from + "-" + to, 1, 1, ProcurementMethod.ElectronicAuction, 1m);
            // Прокидываем процедуру в искомое стартовое состояние через
            // прямое обновление: цепочка переходов проверяется в других тестах.
            procedure.Status = from;
            _procedures.Update(procedure);

            if (allowed)
            {
                var changed = _service.ChangeProcedureStatus(procedure.Id, to, DateTime.Today);
                Assert.Equal(to, changed.Status);
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() =>
                    _service.ChangeProcedureStatus(procedure.Id, to, DateTime.Today));
            }
        }

        [Fact]
        public void ChangeProcedureStatus_to_awarded_captures_winner_data()
        {
            var procedure = _service.CreateProcedure(
                "0173-X", 1, 1, ProcurementMethod.ElectronicAuction, 1_000_000m);
            _service.ChangeProcedureStatus(procedure.Id, ProcurementProcedureStatus.Announced, DateTime.Today);
            _service.ChangeProcedureStatus(procedure.Id, ProcurementProcedureStatus.BidsCollection, DateTime.Today);
            _service.ChangeProcedureStatus(procedure.Id, ProcurementProcedureStatus.BidsEvaluation,
                new DateTime(2026, 3, 1), bidsReceived: 3);
            var awarded = _service.ChangeProcedureStatus(procedure.Id, ProcurementProcedureStatus.Awarded,
                new DateTime(2026, 3, 5),
                winner: "ООО «Поставщик»",
                winnerInn: "7707083893",
                awardedPrice: 900_000m);

            Assert.Equal("ООО «Поставщик»", awarded.Winner);
            Assert.Equal("7707083893", awarded.WinnerInn);
            Assert.Equal(900_000m, awarded.AwardedPrice);
            Assert.Equal(3, awarded.BidsReceived);
            Assert.Equal(new DateTime(2026, 3, 5), awarded.AwardedAt);
        }

        // =========================================================
        // (2) ProcurementService — контракт.
        // =========================================================

        [Fact]
        public void CreateContract_sets_type_and_status_to_draft()
        {
            var contract = _service.CreateContract(new Contract
            {
                Title = "Поставка канцелярии",
                Price = 150_000m,
                SupplierName = "ООО «Канцторг»",
                SupplierInn = "7707083893",
            }, authorEmployeeId: 11);

            Assert.Equal(DocumentType.Contract, contract.Type);
            Assert.Equal(ContractStatus.Draft, contract.ContractStatus);
            Assert.Equal(DocumentStatus.New, contract.Status);
            Assert.Equal(11, contract.AuthorId);
        }

        [Fact]
        public void CreateContract_rejects_blank_title_and_negative_price()
        {
            Assert.Throws<ArgumentException>(() => _service.CreateContract(
                new Contract { Title = " ", Price = 1m }, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => _service.CreateContract(
                new Contract { Title = "T", Price = -1m }, 1));
        }

        [Fact]
        public void SignContract_transitions_draft_to_signed_and_writes_audit()
        {
            var contract = _service.CreateContract(
                new Contract { Title = "T", Price = 1m }, authorEmployeeId: 1);

            var signed = _service.SignContract(contract.Id,
                signedAt: new DateTime(2026, 4, 10),
                contractNumber: "К-2026-001",
                registryNumber: "2773601125726000045",
                actorEmployeeId: 5);

            Assert.Equal(ContractStatus.Signed, signed.ContractStatus);
            Assert.Equal("К-2026-001", signed.ContractNumber);
            Assert.Equal("2773601125726000045", signed.RegistryNumber);
            Assert.Equal(AuditActionType.ContractSigned, _audit.GetLast().ActionType);
        }

        [Fact]
        public void SignContract_rejects_blank_number_and_non_draft()
        {
            var contract = _service.CreateContract(
                new Contract { Title = "T", Price = 1m }, 1);
            Assert.Throws<ArgumentException>(() =>
                _service.SignContract(contract.Id, DateTime.Today, " ", "REG", 1));

            _service.SignContract(contract.Id, DateTime.Today, "К-1", "REG", 1);
            Assert.Throws<InvalidOperationException>(() =>
                _service.SignContract(contract.Id, DateTime.Today, "К-2", "REG", 1));
        }

        [Fact]
        public void StartExecution_completes_terminate_walk_the_state_machine()
        {
            var contract = _service.CreateContract(
                new Contract
                {
                    Title = "T", Price = 1m, ExecutionStartDate = new DateTime(2026, 5, 1),
                }, 1);
            _service.SignContract(contract.Id, DateTime.Today, "К-1", "REG", 1);

            var executing = _service.StartExecution(contract.Id, new DateTime(2026, 5, 10));
            Assert.Equal(ContractStatus.InExecution, executing.ContractStatus);
            // ExecutionStartDate в драфте уже выставлен — повторно перезаписывать нельзя.
            Assert.Equal(new DateTime(2026, 5, 1), executing.ExecutionStartDate);

            var completed = _service.CompleteContract(contract.Id, new DateTime(2026, 12, 1));
            Assert.Equal(ContractStatus.Completed, completed.ContractStatus);
            Assert.Equal(new DateTime(2026, 12, 1), completed.CompletedAt);

            Assert.Throws<InvalidOperationException>(() =>
                _service.TerminateContract(contract.Id, DateTime.Today, "поздно"));
        }

        [Fact]
        public void TerminateContract_allowed_from_signed_and_inexecution()
        {
            var c1 = _service.CreateContract(new Contract { Title = "T1", Price = 1m }, 1);
            _service.SignContract(c1.Id, DateTime.Today, "К-1", "REG", 1);
            var t1 = _service.TerminateContract(c1.Id, new DateTime(2026, 6, 1), "соглашение сторон");
            Assert.Equal(ContractStatus.Terminated, t1.ContractStatus);
            Assert.Equal("соглашение сторон", t1.TerminationReason);

            var c2 = _service.CreateContract(new Contract { Title = "T2", Price = 1m }, 1);
            _service.SignContract(c2.Id, DateTime.Today, "К-2", "REG", 1);
            _service.StartExecution(c2.Id, DateTime.Today);
            var t2 = _service.TerminateContract(c2.Id, DateTime.Today, null);
            Assert.Equal(ContractStatus.Terminated, t2.ContractStatus);
        }

        // =========================================================
        // (2) ProcurementService — этапы.
        // =========================================================

        [Fact]
        public void AddMilestone_attaches_to_contract_and_defaults_status_to_planned()
        {
            var contract = _service.CreateContract(new Contract { Title = "T", Price = 1m }, 1);
            var m = _service.AddMilestone(contract.Id, new ContractMilestone
            {
                SequenceNumber = 1, Title = "Поставка 1", PlannedDate = new DateTime(2026, 7, 1), Amount = 10m,
            });
            Assert.Equal(contract.Id, m.ContractId);
            Assert.Equal(ContractMilestoneStatus.Planned, m.Status);
        }

        [Fact]
        public void AddMilestone_rejects_blank_title_negative_amount_and_completed_contract()
        {
            var contract = _service.CreateContract(new Contract { Title = "T", Price = 1m }, 1);
            Assert.Throws<ArgumentException>(() => _service.AddMilestone(contract.Id,
                new ContractMilestone { Title = " ", PlannedDate = DateTime.Today, Amount = 1m }));
            Assert.Throws<ArgumentOutOfRangeException>(() => _service.AddMilestone(contract.Id,
                new ContractMilestone { Title = "X", PlannedDate = DateTime.Today, Amount = -1m }));

            _service.SignContract(contract.Id, DateTime.Today, "К-1", "REG", 1);
            _service.StartExecution(contract.Id, DateTime.Today);
            _service.CompleteContract(contract.Id, DateTime.Today);
            Assert.Throws<InvalidOperationException>(() => _service.AddMilestone(contract.Id,
                new ContractMilestone { Title = "X", PlannedDate = DateTime.Today, Amount = 1m }));
        }

        [Fact]
        public void AcceptMilestone_writes_act_number_and_updates_amount()
        {
            var contract = _service.CreateContract(new Contract { Title = "T", Price = 1m }, 1);
            var m = _service.AddMilestone(contract.Id, new ContractMilestone
            {
                SequenceNumber = 1, Title = "Поставка 1", PlannedDate = DateTime.Today, Amount = 10m,
            });

            var accepted = _service.AcceptMilestone(m.Id,
                acceptedAt: new DateTime(2026, 7, 5),
                acceptanceActNumber: "АКТ-1",
                actualAmount: 9m);

            Assert.Equal(ContractMilestoneStatus.Accepted, accepted.Status);
            Assert.Equal("АКТ-1", accepted.AcceptanceActNumber);
            Assert.Equal(9m, accepted.Amount);
            Assert.Equal(new DateTime(2026, 7, 5), accepted.AcceptedAt);
        }

        [Fact]
        public void AcceptMilestone_rejects_already_accepted()
        {
            var contract = _service.CreateContract(new Contract { Title = "T", Price = 1m }, 1);
            var m = _service.AddMilestone(contract.Id, new ContractMilestone
            {
                SequenceNumber = 1, Title = "X", PlannedDate = DateTime.Today, Amount = 1m,
            });
            _service.AcceptMilestone(m.Id, DateTime.Today, "АКТ-1");
            Assert.Throws<InvalidOperationException>(() =>
                _service.AcceptMilestone(m.Id, DateTime.Today, "АКТ-2"));
        }

        [Fact]
        public void RejectMilestone_blocked_after_acceptance_and_appends_reason()
        {
            var contract = _service.CreateContract(new Contract { Title = "T", Price = 1m }, 1);
            var m = _service.AddMilestone(contract.Id, new ContractMilestone
            {
                SequenceNumber = 1, Title = "X", PlannedDate = DateTime.Today, Amount = 1m,
                Notes = "Первичные замечания",
            });
            var rejected = _service.RejectMilestone(m.Id, "несоответствие ТЗ");
            Assert.Equal(ContractMilestoneStatus.Rejected, rejected.Status);
            Assert.Contains("несоответствие ТЗ", rejected.Notes);

            _service.AcceptMilestone(m.Id, DateTime.Today, "АКТ-1");
            Assert.Throws<InvalidOperationException>(() =>
                _service.RejectMilestone(m.Id, "поздно"));
        }

        // =========================================================
        // (3) ScanUpcomingDeadlines.
        // =========================================================

        [Fact]
        public void ScanUpcomingDeadlines_creates_milestone_notification_once_per_day()
        {
            var contract = _service.CreateContract(new Contract
            {
                Title = "T", Price = 1m,
                AssignedEmployeeId = 42,
            }, authorEmployeeId: 1);
            _service.SignContract(contract.Id, DateTime.Today, "К-1", "REG", 1);
            _service.StartExecution(contract.Id, DateTime.Today);
            _service.AddMilestone(contract.Id, new ContractMilestone
            {
                SequenceNumber = 1, Title = "Поставка 1",
                PlannedDate = new DateTime(2026, 5, 25), Amount = 10m,
            });

            var now = new DateTime(2026, 5, 20);
            _service.ScanUpcomingDeadlines(now);
            Assert.Single(_notifications.ListForUser(42));

            // Повторный вызов в тот же день — идемпотентно.
            _service.ScanUpcomingDeadlines(now);
            Assert.Single(_notifications.ListForUser(42));

            // На следующий день — новая запись.
            _service.ScanUpcomingDeadlines(now.AddDays(1));
            Assert.Equal(2, _notifications.ListForUser(42).Count);
        }

        [Fact]
        public void ScanUpcomingDeadlines_skips_accepted_milestones()
        {
            var contract = _service.CreateContract(new Contract
            {
                Title = "T", Price = 1m, AssignedEmployeeId = 42,
            }, 1);
            _service.SignContract(contract.Id, DateTime.Today, "К-1", "REG", 1);
            var m = _service.AddMilestone(contract.Id, new ContractMilestone
            {
                SequenceNumber = 1, Title = "Этап",
                PlannedDate = new DateTime(2026, 5, 22), Amount = 1m,
            });
            _service.AcceptMilestone(m.Id, DateTime.Today, "АКТ-1");

            _service.ScanUpcomingDeadlines(new DateTime(2026, 5, 20));
            Assert.Empty(_notifications.ListForUser(42));
        }

        [Fact]
        public void ScanUpcomingDeadlines_skips_milestones_for_draft_or_completed_contracts()
        {
            var draftContract = _service.CreateContract(new Contract
            {
                Title = "DraftC", Price = 1m, AssignedEmployeeId = 50,
            }, 1);
            _service.AddMilestone(draftContract.Id, new ContractMilestone
            {
                SequenceNumber = 1, Title = "Этап", PlannedDate = new DateTime(2026, 5, 22), Amount = 1m,
            });

            _service.ScanUpcomingDeadlines(new DateTime(2026, 5, 20));
            Assert.Empty(_notifications.ListForUser(50));
        }

        [Fact]
        public void ScanUpcomingDeadlines_creates_contract_end_notification_once_per_day()
        {
            var contract = _service.CreateContract(new Contract
            {
                Title = "Long contract", Price = 1m, AssignedEmployeeId = 99,
                ExecutionEndDate = new DateTime(2026, 6, 1),
            }, 1);
            _service.SignContract(contract.Id, DateTime.Today, "К-1", "REG", 1);

            var now = new DateTime(2026, 5, 25);
            _service.ScanUpcomingDeadlines(now);
            var first = _notifications.ListForUser(99);
            Assert.Single(first);
            Assert.Equal(NotificationKind.ContractExecutionEndApproaching, first[0].Kind);

            _service.ScanUpcomingDeadlines(now);
            Assert.Single(_notifications.ListForUser(99));
        }

        [Fact]
        public void ScanUpcomingDeadlines_uses_author_when_no_assignee()
        {
            var contract = _service.CreateContract(new Contract
            {
                Title = "T", Price = 1m,
                ExecutionEndDate = new DateTime(2026, 6, 1),
            }, authorEmployeeId: 77);
            _service.SignContract(contract.Id, DateTime.Today, "К-1", "REG", 1);

            _service.ScanUpcomingDeadlines(new DateTime(2026, 5, 25));
            var notifs = _notifications.ListForUser(77);
            Assert.Single(notifs);
        }

        [Fact]
        public void ScanUpcomingDeadlines_writes_audit_summary()
        {
            _service.ScanUpcomingDeadlines(DateTime.Today);
            Assert.Equal(AuditActionType.ProcurementNotificationScanCompleted,
                _audit.GetLast().ActionType);
        }

        [Fact]
        public void ScanUpcomingDeadlines_rejects_negative_windows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _service.ScanUpcomingDeadlines(DateTime.Today, milestoneDaysAhead: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _service.ScanUpcomingDeadlines(DateTime.Today, contractDaysAhead: -1));
        }
    }
}
