using System;
using EventStore.Core.Bus;
using EventStore.Projections.Core.Messages.ParallelQueryProcessingMessages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class SlaveResultWriter : IResultWriter
    {
        private readonly IPublisher _resultsPublisher;
        private readonly Guid _masterCoreProjectionId;

        public SlaveResultWriter(IPublisher resultsPublisher, Guid masterCoreProjectionId)
        {
            if (resultsPublisher == null) throw new ArgumentNullException("resultsPublisher");

            _resultsPublisher = resultsPublisher;
            _masterCoreProjectionId = masterCoreProjectionId;
        }

        public void WriteEofResult(
            Guid subscriptionId, string partition, string resultBody, CheckpointTag causedBy, Guid causedByGuid,
            string correlationId)
        {
            _resultsPublisher.Publish(
                new PartitionProcessingResult(
                    _masterCoreProjectionId, subscriptionId, partition, causedByGuid, causedBy, resultBody));
        }

        public void WritePartitionMeasured(Guid subscriptionId, string partition, int size)
        {
            _resultsPublisher.Publish(new PartitionMeasured(_masterCoreProjectionId, subscriptionId, partition, size));
        }

        public void WriteRunningResult(EventProcessedResult result)
        {
            // intentionally does nothing            
        }

        public void AccountPartition(EventProcessedResult result)
        {
            // intentionally does nothing            
        }

        public void EventsEmitted(
            EmittedEventEnvelope[] scheduledWrites, Guid causedBy, string correlationId)
        {
            throw new NotSupportedException();
        }

        public void WriteProgress(Guid subscriptionId, float progress)
        {
            _resultsPublisher.Publish(
                new PartitionProcessingProgress(_masterCoreProjectionId, subscriptionId, progress));
        }
    }
}
