﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SenseNet.Configuration;
using SenseNet.ContentRepository;
using SenseNet.ContentRepository.Schema;
using SenseNet.ContentRepository.Search;
using SenseNet.ContentRepository.Security;
using SenseNet.ContentRepository.Storage;
using SenseNet.ContentRepository.Storage.Data;
using SenseNet.ContentRepository.Storage.Security;
using SenseNet.Diagnostics;
using SenseNet.Search;
using SenseNet.Security;
using SenseNet.Security.Data;
using SenseNet.Tests.Implementations;

namespace SenseNet.Tests
{
    [TestClass]
    public class TestBase
    {

        private static volatile bool _prototypesCreated;
        private static readonly object PrototypeSync = new object();
        private void EnsurePrototypes()
        {
            if (!_prototypesCreated)
            {
                SnTrace.Test.Write("Wait for creating prototypes.");
                lock (PrototypeSync)
                {
                    if (!_prototypesCreated)
                    {
                        using (var op = SnTrace.Test.StartOperation("Create prototypes."))
                        {
                            ExecuteTest(false, null, () =>
                            {
                                SnTrace.Test.Write("Create initial index.");
                                SaveInitialIndexDocuments();
                                RebuildIndex();

                                SnTrace.Test.Write("Create snapshots.");
                                if (Providers.Instance.DataProvider is InMemoryDataProvider inMemDataProvider)
                                    inMemDataProvider.CreateSnapshot();
                                if (Providers.Instance.SearchEngine is InMemorySearchEngine inMemSearchEngine)
                                    inMemSearchEngine.CreateSnapshot();
                            });
                            _prototypesCreated = true;
                            op.Successful = true;
                        }
                    }
                }
            }
        }

        // ==========================================================

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void InitializeTest()
        {
            SnTrace.Test.Enabled = true;
            SnTrace.Test.Write("START test: {0}", TestContext.TestName);
        }

        [TestCleanup]
        public void CleanupTest()
        {
            SnTrace.Test.Enabled = true;
            SnTrace.Test.Write("END test: {0}", TestContext.TestName);
            SnTrace.Flush();
        }

        protected void Test(Action callback)
        {
            Test(false, null, callback);
        }
        protected void Test(bool useCurrentUser, Action callback)
        {
            Test(useCurrentUser, null, callback);
        }
        protected void Test(Action<RepositoryBuilder> initialize, Action callback)
        {
            Test(false, initialize, callback);
        }
        protected void Test(bool useCurrentUser, Action<RepositoryBuilder> initialize, Action callback)
        {
            EnsurePrototypes();
            ExecuteTest(useCurrentUser, initialize, callback);
        }
        private void ExecuteTest(bool useCurrentUser, Action<RepositoryBuilder> initialize, Action callback)
        {
            DistributedApplication.Cache.Reset();
            ContentTypeManager.Reset();

            var builder = CreateRepositoryBuilderForTest();

            initialize?.Invoke(builder);

            Indexing.IsOuterSearchEngineEnabled = true;

            if (!_prototypesCreated)
                SnTrace.Test.Write("Start repository.");

            using (Repository.Start(builder))
            {
                SnTrace.Test.Write("...run test....");
                if (useCurrentUser)
                    callback();
                else
                    using (new SystemAccount())
                        callback();
            }
        }

        // ==========================================================

        protected T Test<T>(Func<T> callback)
        {
            return Test(false, null, callback);

        }
        protected T Test<T>(bool useCurrentUser, Func<T> callback)
        {
            return Test(useCurrentUser, null, callback);

        }
        protected T Test<T>(Action<RepositoryBuilder> initialize, Func<T> callback)
        {
            return Test(false, initialize, callback);

        }
        protected T Test<T>(bool useCurrentUser, Action<RepositoryBuilder> initialize, Func<T> callback)
        {
            EnsurePrototypes();
            return ExecuteTest<T>(useCurrentUser, initialize, callback);
        }
        private T ExecuteTest<T>(bool useCurrentUser, Action<RepositoryBuilder> initialize, Func<T> callback)
        {
            DistributedApplication.Cache.Reset();
            ContentTypeManager.Reset();

            var builder = CreateRepositoryBuilderForTest();

            initialize?.Invoke(builder);

            Indexing.IsOuterSearchEngineEnabled = true;

            if (!_prototypesCreated)
                SnTrace.Test.Write("Start repository.");

            using (Repository.Start(builder))
            {
                if (useCurrentUser)
                    return callback();
                using (new SystemAccount())
                    return callback();
            }
        }


        protected RepositoryBuilder CreateRepositoryBuilderForTest()
        {
            var dataProvider = new InMemoryDataProvider();
            var securityDataProvider = GetSecurityDataProvider(dataProvider);

            return new RepositoryBuilder()
                .UseAccessProvider(new DesktopAccessProvider())
                .UseDataProvider(dataProvider)
                .UseBlobMetaDataProvider(new InMemoryBlobStorageMetaDataProvider(dataProvider))
                .UseBlobProviderSelector(new InMemoryBlobProviderSelector())
                .UseSearchEngine(new InMemorySearchEngine())
                .UseSecurityDataProvider(securityDataProvider)
                .UseElevatedModificationVisibilityRuleProvider(new ElevatedModificationVisibilityRule())
                .UseCacheProvider(new EmptyCache())
                .StartWorkflowEngine(false)
                .DisableNodeObservers()
                .EnableNodeObservers(typeof(SettingsCache))
                .UseTraceCategories("Test", "Event", "Custom");
        }

        protected static ISecurityDataProvider GetSecurityDataProvider(InMemoryDataProvider repo)
        {
            return new MemoryDataProvider(new DatabaseStorage
            {
                Aces = new List<StoredAce>
                {
                    new StoredAce {EntityId = 2, IdentityId = 1, LocalOnly = false, AllowBits = 0x0EF, DenyBits = 0x000}
                },
                Entities = repo.GetSecurityEntities().ToDictionary(e => e.Id, e => e),
                Memberships = new List<Membership>
                {
                    new Membership
                    {
                        GroupId = Identifiers.AdministratorsGroupId,
                        MemberId = Identifiers.AdministratorUserId,
                        IsUser = true
                    }
                },
                Messages = new List<Tuple<int, DateTime, byte[]>>()
            });
        }

        protected void SaveInitialIndexDocuments()
        {
            var idSet = DataProvider.LoadIdsOfNodesThatDoNotHaveIndexDocument(0, 11000);
            var nodes = Node.LoadNodes(idSet);

            if (nodes.Count == 0)
                return;

            foreach (var node in nodes)
            {
                // ReSharper disable once UnusedVariable
                DataBackingStore.SaveIndexDocument(node, false, false, out var hasBinary);
            }
        }

        protected void RebuildIndex()
        {
            var paths = new List<string>();
            var populator = SearchManager.GetIndexPopulator();
            populator.NodeIndexed += (o, e) => { paths.Add(e.Path); };
            populator.ClearAndPopulateAll();
        }

        protected static ContentQuery CreateSafeContentQuery(string qtext)
        {
            var cquery = ContentQuery.CreateQuery(qtext, QuerySettings.AdminSettings);
            var cqueryAcc = new PrivateObject(cquery);
            cqueryAcc.SetFieldOrProperty("IsSafe", true);
            return cquery;
        }

    }
}
