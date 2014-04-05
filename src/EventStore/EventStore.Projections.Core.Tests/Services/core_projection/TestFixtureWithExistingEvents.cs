using System;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messaging;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.AwakeReaderService;
using EventStore.Projections.Core.Services.Management;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection
{

    public abstract class TestFixtureWithExistingEvents : EventStore.Core.Tests.Helpers.TestFixtureWithExistingEvents,
                                                          IHandle<ProjectionCoreServiceMessage.CoreTick>,
                                                          IHandle<ReaderCoreServiceMessage.ReaderTick>

    {
        protected
            ReaderSubscriptionDispatcher
            _subscriptionDispatcher;

        protected readonly ProjectionStateHandlerFactory _handlerFactory = new ProjectionStateHandlerFactory();
        private bool _ticksAreHandledImmediately;
        protected AwakeReaderService _awakeReaderService;

        protected override void Given1()
        {
            base.Given1();
            _ticksAreHandledImmediately = false;
        }

        [SetUp]
        public void SetUp()
        {
            _subscriptionDispatcher =
                new ReaderSubscriptionDispatcher
                    (_bus);
            _bus.Subscribe(
                _subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.CommittedEventReceived>());
            _bus.Subscribe(
                _subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.CheckpointSuggested>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.EofReached>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.PartitionEofReached>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.PartitionMeasured>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.PartitionDeleted>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.ProgressChanged>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.SubscriptionStarted>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.NotAuthorized>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.ReaderAssignedReader>());
            _bus.Subscribe<ProjectionCoreServiceMessage.CoreTick>(this);
            _bus.Subscribe<ReaderCoreServiceMessage.ReaderTick>(this);

            _awakeReaderService = new AwakeReaderService();
            _bus.Subscribe<StorageMessage.EventCommitted>(_awakeReaderService);
            _bus.Subscribe<StorageMessage.TfEofAtNonCommitRecord>(_awakeReaderService);
            _bus.Subscribe<AwakeReaderServiceMessage.SubscribeAwake>(_awakeReaderService);
            _bus.Subscribe<AwakeReaderServiceMessage.UnsubscribeAwake>(_awakeReaderService);
            _bus.Subscribe(new UnwrapEnvelopeHandler());
        }

        public void Handle(ProjectionCoreServiceMessage.CoreTick message)
        {
            if (_ticksAreHandledImmediately)
                message.Action();
        }

        public void Handle(ReaderCoreServiceMessage.ReaderTick message)
        {
            if (_ticksAreHandledImmediately)
                message.Action();
        }

        protected void TicksAreHandledImmediately()
        {
            _ticksAreHandledImmediately = true;
        }

        protected ClientMessage.WriteEvents CreateWriteEvent(
            string streamId, string eventType, string data, string metadata = null, bool isJson = true,
            Guid? correlationId = null)
        {
            return new ClientMessage.WriteEvents(
                Guid.NewGuid(), correlationId ?? Guid.NewGuid(), new PublishEnvelope(GetInputQueue()), false, streamId,
                ExpectedVersion.Any, new Event(Guid.NewGuid(), eventType, isJson, data, metadata), null);
        }
    }
}
