﻿using System;
using System.Collections.Generic;
using System.Linq;
using FastTests;
using Raven.Client.Documents;
using Raven.Client.Documents.Indexes;
using Raven.Client.Exceptions;
using Raven.Client.Exceptions.Sharding;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SlowTests.MailingList
{
    public class DocumentQueryIncludeAndStreamTest : RavenTestBase
    {
        public DocumentQueryIncludeAndStreamTest(ITestOutputHelper output) : base(output)
        {
        }

        public class ProcessStep
        {
            public string Id { get; set; }

            public string StepExecutionsId { get; set; }

            public string DeviceSerial { get; set; }

            public string StepName { get; set; }

            public int Group { get; set; }

            public DateTime Start { get; set; }

            public DateTime Stop { get; set; }
        }

        public class StepExecutions
        {
            public string Id { get; set; }

            public List<StepExecution> Executions { get; set; } = new List<StepExecution>();
        }

        public class StepExecution
        {
            public int Group { get; set; }

            public DateTime ExecutionStopTime { get; set; }
        }

        public class ProcessStepIndex : AbstractIndexCreationTask<ProcessStep>
        {
            public ProcessStepIndex()
            {
                Map = steps => from step in steps
                               let se = LoadDocument<StepExecutions>(step.StepExecutionsId)
                               select new
                               {
                                   step.DeviceSerial,
                                   step.Group,
                                   step.StepName,
                                   step.Start,
                                   step.Stop,
                                   LatestExecution = se.Executions.All(e => e.ExecutionStopTime < step.Stop),
                                   LatestExecutionInGroup = se.Executions
                                       .Where(e => e.Group == step.Group).All(e => e.ExecutionStopTime < step.Stop)
                               };
            }
        }

        [RavenTheory(RavenTestCategory.Querying)]
        [RavenData(DatabaseMode = RavenDatabaseMode.Single)]
        public void StreamDocumentQueryWithInclude(Options options)
        {
            var store = GetDocumentStore(options);
            Setup(store);
            Indexes.WaitForIndexing(store);
            using (var session = store.OpenSession())
            {
                var query = session.Advanced.RawQuery<ProcessStep>("from ProcessSteps include StepExecutionsId");
                var notSupportedException = Assert.Throws<RavenException>(() =>
                {
                    using (var stream = session.Advanced.Stream(query))
                    {
                        while (stream.MoveNext())
                        {

                        }
                    }
                });
                Assert.Contains("Includes are not supported by this type of query.", notSupportedException.Message);
            }
        }
        
        [RavenFact(RavenTestCategory.Querying)]
        public void StreamDocumentCollectionQueryWithInclude()
        {
            var store = GetDocumentStore();
            Setup(store);
            Indexes.WaitForIndexing(store);
            using (var session = store.OpenSession())
            {
                var query = session.Advanced.RawQuery<ProcessStep>("from ProcessSteps include StepExecutionsId");
                var notSupportedException = Assert.Throws<RavenException>(() =>
                {
                    using (var stream = session.Advanced.Stream(query))
                    {
                        while (stream.MoveNext())
                        {

                        }
                    }
                });
                Assert.Contains("Includes are not supported by this type of query.", notSupportedException.Message);
            }
        }

        [RavenTheory(RavenTestCategory.Querying)]
        [RavenData(DatabaseMode = RavenDatabaseMode.Sharded)]
        public void ShardedStreamDocumentQueryWithInclude(Options options)
        {
            var store = GetDocumentStore(options);
            Setup(store);
            Indexes.WaitForIndexing(store);
            using (var session = store.OpenSession())
            {
                var query = session.Advanced.RawQuery<ProcessStep>("from ProcessSteps include StepExecutionsId");
                var notSupportedException = Assert.Throws<NotSupportedInShardingException>(() =>
                {
                    using (var stream = session.Advanced.Stream(query))
                    {
                    }
                });
                Assert.Contains("Includes and Loads are not supported in sharded streaming queries", notSupportedException.Message);
            }
        }

        void Setup(IDocumentStore store)
        {
            var index = new ProcessStepIndex();
            index.Execute(store);
            using (var session = store.OpenSession())
            {
                var currentTime = DateTime.Now;
                session.Store(new StepExecutions
                {
                    Id = "1234/Characterization",
                    Executions = new List<StepExecution>
                    {
                        new StepExecution
                        {
                            ExecutionStopTime = currentTime,
                            Group = 2
                        },
                        new StepExecution
                        {
                            ExecutionStopTime = currentTime.AddHours(-1.0),
                            Group = 1
                        }
                    }
                });
                session.Store(new ProcessStep
                {
                    DeviceSerial = "1234",
                    StepExecutionsId = "1234/Characterization",
                    StepName = "Characterization",
                    Group = 2,
                    Start = currentTime.AddMinutes(-10.0),
                    Stop = currentTime
                });
                session.SaveChanges();
            }

            Indexes.WaitForIndexing(store);
        }
    }
}
