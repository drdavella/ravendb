﻿using FastTests;
using Orders;
using Raven.Client.Documents.Commands;
using Xunit;
using Xunit.Abstractions;

namespace SlowTests.Issues
{
    public class RavenDB_8063 : RavenTestBase
    {
        public RavenDB_8063(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void IncludeShouldNotReturnDuplicates()
        {
            using (var store = GetDocumentStore())
            {
                string orderId;

                using (var session = store.OpenSession())
                {
                    var employee = new Employee()
                    {
                        FirstName = "John"
                    };

                    session.Store(employee);

                    var order = new Order
                    {
                        Employee = employee.Id
                    };

                    session.Store(order);

                    orderId = order.Id;

                    session.SaveChanges();
                }

                var command = new GetDocumentsCommand(store.Conventions, new[] { orderId }, new[] { "Employee" }, false);
                using (var commands = store.Commands())
                {
                    commands.RequestExecutor.Execute(command, commands.Context);

                    var result = command.Result;
                    Assert.Equal(1, result.Includes.Count);
                }
            }
        }
    }
}
