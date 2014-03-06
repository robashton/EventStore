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

using EventStore.Common.Options;
using EventStore.Core.Bus;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.Util;
using EventStore.Projections.Core.EventReaders.Feeds;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.ParallelQueryProcessingMessages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Indexing
{
    public class IndexingWorker : IHandle<IndexingMessage.Start>
    {
		private readonly ILogger _logger = LogManager.GetLoggerFor<IndexingWorker>();
        private readonly RunProjections _runProjections;
        private readonly InMemoryBus _coreOutput;
        private readonly EventReaderCoreService _eventReaderCoreService;
        private readonly ReaderSubscriptionDispatcher _subscriptionDispatcher;
        private readonly IODispatcher _ioDispatcher;
		private IndexingReader _reader;
		private readonly ITimeProvider _timeProvider;
		private readonly Lucene _lucene;

        public IndexingWorker(TFChunkDb db, QueuedHandler inputQueue, ITimeProvider timeProvider, RunProjections runProjections, Lucene lucene)
        {
            _runProjections = runProjections;
            Ensure.NotNull(db, "db");

            _coreOutput = new InMemoryBus("Indexing Output");
			_timeProvider = timeProvider;

            IPublisher publisher = CoreOutput;
            _subscriptionDispatcher = new ReaderSubscriptionDispatcher(publisher);
            _ioDispatcher = new IODispatcher(publisher, new PublishEnvelope(inputQueue));
            _eventReaderCoreService = new EventReaderCoreService(
                publisher, _ioDispatcher, 10, db.Config.WriterCheckpoint, runHeadingReader: runProjections >= RunProjections.System);
			_lucene = lucene;
			_reader = new IndexingReader(CoreOutput, _subscriptionDispatcher, _timeProvider, _lucene);
        }

        public InMemoryBus CoreOutput
        {
            get { return _coreOutput; }
        }

		public void Handle(IndexingMessage.Start msg) {
			_logger.Info("Starting up indexing reader");
			_reader.Start();
		}

        public void SetupMessaging(IBus coreInputBus)
        {
			coreInputBus.Subscribe(this);

			// NOTE: I don't actually know if all of these are needed, but they seemed like likely suspects
            coreInputBus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.CheckpointSuggested>());
            coreInputBus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.CommittedEventReceived>());
            coreInputBus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.EofReached>());
            coreInputBus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.PartitionEofReached>());
            coreInputBus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.PartitionMeasured>());
            coreInputBus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.PartitionDeleted>());
            coreInputBus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.ProgressChanged>());
            coreInputBus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.SubscriptionStarted>());
            coreInputBus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.NotAuthorized>());
            coreInputBus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.ReaderAssignedReader>());

			coreInputBus.Subscribe<ClientMessage.ReadStreamEventsForwardCompleted>(_ioDispatcher.ForwardReader);
			coreInputBus.Subscribe<ClientMessage.ReadStreamEventsBackwardCompleted>(_ioDispatcher.BackwardReader);
			coreInputBus.Subscribe<ClientMessage.WriteEventsCompleted>(_ioDispatcher.Writer);
			coreInputBus.Subscribe<ClientMessage.DeleteStreamCompleted>(_ioDispatcher.StreamDeleter);
			coreInputBus.Subscribe<IODispatcherDelayedMessage>(_ioDispatcher);

            coreInputBus.Subscribe<ReaderCoreServiceMessage.ReaderTick>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderCoreServiceMessage.StartReader>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderCoreServiceMessage.StopReader>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionManagement.Subscribe>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionManagement.Unsubscribe>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionManagement.Pause>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionManagement.Resume>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionManagement.SpoolStreamReading>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionManagement.CompleteSpooledStreamReading>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionMessage.CommittedEventDistributed>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionMessage.EventReaderIdle>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionMessage.EventReaderStarting>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionMessage.EventReaderEof>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionMessage.EventReaderPartitionEof>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionMessage.EventReaderPartitionDeleted>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionMessage.EventReaderPartitionMeasured>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionMessage.EventReaderNotAuthorized>(_eventReaderCoreService);

			coreInputBus.Subscribe<IndexingMessage.Tick>(_reader);

            //NOTE: message forwarding is set up outside (for Read/Write events)
        }
    }
}
