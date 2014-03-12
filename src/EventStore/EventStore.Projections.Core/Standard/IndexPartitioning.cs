// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

using System;
using System.Collections.Generic;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using EventStore.Core.Services;

namespace EventStore.Projections.Core.Standard
{
    // This projection takes the primary indexing stream
    // and partitions the indexing requests by stream
    // It also emits the create/destroy/reset instructions to a separate stream
    // so index procesors can be created per index on start-up and during ordinary runtime
    public class IndexPartitioning : IProjectionStateHandler
    {
        public IndexPartitioning(string source, Action<string> logger)
        {
            if (!string.IsNullOrWhiteSpace(source))
                throw new InvalidOperationException(
                    "Does not require source");
        }

        public void ConfigureSourceProcessingStrategy(SourceDefinitionBuilder builder)
        {
            builder.FromStream("$indexing");
            builder.AllEvents();
            builder.SetIncludeLinks();
        }

        public void Load(string state)
        {
        }

        public void LoadShared(string state)
        {
            throw new NotImplementedException();
        }

        public void Initialize()
        {
        }

        public void InitializeShared()
        {
        }

        public string GetStatePartition(CheckpointTag eventPosition, string category, ResolvedEvent data)
        {
            throw new NotImplementedException();
        }

        public string TransformCatalogEvent(CheckpointTag eventPosition, ResolvedEvent data)
        {
            throw new NotImplementedException();
        }


        private bool IsManagementEvent(string eventType)
        {
            return eventType == "index-creation-requested"
                || eventType == "index-reset-requested";
        }

        public bool ProcessEvent(
            string partition, CheckpointTag eventPosition, string category1, ResolvedEvent data,
            out string newState, out string newSharedState, out EmittedEventEnvelope[] emittedEvents)
        {
            newSharedState = null;
            emittedEvents = null;
            newState = null;

            var eventsToEmit = new List<EmittedEventEnvelope>();

            // Get the index name
            // Get the event type
            string eventType = data.EventType;
            string indexName = "whatever";
            string positionStreamId;

            var isStreamDeletedEvent = StreamDeletedHelper.IsStreamDeletedEvent(
                data.PositionStreamId, data.EventType, data.Data, out positionStreamId);

            if(IsManagementEvent(eventType))
            {
                eventsToEmit.Add(new EmittedEventEnvelope(new EmittedDataEvent(
                        "$indexing-$indexes", Guid.NewGuid(), SystemEventTypes.LinkTo, false,
                        data.EventSequenceNumber + "@" + positionStreamId, null,  eventPosition, expectedTag: null)));
            }

            eventsToEmit.Add(new EmittedEventEnvelope(new EmittedDataEvent(
                    String.Format("$index-{0}", indexName), Guid.NewGuid(), SystemEventTypes.LinkTo, false,
                    data.EventSequenceNumber + "@" + positionStreamId, null,  eventPosition, expectedTag: null)));

            emittedEvents = eventsToEmit.ToArray();
            return true;
        }

        public bool ProcessPartitionCreated(string partition, CheckpointTag createPosition, ResolvedEvent data, out EmittedEventEnvelope[] emittedEvents)
        {
            emittedEvents = null;
            return false;
        }

        public bool ProcessPartitionDeleted(string partition, CheckpointTag deletePosition, out string newState)
        {
            newState = null;
            return false;
        }

        public string TransformStateToResult()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
        }

        public IQuerySources GetSourceDefinition()
        {
            return SourceDefinitionBuilder.From(ConfigureSourceProcessingStrategy);
        }

    }
}
