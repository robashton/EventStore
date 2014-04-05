using System;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Tests.Services.TimeService;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Management;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.managed_projection
{
    [TestFixture]
    public class when_loading_a_managed_projection_state : TestFixtureWithExistingEvents
    {
        private new ITimeProvider _timeProvider;

        private ManagedProjection _mp;

        protected override void Given()
        {
            _timeProvider = new FakeTimeProvider();
            _mp = new ManagedProjection(
                _bus, Guid.NewGuid(), 1, "name", true, null, _writeDispatcher, _readDispatcher, _bus, _bus, _handlerFactory,
                _timeProvider);
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_handler_type_throws_argument_null_exception()
        {
            ProjectionManagementMessage.Post message = new ProjectionManagementMessage.Post(
                new NoopEnvelope(), ProjectionMode.OneTime, "name", ProjectionManagementMessage.RunAs.Anonymous,
                (string)null, @"log(1);", enabled: true, checkpointsEnabled: false, emitEnabled: false);
            _mp.InitializeNew(() => { }, new ManagedProjection.PersistedState
                {
                    Enabled = message.Enabled,
                    HandlerType = message.HandlerType,
                    Query = message.Query,
                    Mode = message.Mode,
                    EmitEnabled = message.EmitEnabled,
                    CheckpointsDisabled = !message.CheckpointsEnabled,
                    Epoch = -1,
                    Version = -1,
                    RunAs = message.EnableRunAs ? ManagedProjection.SerializePrincipal(message.RunAs) : null,
                });
        }

        [Test, ExpectedException(typeof (ArgumentException))]
        public void empty_handler_type_throws_argument_null_exception()
        {
            ProjectionManagementMessage.Post message = new ProjectionManagementMessage.Post(
                new NoopEnvelope(), ProjectionMode.OneTime, "name", ProjectionManagementMessage.RunAs.Anonymous, "",
                @"log(1);", enabled: true, checkpointsEnabled: false, emitEnabled: false);
            _mp.InitializeNew(() => { }, new ManagedProjection.PersistedState
                {
                    Enabled = message.Enabled,
                    HandlerType = message.HandlerType,
                    Query = message.Query,
                    Mode = message.Mode,
                    EmitEnabled = message.EmitEnabled,
                    CheckpointsDisabled = !message.CheckpointsEnabled,
                    Epoch = -1,
                    Version = -1,
                    RunAs = message.EnableRunAs ? ManagedProjection.SerializePrincipal(message.RunAs) : null,
                });
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_query_throws_argument_null_exception()
        {
            ProjectionManagementMessage.Post message = new ProjectionManagementMessage.Post(
                new NoopEnvelope(), ProjectionMode.OneTime, "name", ProjectionManagementMessage.RunAs.Anonymous,
                "JS", query: null, enabled: true, checkpointsEnabled: false, emitEnabled: false);
            _mp.InitializeNew(() => { }, new ManagedProjection.PersistedState
                {
                    Enabled = message.Enabled,
                    HandlerType = message.HandlerType,
                    Query = message.Query,
                    Mode = message.Mode,
                    EmitEnabled = message.EmitEnabled,
                    CheckpointsDisabled = !message.CheckpointsEnabled,
                    Epoch = -1,
                    Version = -1,
                    RunAs = message.EnableRunAs ? ManagedProjection.SerializePrincipal(message.RunAs) : null,
                });
        }
    }
}
