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

using System.Collections.Generic;
using System;
using System.Linq;
using EventStore.Common.Options;
using EventStore.Core;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.Util;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.EventReaders.Feeds;
using EventStore.Projections.Core.Messaging;
using EventStore.Projections.Core.Services.AwakeReaderService;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Indexing
{
    public class IndexingSystem : ISubsystem
    {
        private EventStore.Projections.Core.Indexing.Indexing _indexing;
        private readonly RunProjections _runProjections;
        private readonly string _indexPath;

        public IndexingSystem(string indexPath, RunProjections runProjections)
        {
            _runProjections = runProjections;
            _indexPath = indexPath;
        }

        public void Register(
            TFChunkDb db, QueuedHandler mainQueue, ISubscriber mainBus, TimerService timerService,
            ITimeProvider timeProvider, IHttpForwarder httpForwarder, HttpService[] httpServices, IPublisher networkSendService)
        {
            _indexing = new EventStore.Projections.Core.Indexing.Indexing(_indexPath,
                db, mainQueue, mainBus, timerService, timeProvider, httpForwarder, httpServices, networkSendService,
                runProjections: _runProjections);
        }

        public void Start()
        {
            _indexing.Start();
        }

        public void Stop()
        {
           _indexing.Stop();
        }
    }

    public sealed class Indexing
    {
        public const int VERSION = 3;

        private QueuedHandler _indexQueue;
        private IndexingWorker _worker;

        private QueuedHandler _webQueue;
        private IndexingController _web;
        private Lucene _lucene;
        private readonly string _indexPath;

        public Indexing(
            string indexPath,
            TFChunkDb db, QueuedHandler mainQueue, ISubscriber mainBus, TimerService timerService, ITimeProvider timeProvider,
            IHttpForwarder httpForwarder, HttpService[] httpServices, IPublisher networkSendQueue, RunProjections runProjections)
        {
          _indexPath = indexPath;
            SetupMessaging(
                db, mainQueue, mainBus, timerService, timeProvider, httpForwarder, httpServices, networkSendQueue,
                runProjections);
        }

        private void SetupMessaging(
            TFChunkDb db, QueuedHandler mainQueue, ISubscriber mainBus, TimerService timerService, ITimeProvider timeProvider,
            IHttpForwarder httpForwarder, HttpService[] httpServices, IPublisher networkSendQueue, RunProjections runProjections)
        {
            var webInput = new InMemoryBus("Indexing web input bus");
            _webQueue = new QueuedHandler(webInput, "Web queue");
            _web = new IndexingController(httpForwarder, _webQueue, networkSendQueue);
            foreach (var httpService in httpServices)
            {
                httpService.SetupController(_web);
            }

            // Might not need this level of indirection if we only have one handler
            var indexInputBus = new InMemoryBus("bus");
            _indexQueue = new QueuedHandler(indexInputBus, "Indexing Core");

            // Only one worker to process all the things
            // TODO: Consider disposal
            _lucene = Lucene.Create(_indexPath);
            _worker = new IndexingWorker(db, _indexQueue, timeProvider, runProjections, _lucene);
            _worker.SetupMessaging(indexInputBus);

            // Need these for subscriptions
            var forwarder = new RequestResponseQueueForwarder(inputQueue: _indexQueue, externalRequestQueue: mainQueue);
            _worker.CoreOutput.Subscribe<ClientMessage.ReadEvent>(forwarder);
            _worker.CoreOutput.Subscribe<ClientMessage.ReadStreamEventsBackward>(forwarder);
            _worker.CoreOutput.Subscribe<ClientMessage.ReadStreamEventsForward>(forwarder);
            _worker.CoreOutput.Subscribe<ClientMessage.ReadAllEventsForward>(forwarder);
            _worker.CoreOutput.Subscribe<ClientMessage.WriteEvents>(forwarder);
            _worker.CoreOutput.Subscribe(Forwarder.Create<AwakeReaderServiceMessage.SubscribeAwake>(mainQueue));
            _worker.CoreOutput.Subscribe(Forwarder.Create<AwakeReaderServiceMessage.UnsubscribeAwake>(mainQueue));

            // Think something needs this, not sure.
            _worker.CoreOutput.Subscribe<TimerMessage.Schedule>(timerService);

            // Need this one because we wait for system start-up
            mainBus.Subscribe(Forwarder.Create<SystemMessage.StateChangeMessage>(_indexQueue));

            // forward all to self
            _worker.CoreOutput.Subscribe(Forwarder.Create<Message>(_indexQueue));

            indexInputBus.Subscribe(new UnwrapEnvelopeHandler());
        }

        public void Start()
        {
           _indexQueue.Start();
            _webQueue.Start();
        }

        public void Stop()
        {
            _webQueue.Stop();
            _indexQueue.Stop();
        }
    }

}
