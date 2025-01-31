﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Raven.Client.Documents;
using Raven.Client.Documents.Conventions;
using Raven.Tests.Core.Utils.Entities;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SlowTests.Server.Documents.ETL.Raven
{
    public class RavenDB_13288 : ReplicationTestBase
    {
        public RavenDB_13288(ITestOutputHelper output) : base(output)
        {
        }

        [RavenFact(RavenTestCategory.Etl)]
        public async Task ShouldSendCounterChangeMadeInCluster()
        {
            var srcDb = "13288-src";
            var dstDb = "13288-dst";

            var (_, srcRaft) = await CreateRaftCluster(2);
            var (_, dstRaft) = await CreateRaftCluster(1);
            var srcNodes = await CreateDatabaseInCluster(srcDb, 2, srcRaft.WebUrl);
            var destNode = await CreateDatabaseInCluster(dstDb, 1, dstRaft.WebUrl);

            using (var src = new DocumentStore
            {
                Urls = srcNodes.Servers.Select(s => s.WebUrl).ToArray(),
                Database = srcDb,
            }.Initialize())
            using (var dest = new DocumentStore
            {
                Urls = new[] { destNode.Servers[0].WebUrl },
                Database = dstDb,
            }.Initialize())
            {
                Etl.AddEtl((DocumentStore)src, (DocumentStore)dest, "Users", script: null);

                var aNode = srcNodes.Servers.Single(s => s.ServerStore.NodeTag == "A");
                var bNode = srcNodes.Servers.Single(s => s.ServerStore.NodeTag == "B");

                // modify counter on A node (mentor of ETL task)

                using (var aSrc = new DocumentStore
                {
                    Urls = new[] { aNode.WebUrl },
                    Database = srcDb,
                    Conventions = new DocumentConventions
                    {
                        DisableTopologyUpdates = true
                    }
                }.Initialize())
                {
                    using (var session = aSrc.OpenSession())
                    {
                        session.Store(new User()
                        {
                            Name = "Joe Doe"
                        }, "users/1");

                        session.CountersFor("users/1").Increment("likes");

                        session.Advanced.WaitForReplicationAfterSaveChanges();

                        session.SaveChanges();
                    }
                }

                Assert.True(WaitForDocument<User>(dest, "users/1", u => u.Name == "Joe Doe", 30_000));

                using (var session = dest.OpenSession())
                {
                    var user = session.Load<User>("users/1");

                    Assert.NotNull(user);
                    Assert.Equal("Joe Doe", user.Name);

                    var counter = session.CountersFor("users/1").Get("likes");

                    Assert.NotNull(counter);
                    Assert.Equal(1, counter.Value);
                }

                // modify counter on B node (not mentor)

                using (var bSrc = new DocumentStore
                {
                    Urls = new[] { bNode.WebUrl },
                    Database = srcDb,
                    Conventions = new DocumentConventions
                    {
                        DisableTopologyUpdates = true
                    }
                }.Initialize())
                {
                    using (var session = bSrc.OpenSession())
                    {
                        session.CountersFor("users/1").Increment("likes");

                        session.SaveChanges();
                    }
                }

                Assert.True(Replication.WaitForCounterReplication(new List<IDocumentStore>
                {
                    dest
                }, "users/1", "likes", 2, TimeSpan.FromSeconds(60)));
            }
        }
    }
}
