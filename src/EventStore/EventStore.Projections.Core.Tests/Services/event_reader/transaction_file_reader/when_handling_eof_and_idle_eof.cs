using System;
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

namespace EventStore.Projections.Core.Tests.Services.event_reader.transaction_file_reader
{
    [TestFixture]
    public class when_handling_eof_and_idle_eof : TestFixtureWithExistingEvents
    {
        private TransactionFileEventReader _edp;
        private Guid _distibutionPointCorrelationId;
        private Guid _firstEventId;
        private Guid _secondEventId;

        protected override void Given()
        {
            TicksAreHandledImmediately();
        }

        private FakeTimeProvider _fakeTimeProvider;

        [SetUp]
        public new void When()
        {

            _distibutionPointCorrelationId = Guid.NewGuid();
            _fakeTimeProvider = new FakeTimeProvider();
            _edp = new TransactionFileEventReader(
                _ioDispatcher, _bus, _distibutionPointCorrelationId, null, new TFPos(100, 50), _fakeTimeProvider,
                deliverEndOfTFPosition: false);
            _edp.Resume();
            _firstEventId = Guid.NewGuid();
            _secondEventId = Guid.NewGuid();
            _edp.Handle(
                new ClientMessage.ReadAllEventsForwardCompleted(
                    _distibutionPointCorrelationId, ReadAllResult.Success, null,
                    new[]
                    {
                        new EventStore.Core.Data.ResolvedEvent(
                            new EventRecord(
                                1, 50, Guid.NewGuid(), _firstEventId, 50, 0, "a", ExpectedVersion.Any,
                                _fakeTimeProvider.Now,
                                PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                                "event_type1", new byte[] {1}, new byte[] {2}), null, 100),
                        new EventStore.Core.Data.ResolvedEvent(
                            new EventRecord(
                                2, 150, Guid.NewGuid(), _secondEventId, 150, 0, "b", ExpectedVersion.Any,
                                _fakeTimeProvider.Now,
                                PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                                "event_type1", new byte[] {1}, new byte[] {2}), null, 200),
                    }, null, false, 100,
                    new TFPos(200, 150), new TFPos(500, -1), new TFPos(100, 50), 500));

            _edp.Handle(
                new ClientMessage.ReadAllEventsForwardCompleted(
                    _distibutionPointCorrelationId, ReadAllResult.Success, null,
                    new EventStore.Core.Data.ResolvedEvent[0], null, false, 100, new TFPos(), new TFPos(), new TFPos(), 500));
            _fakeTimeProvider.AddTime(TimeSpan.FromMilliseconds(500));
            _edp.Handle(
                new ClientMessage.ReadAllEventsForwardCompleted(
                    _distibutionPointCorrelationId, ReadAllResult.Success, null,
                    new EventStore.Core.Data.ResolvedEvent[0], null, false, 100, new TFPos(), new TFPos(), new TFPos(), 500));
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
