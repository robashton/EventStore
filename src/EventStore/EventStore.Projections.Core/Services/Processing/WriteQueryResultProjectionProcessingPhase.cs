using System;
using System.Collections.Generic;
using System.Linq;

namespace EventStore.Projections.Core.Services.Processing
{
    public sealed class WriteQueryResultProjectionProcessingPhase : WriteQueryResultProjectionProcessingPhaseBase
    {
        public WriteQueryResultProjectionProcessingPhase(
            int phase, string resultStream, ICoreProjectionForProcessingPhase coreProjection,
            PartitionStateCache stateCache, ICoreProjectionCheckpointManager checkpointManager,
            IEmittedEventWriter emittedEventWriter)
            : base(phase, resultStream, coreProjection, stateCache, checkpointManager, emittedEventWriter)
        {
        }

        protected override IEnumerable<EmittedEventEnvelope> WriteResults(CheckpointTag phaseCheckpointTag)
        {
            var items = _stateCache.Enumerate();
            EmittedStream.WriterConfiguration.StreamMetadata streamMetadata = null;
            return from item in items
                let partitionState = item.Item2
                select
                    new EmittedEventEnvelope(
                        new EmittedDataEvent(
                            _resultStream, Guid.NewGuid(), "Result", true, partitionState.Result, null, phaseCheckpointTag,
                            null), streamMetadata);
        }
    }
}
