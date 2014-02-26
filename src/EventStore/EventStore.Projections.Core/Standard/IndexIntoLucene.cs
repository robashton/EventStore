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
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.v8;

namespace EventStore.Projections.Core.Standard
{
    public class IndexIntoLucene : IProjectionStateHandler
    {
        IntPtr _indexingHandle;
        Js1.LogDelegate _logHandler;
        Action<string> _logger;

        private void LogHandler(string message)
        {
            if (_logger != null)
                _logger(message);
            else
                Console.WriteLine(message);
        }

        public IndexIntoLucene(string source, Action<string> logger)
        {
            if (!string.IsNullOrWhiteSpace(source))
                throw new InvalidOperationException(
                    "Does not require source");
            this._logger = logger;
            this._logHandler = LogHandler;
        }

        public void ConfigureSourceProcessingStrategy(SourceDefinitionBuilder builder)
        {
            builder.FromStream("$indexing");
            builder.AllEvents();
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
            _indexingHandle = Js1.OpenIndexingSystem("Indexes", _logHandler);
        }

        public void InitializeShared()
        {
        }

        public string GetStatePartition(CheckpointTag eventPosition, string category, ResolvedEvent data)
        {
            throw new NotImplementedException();
        }

        public bool ProcessPartitionCreated(string partition, CheckpointTag createPosition, ResolvedEvent data, out EmittedEventEnvelope[] emittedEvents)
        {
            emittedEvents = null;
            return false;
        }

        public bool ProcessPartitionDeleted(string partition, CheckpointTag deletePosition, out string newState)
        {
            throw new NotImplementedException();
        }

        public string TransformCatalogEvent(CheckpointTag eventPosition, ResolvedEvent data)
        {
            throw new NotImplementedException();
        }

        public bool ProcessEvent(
            string partition, CheckpointTag eventPosition, string category1, ResolvedEvent data,
            out string newState, out string newSharedState, out EmittedEventEnvelope[] emittedEvents)
        {
            newSharedState = null;
            emittedEvents = null;
            newState = null;
            Js1.HandleIndexCommand(_indexingHandle, data.EventType, data.Data);
            return true;
        }

        public string TransformStateToResult()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            Js1.CloseIndexingSystem(_indexingHandle);
        }

        public IQuerySources GetSourceDefinition()
        {
            return SourceDefinitionBuilder.From(ConfigureSourceProcessingStrategy);
        }

    }
}
