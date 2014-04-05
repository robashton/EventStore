using System;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class QueryProcessingStrategy : DefaultProjectionProcessingStrategy
    {
        public QueryProcessingStrategy(
            string name, ProjectionVersion projectionVersion, IProjectionStateHandler stateHandler,
            ProjectionConfig projectionConfig, IQuerySources sourceDefinition, ILogger logger,
            ReaderSubscriptionDispatcher subscriptionDispatcher)
            : base(
                name, projectionVersion, stateHandler, projectionConfig, sourceDefinition, logger,
                subscriptionDispatcher)
        {
        }

        public override bool GetStopOnEof()
        {
            return true;
        }

        public override bool GetUseCheckpoints()
        {
            return false;
        }

        public override bool GetProducesRunningResults()
        {
            return !_sourceDefinition.DefinesFold;
        }

        public override bool GetIsSlaveProjection()
        {
            return false;
        }

        public override SlaveProjectionDefinitions GetSlaveProjections()
        {
            return null;
        }

        protected override IProjectionProcessingPhase[] CreateProjectionProcessingPhases(
            IPublisher publisher, Guid projectionCorrelationId, ProjectionNamesBuilder namingBuilder,
            PartitionStateCache partitionStateCache, CoreProjection coreProjection, IODispatcher ioDispatcher,
            IProjectionProcessingPhase firstPhase)
        {

            var coreProjectionCheckpointWriter =
                new CoreProjectionCheckpointWriter(
                    namingBuilder.MakeCheckpointStreamName(), ioDispatcher, _projectionVersion, _name);
            var checkpointManager2 = new DefaultCheckpointManager(
                publisher, projectionCorrelationId, _projectionVersion, _projectionConfig.RunAs, ioDispatcher,
                _projectionConfig, _name, new PhasePositionTagger(1), namingBuilder, GetUseCheckpoints(), false,
                _sourceDefinition.DefinesFold, coreProjectionCheckpointWriter);

            IProjectionProcessingPhase writeResultsPhase;
            if (GetProducesRunningResults()
                || !string.IsNullOrEmpty(_sourceDefinition.CatalogStream) && _sourceDefinition.ByStreams)
                writeResultsPhase = new WriteQueryEofProjectionProcessingPhase(
                    1, namingBuilder.GetResultStreamName(), coreProjection, partitionStateCache, checkpointManager2,
                    checkpointManager2);
            else
                writeResultsPhase = new WriteQueryResultProjectionProcessingPhase(
                    1, namingBuilder.GetResultStreamName(), coreProjection, partitionStateCache, checkpointManager2,
                    checkpointManager2);

            return new[] {firstPhase, writeResultsPhase};
        }

        protected override IResultEventEmitter CreateFirstPhaseResultEmitter(ProjectionNamesBuilder namingBuilder)
        {
            return new ResultEventEmitter(namingBuilder);
        }

    }
}
