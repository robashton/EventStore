using System;
using System.IO;
using System.Security.Principal;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Core.Services.TimerService;

namespace EventStore.Projections.Core.Services.Processing
{
    public class ParallelQueryMasterReaderStrategy : IReaderStrategy
    {
        private readonly IPrincipal _runAs;
        private readonly ITimeProvider _timeProvider;
        private readonly string _catalogStream;
        private readonly EventFilter _eventFilter;
        private readonly PositionTagger _positionTagger;

        public ParallelQueryMasterReaderStrategy(
            int phase, IPrincipal runAs, ITimeProvider timeProvider, string catalogStream)
        {
            _runAs = runAs;
            _timeProvider = timeProvider;
            _catalogStream = catalogStream;
            _eventFilter = new StreamEventFilter(catalogStream, true, null);
            _positionTagger = new CatalogStreamPositionTagger(phase, catalogStream);
        }

        public bool IsReadingOrderRepeatable
        {
            get { return true; }
        }

        public EventFilter EventFilter
        {
            get { return _eventFilter; }
        }

        public PositionTagger PositionTagger
        {
            get { return _positionTagger; }
        }

        public IReaderSubscription CreateReaderSubscription(
            IPublisher publisher, CheckpointTag fromCheckpointTag, Guid subscriptionId,
            ReaderSubscriptionOptions readerSubscriptionOptions)
        {
            return new ReaderSubscription(
                publisher, subscriptionId, fromCheckpointTag, this,
                readerSubscriptionOptions.CheckpointUnhandledBytesThreshold,
                readerSubscriptionOptions.CheckpointProcessedEventsThreshold, readerSubscriptionOptions.StopOnEof,
                readerSubscriptionOptions.StopAfterNEvents);
        }

        public IEventReader CreatePausedEventReader(
            Guid eventReaderId, IPublisher publisher, IODispatcher ioDispatcher, CheckpointTag checkpointTag,
            bool stopOnEof, int? stopAfterNEvents)
        {
            return new StreamEventReader(
                ioDispatcher, publisher, eventReaderId, _runAs, _catalogStream, checkpointTag.CatalogPosition + 1,
                _timeProvider, resolveLinkTos: true, stopOnEof: stopOnEof, stopAfterNEvents: stopAfterNEvents,
                produceStreamDeletes: false);
        }
    }
}
