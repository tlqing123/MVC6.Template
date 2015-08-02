﻿using Microsoft.Data.Entity;
using Microsoft.Data.Entity.ChangeTracking;
using MvcTemplate.Data.Logging;
using MvcTemplate.Objects;
using MvcTemplate.Tests.Data;
using MvcTemplate.Tests.Objects;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace MvcTemplate.Tests.Unit.Data.Logging
{
    public class AuditLoggerTests : IDisposable
    {
        private EntityEntry<BaseModel> entry;
        private TestingContext dataContext;
        private AuditLogger logger;
        private DbContext context;

        public AuditLoggerTests()
        {
            context = new TestingContext();
            dataContext = new TestingContext();
            TestModel model = ObjectFactory.CreateTestModel();
            logger = Substitute.ForPartsOf<AuditLogger>(context, null);

            entry = dataContext.Entry<BaseModel>(dataContext.Set<TestModel>().Add(model).Entity);
            dataContext.Set<TestModel>().RemoveRange(dataContext.Set<TestModel>());
            dataContext.SaveChanges();
        }
        public void Dispose()
        {
            dataContext.Dispose();
            context.Dispose();
            logger.Dispose();
        }

        #region Constructor: AuditLogger(DbContext context, String accountId = null)

        [Fact]
        public void AuditLogger_AccountId_DisablesChangesDetection()
        {
            TestingContext context = new TestingContext();
            context.ChangeTracker.AutoDetectChangesEnabled = true;

            using (new AuditLogger(context, "Test"))
                Assert.False(context.ChangeTracker.AutoDetectChangesEnabled);
        }

        #endregion

        #region Method: Log(IEnumerable<DbEntityEntry<BaseModel>> entries)

        [Fact(Skip = "Log class implementation not supported by EF")]
        public void Log_LogsAddedEntities()
        {
            entry.State = EntityState.Added;

            Logs(entry);
        }

        [Fact(Skip = "Log class implementation not supported by EF")]
        public void Log_LogsModifiedEntities()
        {
            (entry.Entity as TestModel).Text += "Test";
            entry.State = EntityState.Modified;

            Logs(entry);
        }

        [Fact]
        public void Log_DoesNotLogModifiedEntitiesWithoutChanges()
        {
            entry.State = EntityState.Modified;

            logger.Log(new[] { entry });

            logger.DidNotReceiveWithAnyArgs().Log((LoggableEntity)null);
        }

        [Fact(Skip = "Log class implementation not supported by EF")]
        public void Log_LogsDeletedEntities()
        {
            entry.State = EntityState.Deleted;

            Logs(entry);
        }

        [Fact]
        public void Log_DoesNotLogUnsupportedEntityStates()
        {
            IEnumerable<EntityState> unsupportedStates = Enum
                .GetValues(typeof(EntityState))
                .Cast<EntityState>()
                .Where(state =>
                    state != EntityState.Added &&
                    state != EntityState.Modified &&
                    state != EntityState.Deleted);

            foreach (EntityState usupportedState in unsupportedStates)
            {
                entry.State = usupportedState;
                logger.Log(new[] { entry });
            }

            logger.DidNotReceiveWithAnyArgs().Log((LoggableEntity)null);
        }

        [Fact]
        public void Log_DoesNotSaveLogs()
        {
            entry.State = EntityState.Added;

            logger.Log(new[] { entry });

            Assert.Empty(context.Set<AuditLog>());
        }

        #endregion

        #region Method: Log(LoggableEntity entity)

        [InlineData("", "", null)]
        [InlineData(null, "", null)]
        [InlineData("", null, null)]
        [InlineData(null, null, null)]
        [InlineData("", "IdentityId", null)]
        [InlineData("AccountId", "", "AccountId")]
        [InlineData("AccountId", null, "AccountId")]
        [InlineData(null, "IdentityId", "IdentityId")]
        [InlineData("AccountId", "IdentityId", "AccountId")]
        [Theory(Skip = "Log class implementation not supported by EF")]
        public void Log_AddsLogToTheSet(String accountId, String identityName, String expectedAccountId)
        {
            LoggableEntity entity = new LoggableEntity(entry);
            logger = new AuditLogger(context, accountId);

            logger.Log(entity);

            AuditLog actual = context.ChangeTracker.Entries<AuditLog>().First().Entity;
            LoggableEntity expected = entity;

            Assert.Equal(expectedAccountId, actual.AccountId);
            Assert.Equal(expected.ToString(), actual.Changes);
            Assert.Equal(expected.Name, actual.EntityName);
            Assert.Equal(expected.Action, actual.Action);
            Assert.Equal(expected.Id, actual.EntityId);
        }

        [Fact]
        public void Log_DoesNotSaveLog()
        {
            entry.State = EntityState.Added;
            LoggableEntity entity = new LoggableEntity(entry);

            logger.Log(entity);

            Assert.Empty(context.Set<AuditLog>());
        }

        #endregion

        #region Method: Save()

        [Fact]
        public void Save_SavesLogs()
        {
            TestingContext context = Substitute.ForPartsOf<TestingContext>();
            logger = Substitute.ForPartsOf<AuditLogger>(context, null);

            logger.Save();

            context.Received().SaveChanges();
        }

        #endregion

        #region Method: Dispose()

        [Fact]
        public void Dispose_DisposesContext()
        {
            TestingContext context = Substitute.ForPartsOf<TestingContext>();
            logger = Substitute.ForPartsOf<AuditLogger>(context, null);

            logger.Dispose();

            context.Received().Dispose();
        }

        [Fact]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            logger.Dispose();
            logger.Dispose();
        }

        #endregion

        #region Test helpers

        private void Logs(EntityEntry<BaseModel> entry)
        {
            LoggableEntity expected = new LoggableEntity(entry);
            logger.When(sub => sub.Log(Arg.Any<LoggableEntity>())).DoNotCallBase();
            logger.When(sub => sub.Log(Arg.Any<LoggableEntity>())).Do(info =>
            {
                LoggableEntity actual = info.Arg<LoggableEntity>();

                Assert.Equal(expected.ToString(), actual.ToString());
                Assert.Equal(expected.Action, actual.Action);
                Assert.Equal(expected.Name, actual.Name);
                Assert.Equal(expected.Id, actual.Id);
            });

            logger.Log(new[] { entry });

            logger.ReceivedWithAnyArgs().Log(expected);
        }

        #endregion
    }
}
