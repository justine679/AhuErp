namespace AhuErp.Core.Migrations
{
    using System.Data.Entity.Migrations;

    /// <summary>
    /// Phase 20 / Improvement #13 — государственные закупки по 44-ФЗ.
    /// <list type="bullet">
    ///   <item><description><c>ProcurementPlans</c> — план-график закупок по
    ///     ст. 16 № 44-ФЗ. Не наследует <c>Documents</c>; собственная стейт-машина
    ///     <c>Draft → Approved → Published → Closed</c>.</description></item>
    ///   <item><description><c>ProcurementPlanItems</c> — позиции плана-графика
    ///     (ИКЗ, предмет, НМЦК, источник финансирования, способ, плановый
    ///     квартал). Каскадно удаляются вместе с планом.</description></item>
    ///   <item><description><c>ProcurementProcedures</c> — закупочные процедуры
    ///     (извещения в ЕИС). Собственная стейт-машина
    ///     <c>Planned → Announced → BidsCollection → BidsEvaluation → Awarded → ContractSigned</c>
    ///     с возможным переходом в <c>Cancelled</c> / <c>Failed</c>.</description></item>
    ///   <item><description>Расширение TPH-таблицы <c>Documents</c> новым
    ///     дискриминатором <c>Contract</c> + полями: <c>ContractRegistryNumber</c>,
    ///     <c>ContractNumber</c>, <c>SignedAt</c>, <c>ExecutionStartDate</c>,
    ///     <c>ExecutionEndDate</c>, <c>Price</c>, <c>FundingSource</c>,
    ///     <c>SupplierName</c>, <c>SupplierInn</c>, <c>SupplierKpp</c>,
    ///     <c>ProcurementProcedureId</c>, <c>ContractStatus</c>, <c>CompletedAt</c>,
    ///     <c>TerminatedAt</c>, <c>TerminationReason</c>.</description></item>
    ///   <item><description><c>ContractMilestones</c> — этапы исполнения
    ///     контракта (приёмочные акты): порядковый номер, описание, плановая
    ///     дата, фактическая дата приёмки, сумма, статус, номер акта приёмки.
    ///     Каскадно удаляются вместе с контрактом.</description></item>
    /// </list>
    /// Схема создаётся внешним <c>scripts/create-db.sql</c>; миграция документирует
    /// изменения и поддерживает совместимость с EF6 Add-Migration.
    /// </summary>
    /// <remarks>
    /// Снимок модели в .resx сгенерирован вручную как заглушка. После
    /// возврата в среду VS / EF6 PowerShell миграцию следует пересобрать
    /// командой <c>Add-Migration AddProcurementPhase20 -Force</c>,
    /// чтобы зафиксировать корректный EDM-снимок для последующих миграций.
    /// </remarks>
    public partial class AddProcurementPhase20 : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.ProcurementPlans",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    PlanNumber = c.String(nullable: false, maxLength: 64),
                    Year = c.Int(nullable: false),
                    CreatedAt = c.DateTime(nullable: false),
                    Status = c.Int(nullable: false, defaultValue: 0),
                    DraftedByEmployeeId = c.Int(nullable: false),
                    ApprovedByEmployeeId = c.Int(),
                    ApprovedAt = c.DateTime(),
                    PublishedAt = c.DateTime(),
                    ClosedAt = c.DateTime(),
                    Notes = c.String(maxLength: 4096),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Employees", t => t.DraftedByEmployeeId)
                .ForeignKey("dbo.Employees", t => t.ApprovedByEmployeeId)
                .Index(t => t.PlanNumber, unique: true)
                .Index(t => t.Year)
                .Index(t => t.Status)
                .Index(t => t.DraftedByEmployeeId)
                .Index(t => t.ApprovedByEmployeeId);

            CreateTable(
                "dbo.ProcurementPlanItems",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    ProcurementPlanId = c.Int(nullable: false),
                    LineNumber = c.Int(nullable: false, defaultValue: 0),
                    PurchaseCode = c.String(maxLength: 36),
                    Subject = c.String(nullable: false, maxLength: 1024),
                    OkpdCode = c.String(maxLength: 32),
                    MaxPrice = c.Decimal(nullable: false, precision: 18, scale: 2),
                    FundingSource = c.Int(nullable: false, defaultValue: 2),
                    Method = c.Int(nullable: false, defaultValue: 1),
                    PlannedQuarter = c.Int(nullable: false, defaultValue: 1),
                    Justification = c.String(maxLength: 2048),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.ProcurementPlans", t => t.ProcurementPlanId, cascadeDelete: true)
                .Index(t => t.ProcurementPlanId)
                .Index(t => t.PurchaseCode);

            CreateTable(
                "dbo.ProcurementProcedures",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    NoticeNumber = c.String(nullable: false, maxLength: 64),
                    ProcurementPlanItemId = c.Int(nullable: false),
                    Method = c.Int(nullable: false, defaultValue: 1),
                    Status = c.Int(nullable: false, defaultValue: 0),
                    MaxPrice = c.Decimal(nullable: false, precision: 18, scale: 2),
                    NoticePublishedAt = c.DateTime(),
                    BidsDeadline = c.DateTime(),
                    EvaluationDate = c.DateTime(),
                    AwardedAt = c.DateTime(),
                    ResponsibleEmployeeId = c.Int(nullable: false),
                    BidsReceived = c.Int(nullable: false, defaultValue: 0),
                    Winner = c.String(maxLength: 512),
                    WinnerInn = c.String(maxLength: 12),
                    AwardedPrice = c.Decimal(precision: 18, scale: 2),
                    Notes = c.String(maxLength: 4096),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.ProcurementPlanItems", t => t.ProcurementPlanItemId)
                .ForeignKey("dbo.Employees", t => t.ResponsibleEmployeeId)
                .Index(t => t.NoticeNumber, unique: true)
                .Index(t => t.ProcurementPlanItemId)
                .Index(t => t.Status)
                .Index(t => t.ResponsibleEmployeeId);

            // Контракт как TPH-наследник Document.
            AddColumn("dbo.Documents", "ContractRegistryNumber", c => c.String(maxLength: 64));
            AddColumn("dbo.Documents", "ContractNumber", c => c.String(maxLength: 64));
            AddColumn("dbo.Documents", "SignedAt", c => c.DateTime());
            AddColumn("dbo.Documents", "ExecutionStartDate", c => c.DateTime());
            AddColumn("dbo.Documents", "ExecutionEndDate", c => c.DateTime());
            AddColumn("dbo.Documents", "Price", c => c.Decimal(precision: 18, scale: 2));
            AddColumn("dbo.Documents", "FundingSource", c => c.Int());
            AddColumn("dbo.Documents", "SupplierName", c => c.String(maxLength: 512));
            AddColumn("dbo.Documents", "SupplierInn", c => c.String(maxLength: 12));
            AddColumn("dbo.Documents", "SupplierKpp", c => c.String(maxLength: 9));
            AddColumn("dbo.Documents", "ProcurementProcedureId", c => c.Int());
            AddColumn("dbo.Documents", "ContractStatus", c => c.Int());
            AddColumn("dbo.Documents", "CompletedAt", c => c.DateTime());
            AddColumn("dbo.Documents", "TerminatedAt", c => c.DateTime());
            AddColumn("dbo.Documents", "TerminationReason", c => c.String(maxLength: 2048));

            CreateIndex("dbo.Documents", "ProcurementProcedureId");
            AddForeignKey(
                "dbo.Documents",
                "ProcurementProcedureId",
                "dbo.ProcurementProcedures",
                "Id");

            CreateTable(
                "dbo.ContractMilestones",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    ContractId = c.Int(nullable: false),
                    SequenceNumber = c.Int(nullable: false, defaultValue: 0),
                    Title = c.String(nullable: false, maxLength: 512),
                    PlannedDate = c.DateTime(nullable: false),
                    AcceptedAt = c.DateTime(),
                    Amount = c.Decimal(nullable: false, precision: 18, scale: 2),
                    Status = c.Int(nullable: false, defaultValue: 0),
                    AcceptanceActNumber = c.String(maxLength: 64),
                    Notes = c.String(maxLength: 2048),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Documents", t => t.ContractId, cascadeDelete: true)
                .Index(t => t.ContractId)
                .Index(t => t.Status)
                .Index(t => t.PlannedDate);
        }

        public override void Down()
        {
            DropForeignKey("dbo.ContractMilestones", "ContractId", "dbo.Documents");
            DropIndex("dbo.ContractMilestones", new[] { "PlannedDate" });
            DropIndex("dbo.ContractMilestones", new[] { "Status" });
            DropIndex("dbo.ContractMilestones", new[] { "ContractId" });
            DropTable("dbo.ContractMilestones");

            DropForeignKey("dbo.Documents", "ProcurementProcedureId", "dbo.ProcurementProcedures");
            DropIndex("dbo.Documents", new[] { "ProcurementProcedureId" });
            DropColumn("dbo.Documents", "TerminationReason");
            DropColumn("dbo.Documents", "TerminatedAt");
            DropColumn("dbo.Documents", "CompletedAt");
            DropColumn("dbo.Documents", "ContractStatus");
            DropColumn("dbo.Documents", "ProcurementProcedureId");
            DropColumn("dbo.Documents", "SupplierKpp");
            DropColumn("dbo.Documents", "SupplierInn");
            DropColumn("dbo.Documents", "SupplierName");
            DropColumn("dbo.Documents", "FundingSource");
            DropColumn("dbo.Documents", "Price");
            DropColumn("dbo.Documents", "ExecutionEndDate");
            DropColumn("dbo.Documents", "ExecutionStartDate");
            DropColumn("dbo.Documents", "SignedAt");
            DropColumn("dbo.Documents", "ContractNumber");
            DropColumn("dbo.Documents", "ContractRegistryNumber");

            DropForeignKey("dbo.ProcurementProcedures", "ResponsibleEmployeeId", "dbo.Employees");
            DropForeignKey("dbo.ProcurementProcedures", "ProcurementPlanItemId", "dbo.ProcurementPlanItems");
            DropIndex("dbo.ProcurementProcedures", new[] { "ResponsibleEmployeeId" });
            DropIndex("dbo.ProcurementProcedures", new[] { "Status" });
            DropIndex("dbo.ProcurementProcedures", new[] { "ProcurementPlanItemId" });
            DropIndex("dbo.ProcurementProcedures", new[] { "NoticeNumber" });
            DropTable("dbo.ProcurementProcedures");

            DropForeignKey("dbo.ProcurementPlanItems", "ProcurementPlanId", "dbo.ProcurementPlans");
            DropIndex("dbo.ProcurementPlanItems", new[] { "PurchaseCode" });
            DropIndex("dbo.ProcurementPlanItems", new[] { "ProcurementPlanId" });
            DropTable("dbo.ProcurementPlanItems");

            DropForeignKey("dbo.ProcurementPlans", "ApprovedByEmployeeId", "dbo.Employees");
            DropForeignKey("dbo.ProcurementPlans", "DraftedByEmployeeId", "dbo.Employees");
            DropIndex("dbo.ProcurementPlans", new[] { "ApprovedByEmployeeId" });
            DropIndex("dbo.ProcurementPlans", new[] { "DraftedByEmployeeId" });
            DropIndex("dbo.ProcurementPlans", new[] { "Status" });
            DropIndex("dbo.ProcurementPlans", new[] { "Year" });
            DropIndex("dbo.ProcurementPlans", new[] { "PlanNumber" });
            DropTable("dbo.ProcurementPlans");
        }
    }
}
