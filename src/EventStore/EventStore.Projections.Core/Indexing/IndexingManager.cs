// Copyright (c) 2012, Event Store LLP
// All rights reserved.
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
using System.ComponentModel;
using System.Linq;
using System.Text;
using EventStore.Common.Log;
using EventStore.Common.Options;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.UserManagement;
using EventStore.Core.Util;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Standard;
using ReadStreamResult = EventStore.Core.Data.ReadStreamResult;

namespace EventStore.Projections.Core.Indexing
{
    public class IndexingManager : IHandle<ClientMessage.ReadStreamEventsBackwardCompleted>,
                                   IHandle<EventReaderSubscriptionMessage.CommittedEventReceived>,
                                   IHandle<EventReaderSubscriptionMessage.EofReached>,
                                   IHandle<EventReaderSubscriptionMessage.PartitionEofReached>,
                                   IHandle<EventReaderSubscriptionMessage.CheckpointSuggested>,
                                   IHandle<EventReaderSubscriptionMessage.NotAuthorized>
    {

        private readonly ILogger _logger = LogManager.GetLoggerFor<IndexingManager>();

        private readonly QueuedHandler _inputQueue;
        private readonly IPublisher _publisher;
        private readonly ITimeProvider _timeProvider;
        private readonly HashSet<string> _indexes = new HashSet<string>();
        private Guid  _subscriptionId;

        private readonly RequestResponseDispatcher<ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted>
            _readDispatcher;

        private readonly PublishSubscribeDispatcher
                <Guid, ReaderSubscriptionManagement.Subscribe,
                ReaderSubscriptionManagement.ReaderSubscriptionManagementMessage, EventReaderSubscriptionMessage>
                _subscriptionDispatcher;

        private int _readEventsBatchSize = 100;
        private int _lastEventRead = -1;
        private bool _started;

        public IndexingManager(
            QueuedHandler inputQueue,
            IPublisher publisher,
            PublishSubscribeDispatcher
                <Guid, ReaderSubscriptionManagement.Subscribe,
                ReaderSubscriptionManagement.ReaderSubscriptionManagementMessage, EventReaderSubscriptionMessage>
                subscriptionDispatcher,
                ITimeProvider timeProvider)
        {
            _inputQueue = inputQueue;
            _publisher = publisher;
            _subscriptionDispatcher = subscriptionDispatcher;
            _timeProvider = timeProvider;
            _logger.Info("Creating a goddamned IndexingManager");

            _readDispatcher =
                new RequestResponseDispatcher
                    <ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted>(
                    publisher, v => v.CorrelationId, v => v.CorrelationId, new PublishEnvelope(_inputQueue));
        }

        public void Start()
        {
            // Start listening for changes
            var streamName = "$indexing-$indexes";
            var sourceDefinition = new SourceDefinitionBuilder();
            sourceDefinition.FromStream(streamName);
            sourceDefinition.IncludeEvent(IndexingEvents.IndexCreationRequested);
            sourceDefinition.IncludeEvent(IndexingEvents.IndexResetRequested);

            CheckpointTag fromPosition = CheckpointTag.FromStreamPosition(0, streamName, _lastEventRead);
            var readerStrategy = ReaderStrategy.Create(0, sourceDefinition.Build(), _timeProvider, stopOnEof: false, runAs: SystemAccount.Principal);
            var readerOptions = new ReaderSubscriptionOptions(1024*1024, 1024, stopOnEof: false, stopAfterNEvents: null);
            _subscriptionId =
                _subscriptionDispatcher.PublishSubscribe(
                    new ReaderSubscriptionManagement.Subscribe(
                        Guid.NewGuid(), fromPosition, readerStrategy, readerOptions), this);
            _logger.Info("Subscribing for index management with subscription {0}", _subscriptionId);
            _started = true;
        }

        private void Stop()
        {
            _started = false;
            _subscriptionDispatcher.Cancel(_subscriptionId);
            _readDispatcher.CancelAll();
        }

        public void Handle(ClientMessage.ReadStreamEventsBackwardCompleted message)
        {
            _readDispatcher.Handle(message);
        }

        public void Handle(EventReaderSubscriptionMessage.CommittedEventReceived message)
        {
            _logger.Info("Received event from the boss {0}", message.Data.EventType);
            string indexName = message.Data.Data.EventIndexName();
            switch(message.Data.EventType)
            {
                case IndexingEvents.IndexCreationRequested:
                    AddIndex(indexName);
                    break;
                case IndexingEvents.IndexResetRequested:
                    ResetIndex(indexName);
                    break;
                default:
                    throw new InvalidOperationException(String.Format("Unexpected event in indexing manager {0}", message.Data.EventType));
            }
        }

        public void Handle(EventReaderSubscriptionMessage.CheckpointSuggested message) { }
        public void Handle(EventReaderSubscriptionMessage.NotAuthorized message)
        {
            _logger.Error("This should never happen");
        }
        public void Handle(EventReaderSubscriptionMessage.PartitionEofReached message)
        {
            _logger.Error("This should never happen");
        }
        public void Handle(EventReaderSubscriptionMessage.EofReached message)
        {
            _logger.Error("This should never happen");
        }

        public void RetrieveInitialIndexList(Action<string[]> callback, int from = -1)
        {
            var corrId = Guid.NewGuid();
            _readDispatcher.Publish(
                new ClientMessage.ReadStreamEventsBackward(
                    corrId, corrId, _readDispatcher.Envelope, "$indexing-$indexes", from, _readEventsBatchSize,
                    resolveLinkTos: true, requireMaster: false, validationStreamVersion: null, user: SystemAccount.Principal),
                m => LoadIndexListCompleted(m, from, callback));
        }

        private void AddIndex(string indexName)
        {
            _indexes.Add(indexName);
            _publisher.Publish(new IndexingMessage.AddIndex(indexName));
        }

        private void ResetIndex(string indexName)
        {
            _publisher.Publish(new IndexingMessage.ResetIndex(indexName));
        }

        private void LoadIndexListCompleted(
            ClientMessage.ReadStreamEventsBackwardCompleted completed, int requestedFrom, Action<string[]> callback)
        {
            if (completed.Result == ReadStreamResult.Success)
            {
                var events = completed.Events.Where(e => e.Event.EventType == IndexingEvents.IndexCreationRequested).ToArray();
                if (events.IsNotEmpty())
                    foreach (var @event in events)
                    {
                        var eventData = Helper.UTF8NoBom.GetString(@event.Event.Data);
                        var indexName = eventData.EventIndexName();
                        _indexes.Add(indexName);
                        _lastEventRead = @event.Event.EventNumber;
                    }
            }

            if (completed.Result == ReadStreamResult.Success && !completed.IsEndOfStream)
            {
                RetrieveInitialIndexList(callback, @from: completed.NextEventNumber);
                return;
            }
            callback(_indexes.ToArray());
        }
    }
}
