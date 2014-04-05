using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests.Services.TimeService;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;
using ReadStreamResult = EventStore.Core.Data.ReadStreamResult;
using ResolvedEvent = EventStore.Core.Data.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.Services.event_reader.stream_reader
{
    [TestFixture]
    public class when_handling_eof_and_idle_eof : TestFixtureWithExistingEvents
    {
        private StreamEventReader _edp;
        //private Guid _publishWithCorrelationId;
        private Guid _distibutionPointCorrelationId;
        private Guid _firstEventId;
        private Guid _secondEventId;
        private FakeTimeProvider _fakeTimeProvider;

        protected override void Given()
        {
            TicksAreHandledImmediately();
        }

        [SetUp]
        public new void When()
        {
            //_publishWithCorrelationId = Guid.NewGuid();
            _distibutionPointCorrelationId = Guid.NewGuid();
            _fakeTimeProvider = new FakeTimeProvider();
            _edp = new StreamEventReader(
                _ioDispatcher, _bus, _distibutionPointCorrelationId, null, "stream", 10, _fakeTimeProvider, false,
                produceStreamDeletes: false);
            _edp.Resume();
            _firstEventId = Guid.NewGuid();
            _secondEventId = Guid.NewGuid();
            _edp.Handle(
                new ClientMessage.ReadStreamEventsForwardCompleted(
                    _distibutionPointCorrelationId, "stream", 100, 100, ReadStreamResult.Success, 
                    new[]
                        {
                            new ResolvedEvent(
                        new EventRecord(
                            10, 50, Guid.NewGuid(), _firstEventId, 50, 0, "stream", ExpectedVersion.Any, DateTime.UtcNow,
                            PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                            "event_type1", new byte[] {1}, new byte[] {2}), null),
                            new ResolvedEvent(
                        new EventRecord(
                            11, 100, Guid.NewGuid(), _secondEventId, 100, 0, "stream", ExpectedVersion.Any,
                            DateTime.UtcNow,
                            PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                            "event_type2", new byte[] {3}, new byte[] {4}), null)
                        }, null, false, "", 12, 11, true, 200));
            _edp.Handle(
                new ClientMessage.ReadStreamEventsForwardCompleted(
                    _distibutionPointCorrelationId, "stream", 100, 100, ReadStreamResult.Success,
                    new ResolvedEvent[] { }, null, false, "", 12, 11, true, 400));
            _fakeTimeProvider.AddTime(TimeSpan.FromMilliseconds(500));
            _edp.Handle(
                new ClientMessage.ReadStreamEventsForwardCompleted(
                    _distibutionPointCorrelationId, "stream", 100, 100, ReadStreamResult.Success,
                    new ResolvedEvent[] { }, null, false, "", 12, 11, true, 400));
        }

        [Test]
        public void publishes_event_distribution_idle_messages()
        {
            Assert.AreEqual(
                2, _consumer.HandledMessages.OfType<ReaderSubscriptionMessage.EventReaderIdle>().Count());
            var first =
                _consumer.HandledMessages.OfType<ReaderSubscriptionMessage.EventReaderIdle>().First();
            var second =
                _consumer.HandledMessages.OfType<ReaderSubscriptionMessage.EventReaderIdle>()
                         .Skip(1)
                         .First();

            Assert.AreEqual(first.CorrelationId, _distibutionPointCorrelationId);
            Assert.AreEqual(second.CorrelationId, _distibutionPointCorrelationId);

            Assert.AreEqual(TimeSpan.FromMilliseconds(500), second.IdleTimestampUtc - first.IdleTimestampUtc);
        }

    }
}
