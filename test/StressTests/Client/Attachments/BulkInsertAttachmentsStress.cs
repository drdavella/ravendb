﻿using System.Threading.Tasks;
using FastTests;
using SlowTests.Client.Attachments;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace StressTests.Client.Attachments
{
    public class BulkInsertAttachmentsStress : NoDisposalNoOutputNeeded
    {
        public BulkInsertAttachmentsStress(ITestOutputHelper output) : base(output)
        {
        }

        [RavenMultiplatformTheory(RavenTestCategory.Attachments, RavenArchitecture.AllX64)]
        [InlineData(100, 32 * 1024 * 1024)]
        public async Task StoreManyAttachmentsStress(int count, int size)
        {
            using (var test = new BulkInsertAttachments(Output))
            {
                await test.StoreManyAttachments(RavenTestBase.Options.ForMode(RavenDatabaseMode.Single), count, size);
            }
        }

        [RavenMultiplatformTheory(RavenTestCategory.Attachments, RavenArchitecture.AllX64)]
        [InlineData(1000, 100, 32 * 1024)]
        [InlineData(1000, 100, 64 * 1024)]
        public async Task StoreManyAttachmentsAndDocsStress(int count, int attachments, int size)
        {
            using (var test = new BulkInsertAttachments(Output))
            {
                await test.StoreManyAttachmentsAndDocs(RavenTestBase.Options.ForMode(RavenDatabaseMode.Single), count, attachments, size);
            }
        }
    }
}
