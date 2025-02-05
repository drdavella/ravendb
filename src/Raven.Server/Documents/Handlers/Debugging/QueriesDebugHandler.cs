﻿using System.Threading.Tasks;
using Raven.Server.Documents.Handlers.Debugging.Processors;
using Raven.Server.Routing;

namespace Raven.Server.Documents.Handlers.Debugging
{
    public sealed class QueriesDebugHandler : DatabaseRequestHandler
    {
        [RavenAction("/databases/*/debug/queries/kill", "POST", AuthorizationStatus.ValidUser, EndpointType.Write)]
        public async Task KillQuery()
        {
            using (var processor = new QueriesDebugHandlerProcessorForKillQuery(this))
                await processor.ExecuteAsync();
        }

        [RavenAction("/databases/*/debug/queries/running", "GET", AuthorizationStatus.ValidUser, EndpointType.Read, IsDebugInformationEndpoint = true)]
        public async Task RunningQueries()
        {
            using (var processor = new QueriesDebugHandlerProcessorForRunningQueries(this))
                await processor.ExecuteAsync();
        }

        [RavenAction("/databases/*/debug/queries/cache/list", "GET", AuthorizationStatus.ValidUser, EndpointType.Read, IsDebugInformationEndpoint = true)]
        public async Task QueriesCacheList()
        {
            using (var processor = new QueriesDebugHandlerProcessorForQueriesCacheList(this))
                await processor.ExecuteAsync();
        }
    }
}
