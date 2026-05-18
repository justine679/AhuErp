using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Infrastructure;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// Регрессия на коллизию TPH-колонок в <c>dbo.Documents</c> между
    /// <see cref="ItTicket.CompletedAt"/> и <see cref="Contract.CompletedAt"/>:
    /// без явного <c>HasColumnName</c> EF6 даёт второму свойству суффикс «1»
    /// и SQL-запросы падают с <c>Invalid column name 'CompletedAt1'</c>.
    /// Тест проверяет реальный EDM-маппинг, а не in-memory репо.
    /// </summary>
    public class DocumentTphMappingTests
    {
        private static IReadOnlyDictionary<string, string> GetDocumentsColumnMap()
        {
            // Контекст с фейковой строкой подключения — мы не открываем соединение,
            // только триггерим OnModelCreating и читаем MetadataWorkspace.
            using (var ctx = new AhuDbContext("Server=.;Database=AhuMappingProbe;Integrated Security=true"))
            {
                var objectContext = ((IObjectContextAdapter)ctx).ObjectContext;
                var workspace = objectContext.MetadataWorkspace;

                var storeContainer = workspace
                    .GetItems<EntityContainer>(DataSpace.SSpace)
                    .Single();
                var documentsSet = storeContainer.EntitySets
                    .Single(s => string.Equals(s.Table, "Documents", StringComparison.OrdinalIgnoreCase));

                return documentsSet.ElementType.Properties
                    .ToDictionary(p => p.Name, p => p.TypeUsage.EdmType.Name, StringComparer.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void Documents_table_must_not_contain_auto_generated_CompletedAt1()
        {
            var columns = GetDocumentsColumnMap();
            var allColumns = string.Join(", ", columns.Keys.OrderBy(k => k));
            Assert.False(
                columns.ContainsKey("CompletedAt1"),
                $"EF6 сгенерировал колонку CompletedAt1 — значит, Contract.CompletedAt не " +
                $"замаплен явно. Все колонки: [{allColumns}]");
        }

        [Fact]
        public void Documents_table_has_both_CompletedAt_and_ContractCompletedAt()
        {
            var columns = GetDocumentsColumnMap();
            Assert.True(
                columns.ContainsKey("CompletedAt"),
                "ItTicket.CompletedAt должен сохраняться в колонке CompletedAt (Phase 14).");
            Assert.True(
                columns.ContainsKey("ContractCompletedAt"),
                "Contract.CompletedAt должен быть явно замаплен на ContractCompletedAt (Phase 20-fix).");
        }
    }
}
