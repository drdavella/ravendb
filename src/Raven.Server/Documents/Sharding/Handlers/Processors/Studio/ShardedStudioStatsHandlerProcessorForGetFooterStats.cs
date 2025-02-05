﻿using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Raven.Client.Http;
using Raven.Server.Documents.Commands.Studio;
using Raven.Server.Documents.Handlers.Processors.Studio;
using Raven.Server.Documents.Sharding.Executors;
using Raven.Server.Documents.Sharding.Operations;
using Raven.Server.Documents.Studio;
using Raven.Server.ServerWide.Context;

namespace Raven.Server.Documents.Sharding.Handlers.Processors.Studio
{
    internal sealed class ShardedStudioStatsHandlerProcessorForGetFooterStats : AbstractStudioStatsHandlerProcessorForGetFooterStats<ShardedDatabaseRequestHandler, TransactionOperationContext>
    {
        public ShardedStudioStatsHandlerProcessorForGetFooterStats([NotNull] ShardedDatabaseRequestHandler requestHandler) : base(requestHandler)
        {
        }
        
        protected override async ValueTask<FooterStatistics> GetFooterStatisticsAsync()
        {
            var op = new ShardedGetStudioFooterStatsOperation(RequestHandler.HttpContext);
            var stats = await RequestHandler.ShardExecutor.ExecuteParallelForAllAsync(op);
            stats.CountOfIndexes = RequestHandler.DatabaseContext.DatabaseRecord.Indexes.Count;
            return stats;
        }

        private readonly struct ShardedGetStudioFooterStatsOperation : IShardedOperation<FooterStatistics>
        {
            private readonly HttpContext _httpContext;

            public ShardedGetStudioFooterStatsOperation(HttpContext httpContext)
            {
                _httpContext = httpContext;
            }

            public HttpRequest HttpRequest => _httpContext.Request;

            public FooterStatistics Combine(Dictionary<int, ShardExecutionResult<FooterStatistics>> results)
            {
                var result = new FooterStatistics();

                foreach (var stats in results.Values)
                    result.CombineWith(stats.Result);

                return result;
            }

            public RavenCommand<FooterStatistics> CreateCommandForShard(int shardNumber) => new GetStudioFooterStatisticsOperation.GetStudioFooterStatisticsCommand();
        }
    }
}
