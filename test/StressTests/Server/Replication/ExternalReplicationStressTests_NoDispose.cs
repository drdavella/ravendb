﻿using System;
using System.Threading.Tasks;
using FastTests;
using SlowTests.Server.Replication;
using Tests.Infrastructure;
using Xunit.Abstractions;

namespace StressTests.Server.Replication
{
    public class ExternalReplicationStressTests_NoDispose : NoDisposalNoOutputNeeded
    {
        public ExternalReplicationStressTests_NoDispose(ITestOutputHelper output) : base(output)
        {
        }

        [RavenMultiplatformTheory(RavenTestCategory.Replication, RavenArchitecture.AllX64)]
        [RavenData(DatabaseMode = RavenDatabaseMode.All)]
        public void ExternalReplicationShouldWorkWithSmallTimeoutStress(RavenTestBase.Options options)
        {
            for (int i = 0; i < 10; i++)
            {
                Parallel.For(0, 3, RavenTestHelper.DefaultParallelOptions, _ =>
                {
                    using (var test = new ExternalReplicationTests(Output))
                    {
                        test.ExternalReplicationShouldWorkWithSmallTimeoutStress(options, 20000).Wait(TimeSpan.FromMinutes(10));
                    }
                });
            }
        }
    }
}
