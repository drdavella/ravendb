﻿using System;
using System.Linq;
using FastTests;
using Raven.Client.Documents;
using Raven.Client.Documents.Indexes;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SlowTests.Issues
{
    public class RavenDB_16334 : RavenTestBase
    {
        public RavenDB_16334(ITestOutputHelper output) : base(output)
        {
        }

        [RavenTheory(RavenTestCategory.Indexes)]
        [RavenData(true, DatabaseMode = RavenDatabaseMode.All)]
        [RavenData(false, DatabaseMode = RavenDatabaseMode.All)]
        public void CanWaitForIndexesWithLoadAfterSaveChanges(Options options, bool allIndexes)
        {
            using (var documentStore = GetDocumentStore(options))
            {
                new MyIndex().Execute(documentStore);
                using (var session = documentStore.OpenSession())
                {
                    session.Store(new MainDocument() { Name = "A" });
                    session.Store(new RelatedDocument() { Name = "A", Value = 21.5m });
                    session.SaveChanges();
                }

                Indexes.WaitForIndexing(documentStore);

                using (var session = documentStore.OpenSession())
                {
                    var result = session.Query<MyIndex.Result, MyIndex>().ProjectInto<MyIndex.Result>().Single();
                    Assert.Equal(21.5m, result.Value);
                }

                // Act
                using (var session = documentStore.OpenSession())
                {
                    session.Advanced.WaitForIndexesAfterSaveChanges(TimeSpan.FromSeconds(15), throwOnTimeout: true, indexes: allIndexes ? null : new[] { "MyIndex" });
                    var related = session.Load<RelatedDocument>("related/A$foo");
                    related.Value = 42m;

                    session.SaveChanges();
                }

                // Assert
                using (var session = documentStore.OpenSession())
                {
                    var result = session.Query<MyIndex.Result, MyIndex>().ProjectInto<MyIndex.Result>().Single();
                    Assert.Equal(42m, result.Value);
                }
            }
        }

        private class MainDocument
        {
            public string Name { get; set; }
            public string Id => $"main/{Name}$foo";
        }

        private class RelatedDocument
        {
            public string Name { get; set; }
            public decimal Value { get; set; }

            public string Id => $"related/{Name}$foo";
        }

        private class MyIndex : AbstractIndexCreationTask<MainDocument>
        {
            public class Result
            {
                public string Name { get; set; }
                public decimal? Value { get; set; }
            }

            public MyIndex()
            {
                Map = mainDocuments => from mainDocument in mainDocuments
                                       let related = LoadDocument<RelatedDocument>($"related/{mainDocument.Name}$foo")
                                       select new Result
                                       {
                                           Name = mainDocument.Name,
                                           Value = related != null ? related.Value : (decimal?)null
                                       };

                StoreAllFields(FieldStorage.Yes);
            }
        }
    }
}
