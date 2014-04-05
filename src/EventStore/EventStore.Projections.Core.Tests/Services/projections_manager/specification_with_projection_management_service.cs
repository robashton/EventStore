using EventStore.Common.Options;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Helpers;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.AwakeReaderService;
using EventStore.Projections.Core.Services.Management;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager
{
    public abstract class specification_with_projection_management_service : TestFixtureWithExistingEvents
    {
        protected ProjectionManager _manager;
        private bool _initializeSystemProjections;
        protected AwakeReaderService _awakeReaderService;


        protected override void Given1()
        {
            base.Given1();
            _initializeSystemProjections = GivenInitializeSystemProjections();
            if (!_initializeSystemProjections)
            {
                ExistingEvent("$projections-$all", "$ProjectionsInitialized", "", "");
            }
        }

        protected virtual bool GivenInitializeSystemProjections()
        {
            return false;
        }

        protected override ManualQueue GiveInputQueue()
        {
            return new ManualQueue(_bus, _timeProvider);
        }

        [SetUp]
        public void Setup()
        {
            //TODO: this became an integration test - proper ProjectionCoreService and ProjectionManager testing is required as well
            _bus.Subscribe(_consumer);

            _manager = new ProjectionManager(
                GetInputQueue(), GetInputQueue(), GivenCoreQueues(), _timeProvider, RunProjections.All,
                _initializeSystemProjections);

            IPublisher inputQueue = GetInputQueue();
            IPublisher publisher = GetInputQueue();
            var ioDispatcher = new IODispatcher(publisher, new PublishEnvelope(inputQueue));
            _bus.Subscribe<ProjectionManagementMessage.Internal.CleanupExpired>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.Internal.Deleted>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.Started>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.Stopped>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.Prepared>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.Faulted>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.StateReport>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.ResultReport>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.StatisticsReport>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.SlaveProjectionReaderAssigned>(_manager);
            
            _bus.Subscribe<ProjectionManagementMessage.Post>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.UpdateQuery>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.GetQuery>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.Delete>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.GetStatistics>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.GetState>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.GetResult>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.Disable>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.Enable>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.Abort>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.SetRunAs>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.Reset>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.StartSlaveProjections>(_manager);
            _bus.Subscribe<ClientMessage.WriteEventsCompleted>(_manager);
            _bus.Subscribe<ClientMessage.ReadStreamEventsBackwardCompleted>(_manager);
            _bus.Subscribe<ClientMessage.WriteEventsCompleted>(_manager);
            _bus.Subscribe<SystemMessage.StateChangeMessage>(_manager);

            _bus.Subscribe<ClientMessage.ReadStreamEventsForwardCompleted>(ioDispatcher.ForwardReader);
            _bus.Subscribe<ClientMessage.ReadStreamEventsBackwardCompleted>(ioDispatcher.BackwardReader);
            _bus.Subscribe<ClientMessage.WriteEventsCompleted>(ioDispatcher.Writer);
            _bus.Subscribe<ClientMessage.DeleteStreamCompleted>(ioDispatcher.StreamDeleter);
            _bus.Subscribe<IODispatcherDelayedMessage>(ioDispatcher);

            _awakeReaderService = new AwakeReaderService();
            _bus.Subscribe<StorageMessage.EventCommitted>(_awakeReaderService);
            _bus.Subscribe<StorageMessage.TfEofAtNonCommitRecord>(_awakeReaderService);
            _bus.Subscribe<AwakeReaderServiceMessage.SubscribeAwake>(_awakeReaderService);
            _bus.Subscribe<AwakeReaderServiceMessage.UnsubscribeAwake>(_awakeReaderService);


            Given();
            WhenLoop();
        }

        protected abstract IPublisher[] GivenCoreQueues();
    }
}
