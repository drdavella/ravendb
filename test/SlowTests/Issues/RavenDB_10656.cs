﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FastTests.Utils;
using Orders;
using Raven.Client;
using Raven.Client.Documents.Operations.Revisions;
using Raven.Server.Documents;
using Raven.Server.NotificationCenter;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SlowTests.Issues
{
    public class RavenDB_10656 : ReplicationTestBase, ITombstoneAware
    {
        public RavenDB_10656(ITestOutputHelper output) : base(output)
        {
        }

        [RavenTheory(RavenTestCategory.Revisions | RavenTestCategory.Replication)]
        [RavenData(DatabaseMode = RavenDatabaseMode.All)]
        public async Task RevisionsWillBeReplicatedEvenIfTheyAreNotConfiguredOnTheDestinationNode(Options options)
        {
            var company = new Company { Name = "Company Name" };
            using (var store1 = GetDocumentStore(options))
            using (var store2 = GetDocumentStore(options))
            {
                await RevisionsHelper.SetupRevisionsAsync(store1);
                //await RevisionsHelper.SetupRevisionsAsync(store2); // not setting up revisions on purpose
                await SetupReplicationAsync(store1, store2);

                using (var session = store1.OpenAsyncSession())
                {
                    await session.StoreAsync(company);
                    await session.SaveChangesAsync();
                }
                using (var session = store1.OpenAsyncSession())
                {
                    var company3 = await session.LoadAsync<Company>(company.Id);
                    company3.Name = "Hibernating Rhinos";
                    await session.SaveChangesAsync();
                }

                await EnsureReplicatingAsync(store1, store2);

                using (var session = store2.OpenAsyncSession())
                {
                    var companiesRevisions = await session.Advanced.Revisions.GetForAsync<Company>(company.Id);
                    Assert.Equal(2, companiesRevisions.Count);
                    Assert.Equal("Hibernating Rhinos", companiesRevisions[0].Name);
                    Assert.Equal("Company Name", companiesRevisions[1].Name);
                }
            }
        }

        [RavenTheory(RavenTestCategory.Revisions | RavenTestCategory.Replication)]
        [RavenData(DatabaseMode = RavenDatabaseMode.All)]
        public async Task RevisionsWillBeReplicatedEvenIfTheyAreDisabledOnTheDestinationNode(Options options)
        {
            var company = new Company { Name = "Company Name" };
            using (var store1 = GetDocumentStore(options))
            using (var store2 = GetDocumentStore(options))
            {
                await RevisionsHelper.SetupRevisionsAsync(store1);
                await RevisionsHelper.SetupRevisionsAsync(store2, modifyConfiguration: configuration => configuration.Collections["Companies"] = new RevisionsCollectionConfiguration
                {
                    Disabled = true
                });
                await SetupReplicationAsync(store1, store2);

                using (var session = store1.OpenAsyncSession())
                {
                    await session.StoreAsync(company);
                    await session.SaveChangesAsync();
                }
                using (var session = store1.OpenAsyncSession())
                {
                    var company3 = await session.LoadAsync<Company>(company.Id);
                    company3.Name = "Hibernating Rhinos";
                    await session.SaveChangesAsync();
                }

                await EnsureReplicatingAsync(store1, store2);

                using (var session = store2.OpenAsyncSession())
                {
                    var companiesRevisions = await session.Advanced.Revisions.GetForAsync<Company>(company.Id);
                    Assert.Equal(2, companiesRevisions.Count);
                    Assert.Equal("Hibernating Rhinos", companiesRevisions[0].Name);
                    Assert.Equal("Company Name", companiesRevisions[1].Name);
                }
            }
        }

        [RavenTheory(RavenTestCategory.Revisions | RavenTestCategory.Replication)]
        [RavenData(DatabaseMode = RavenDatabaseMode.All)]
        public async Task RevisionsDeletesWillBeReplicatedEvenIfTheyAreDisabledOnTheDestinationNode(Options options)
        {
            var company = new Company { Name = "Company Name" };
            using (var store1 = GetDocumentStore(options))
            using (var store2 = GetDocumentStore(options))
            {
                await RevisionsHelper.SetupRevisionsAsync(store1);
                await RevisionsHelper.SetupRevisionsAsync(store2, modifyConfiguration: configuration => configuration.Collections["Companies"] = new RevisionsCollectionConfiguration
                {
                    Disabled = true
                });
                await SetupReplicationAsync(store1, store2);

                using (var session = store1.OpenAsyncSession())
                {
                    await session.StoreAsync(company);
                    await session.SaveChangesAsync();

                    session.Delete(company);
                    await session.SaveChangesAsync();
                }

                await EnsureReplicatingAsync(store1, store2);

                using (var session = store2.OpenAsyncSession())
                {
                    var revisionsMetadata = await session.Advanced.Revisions.GetMetadataForAsync(company.Id);
                    Assert.Equal(2, revisionsMetadata.Count);
                    Assert.Contains(DocumentFlags.DeleteRevision.ToString(), revisionsMetadata[0].GetString(Constants.Documents.Metadata.Flags));
                    Assert.Contains(DocumentFlags.Revision.ToString(), revisionsMetadata[1].GetString(Constants.Documents.Metadata.Flags));
                }
            }
        }

        public string TombstoneCleanerIdentifier => nameof(RavenDB_10656);

        public Dictionary<string, long> GetLastProcessedTombstonesPerCollection(ITombstoneAware.TombstoneType tombstoneType)
        {
            return new Dictionary<string, long>
            {
                ["Products"] = 0,
                ["Users"] = 0
            };
        }

        public Dictionary<TombstoneDeletionBlockageSource, HashSet<string>> GetDisabledSubscribersCollections(HashSet<string> tombstoneCollections)
        {
            throw new NotImplementedException();
        }
    }
}
