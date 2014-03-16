// Copyright (c) 2012, Event Store LLP // All rights reserved.
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
using System.Security.Principal;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.EventReaders.Feeds;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Core.Services.UserManagement;
using EventStore.Common.Log;

namespace EventStore.Projections.Core.Indexing
{
    public class IndexingReader : IHandle<EventReaderSubscriptionMessage.CommittedEventReceived>,
                                  IHandle<EventReaderSubscriptionMessage.EofReached>,
                                  IHandle<EventReaderSubscriptionMessage.PartitionEofReached>,
                                  IHandle<EventReaderSubscriptionMessage.CheckpointSuggested>,
                                  IHandle<EventReaderSubscriptionMessage.NotAuthorized>
    {
        private readonly ILogger _logger = LogManager.GetLoggerFor<IndexingWorker>();

        private readonly
            PublishSubscribeDispatcher
                <Guid, ReaderSubscriptionManagement.Subscribe,
                    ReaderSubscriptionManagement.ReaderSubscriptionManagementMessage, EventReaderSubscriptionMessage>

            _subscriptionDispatcher;

        private readonly IPrincipal _user;

        private readonly List<TaggedResolvedEvent> _batch = new List<TaggedResolvedEvent>();
        private readonly ITimeProvider _timeProvider;

        private Guid _subscriptionId;
        private CheckpointTag _lastReaderPosition;
        private CheckpointTag _fromPosition;
        private readonly Lucene _lucene;
        private bool _tickPending;
        private IPublisher _publisher;
        private readonly string _indexName;

        public IndexingReader(
            string indexName,
            IPublisher publisher,
            PublishSubscribeDispatcher
                <Guid, ReaderSubscriptionManagement.Subscribe,
                ReaderSubscriptionManagement.ReaderSubscriptionManagementMessage, EventReaderSubscriptionMessage>
                subscriptionDispatcher, ITimeProvider timeProvider, Lucene lucene)
        {
            if (subscriptionDispatcher == null) throw new ArgumentNullException("subscriptionDispatcher");
            _indexName = indexName;
            _publisher = publisher;
            _subscriptionDispatcher = subscriptionDispatcher;
            _timeProvider = timeProvider;
            _lucene = lucene;
            _logger.Info("Creating a goddamned IndexingReader");
        }

        public void Start()
        {
            var streamName = String.Format("$index-{0}", _indexName);
            var sourceDefinition = new SourceDefinitionBuilder();
            sourceDefinition.FromStream(streamName);
            sourceDefinition.AllEvents();

            // TODO: Read this from the index if we can
            // We can read this from the index now we're fls
            _fromPosition = CheckpointTag.FromStreamPosition(0, streamName, -1);
            var readerStrategy = ReaderStrategy.Create(0, sourceDefinition.Build(), _timeProvider, stopOnEof: true, runAs: SystemAccount.Principal);
            var readerOptions = new ReaderSubscriptionOptions(1024*1024, 1024, stopOnEof: false, stopAfterNEvents: null);
            _subscriptionId = _subscriptionDispatcher.PublishSubscribe(
                    new ReaderSubscriptionManagement.Subscribe(
                        Guid.NewGuid(), _fromPosition, readerStrategy, readerOptions), this);
            _logger.Info("Subscribing for indexing with subscription {0}", _subscriptionId);
        }


        public void EnsureTickPending()
        {
            if(_tickPending) return;
            _tickPending = true;
            _publisher.Publish(new IndexingMessage.Tick(Tick));
        }

        private void Tick()
        {
            _tickPending = false;
            this.Flush();
        }

        public void Stop()
        {
            this.Unsubscribe();
        }

        public void Handle(EventReaderSubscriptionMessage.CommittedEventReceived message)
        {
            _logger.Info("Sticking a message in my queue");
            _lastReaderPosition = message.CheckpointTag;
            _batch.Add(new TaggedResolvedEvent(message.Data, message.EventCategory, message.CheckpointTag));
            this.EnsureTickPending();
        }

        public void Handle(EventReaderSubscriptionMessage.CheckpointSuggested message)
        {
            _lastReaderPosition = message.CheckpointTag;
            Flush();
        }

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

        private void Unsubscribe()
        {
            _logger.Info ("Unsubscribing");
            _subscriptionDispatcher.Cancel(_subscriptionId);
        }

        private void Flush()
        {
            _logger.Info("Flushing");
            foreach(var @event in _batch)
            {
                _lucene.Write(@event.ResolvedEvent.EventType, @event.ResolvedEvent.Data);
            }
            _lucene.Flush(_indexName, 0);

            // publish commit point to indexing management stream
            _batch.Clear();
            _logger.Info("Flushed");
        }
    }
}

