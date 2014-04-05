using System;
using System.Linq;
using EventStore.Common.Options;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Tests.Services.TimeService;
using EventStore.Core.Util;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Management;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager
{
    [TestFixture]
    public class when_starting_the_projection_manager_with_existing_partially_created_projection :
        TestFixtureWithExistingEvents
    {
        private ProjectionManager _manager;
        private new ITimeProvider _timeProvider;

        protected override void Given()
        {
            ExistingEvent("$projections-$all", "$ProjectionCreated", null, "projection1");
            NoStream("$projections-projection1");
        }


        [SetUp]
        public void setup()
        {
            _timeProvider = new FakeTimeProvider();
            _manager = new ProjectionManager(_bus, _bus, new IPublisher[] { _bus }, _timeProvider, RunProjections.All);
            _bus.Subscribe<ClientMessage.WriteEventsCompleted>(_manager);
            _bus.Subscribe<ClientMessage.ReadStreamEventsBackwardCompleted>(_manager);
            _manager.Handle(new SystemMessage.BecomeMaster(Guid.NewGuid()));
        }

        [TearDown]
        public void TearDown()
        {
            _manager.Dispose();
        }

        [Test]
        public void projection_status_can_be_retrieved()
        {
            _manager.Handle(
                new ProjectionManagementMessage.GetStatistics(new PublishEnvelope(_bus), null, "projection1", true));
            Assert.IsNotNull(
                _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>().SingleOrDefault(
                    v => v.Projections[0].Name == "projection1"));
        }

        [Test]
        public void projection_status_is_creating()
        {
            _manager.Handle(
                new ProjectionManagementMessage.GetStatistics(new PublishEnvelope(_bus), null, "projection1", true));
            Assert.AreEqual(
                ManagedProjectionState.Creating,
                _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>().SingleOrDefault(
                    v => v.Projections[0].Name == "projection1").Projections[0].MasterStatus);
        }
    }
}
