﻿using System.Collections.Generic;
using FastTests;
using Raven.Client;
using Raven.Client.Documents.Commands;
using Raven.Client.Documents.Conventions;
using Raven.Client.Documents.Operations.Counters;
using SlowTests.Core.Utils.Entities;
using Sparrow.Json;
using Xunit;
using Xunit.Abstractions;

namespace SlowTests.Issues
{
    public class RavenDB_11819 : RavenTestBase
    {
        public RavenDB_11819(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void IncrementCounter_WhenDocumentHasNoMetadata_ShouldWork()
        {
            //Arrange
            var user = new User { Name = "August" };
            const string id = "users/A-1";
            using (var store = GetDocumentStore())
            using (var context = JsonOperationContext.ShortTermSingleUse())
            {
                var requestExecuter = store.GetRequestExecutor();
                var blitUser = DocumentConventions.Default.Serialization.DefaultConverter.ToBlittable(user, context);
                requestExecuter.Execute(new PutDocumentCommand(store.Conventions, id, null, blitUser), context);

                //Action
                store.Operations.Send(new CounterBatchOperation(new CounterBatch
                {
                    Documents = new List<DocumentCountersOperation>
                    {
                        new DocumentCountersOperation
                        {
                            DocumentId = id,
                            Operations = new List<CounterOperation>
                            {
                                new CounterOperation
                                {
                                    Type = CounterOperationType.Increment,
                                    CounterName = "likes",
                                    Delta = 54
                                }
                            }
                        }
                    }
                }));

                //Assert
                using (var session = store.OpenSession())
                {
                    var counters = session.CountersFor(id);
                    Assert.Equal(54 ,counters.Get("likes"));
                }
            }
        }

        [Fact]
        public void DeleteCounter_WhenHasNoCounters_ShouldNotResultInMetadataWithCounters()
        {
            //Arrange
            var user = new User{ Name = "August" };
            const string id = "users/A-1";
            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenSession())
                {
                    session.Store(user, id);

                    //Action
                    session.CountersFor(id).Delete("Likes");
                    session.SaveChanges();
                }

                //Assert
                using (var session = store.OpenSession())
                {
                    var loadedUser = session.Load<User>(id);
                    var b = session.Advanced.GetMetadataFor(loadedUser);

                    Assert.False(b.TryGetValue(Constants.Documents.Metadata.Counters, out object _),
                        "When delete counter from document without document should no result in metadata with counters");
                }
            }
        }
    }
}
