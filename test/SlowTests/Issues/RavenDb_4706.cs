﻿using System.Threading.Tasks;
using FastTests;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Tests.Core.Utils.Entities;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SlowTests.Issues
{
    public class RavenDb_4706 : RavenTestBase
    {
        public RavenDb_4706(ITestOutputHelper output) : base(output)
        {
        }

        [RavenTheory(RavenTestCategory.Querying)]
        [RavenData(SearchEngineMode = RavenSearchEngineMode.Lucene, DatabaseMode = RavenDatabaseMode.All)]
        public async Task SupportRandomOrder(Options options)
        {
            using (var documentStore = GetDocumentStore(options))
            {
                using (var session = documentStore.OpenAsyncSession())
                {
                    await session.StoreAsync(new User { Name = "Fitzchak Yitzchaki" });
                    await session.StoreAsync(new User { Name = "Oren Eini" });
                    await session.StoreAsync(new User { Name = "Maxim Buryak" });
                    await session.StoreAsync(new User { Name = "Grisha Kotler" });
                    await session.StoreAsync(new User { Name = "Michael Yarichuk" });
                    await session.SaveChangesAsync();
                }

                using (var session = documentStore.OpenAsyncSession())
                {
                    var users = await session.Query<User>()
                        .Customize(customization => customization.RandomOrdering())
                        .Where(product => product.Name != "Fitzchak Yitzchaki")
                        .Take(2)
                        .ToListAsync();

                    Assert.Equal(2, users.Count);
                }
            }
        }
    }
}
