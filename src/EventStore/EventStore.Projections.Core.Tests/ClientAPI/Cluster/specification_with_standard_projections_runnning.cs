﻿// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Common.Log;
using EventStore.ClientAPI.SystemData;
using EventStore.Common.Options;
using EventStore.Core;
using EventStore.Core.Bus;
using EventStore.Core.Tests;
using EventStore.Core.Tests.Helpers;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using ResolvedEvent = EventStore.ClientAPI.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.ClientAPI.Cluster
{
    [Category("ClientAPI")]
    public class specification_with_standard_projections_runnning : SpecificationWithDirectoryPerTestFixture
    {
        protected MiniClusterNode[] _nodes = new MiniClusterNode[3];
        protected Endpoints[] _nodeEndpoints = new Endpoints[3];
        protected IEventStoreConnection _conn;
        protected ProjectionsSubsystem _projections;
        protected UserCredentials _admin = new UserCredentials("admin", "changeit");
        protected ProjectionsManager _manager;

        protected class Endpoints
        {
            public readonly IPEndPoint InternalTcp;
            public readonly IPEndPoint InternalTcpSec;
            public readonly IPEndPoint InternalHttp;
            public readonly IPEndPoint ExternalTcp;
            public readonly IPEndPoint ExternalTcpSec;
            public readonly IPEndPoint ExternalHttp;

            public Endpoints(
                int internalTcp, int internalTcpSec, int internalHttp, int externalTcp,
                int externalTcpSec, int externalHttp)
            {
                InternalTcp = new IPEndPoint(IPAddress.Loopback, internalTcp);
                InternalTcpSec = new IPEndPoint(IPAddress.Loopback, internalTcpSec);
                InternalHttp = new IPEndPoint(IPAddress.Loopback, internalHttp);
                ExternalTcp = new IPEndPoint(IPAddress.Loopback, externalTcp);
                ExternalTcpSec = new IPEndPoint(IPAddress.Loopback, externalTcpSec);
                ExternalHttp = new IPEndPoint(IPAddress.Loopback, externalHttp);
            }
        }

        [TestFixtureSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();
#if DEBUG
            QueueStatsCollector.InitializeIdleDetection();
#else 
            throw new NotSupportedException("These tests require DEBUG conditional");
#endif

            _nodeEndpoints[0] = new Endpoints(
                PortsHelper.GetAvailablePort(IPAddress.Loopback), PortsHelper.GetAvailablePort(IPAddress.Loopback),
                PortsHelper.GetAvailablePort(IPAddress.Loopback), PortsHelper.GetAvailablePort(IPAddress.Loopback),
                PortsHelper.GetAvailablePort(IPAddress.Loopback), PortsHelper.GetAvailablePort(IPAddress.Loopback));
            _nodeEndpoints[1] = new Endpoints(
                PortsHelper.GetAvailablePort(IPAddress.Loopback), PortsHelper.GetAvailablePort(IPAddress.Loopback),
                PortsHelper.GetAvailablePort(IPAddress.Loopback), PortsHelper.GetAvailablePort(IPAddress.Loopback),
                PortsHelper.GetAvailablePort(IPAddress.Loopback), PortsHelper.GetAvailablePort(IPAddress.Loopback));
            _nodeEndpoints[2] = new Endpoints(
                PortsHelper.GetAvailablePort(IPAddress.Loopback), PortsHelper.GetAvailablePort(IPAddress.Loopback),
                PortsHelper.GetAvailablePort(IPAddress.Loopback), PortsHelper.GetAvailablePort(IPAddress.Loopback),
                PortsHelper.GetAvailablePort(IPAddress.Loopback), PortsHelper.GetAvailablePort(IPAddress.Loopback));

            PortsHelper.GetAvailablePort(IPAddress.Loopback);

            _nodes[0] = CreateNode(0, 
                _nodeEndpoints[0], new IPEndPoint[] {_nodeEndpoints[1].InternalHttp, _nodeEndpoints[2].InternalHttp});
            _nodes[1] = CreateNode(1, 
                _nodeEndpoints[1], new IPEndPoint[] { _nodeEndpoints[0].InternalHttp, _nodeEndpoints[2].InternalHttp });
            
            _nodes[2] = CreateNode(2, 
                _nodeEndpoints[2], new IPEndPoint[] { _nodeEndpoints[0].InternalHttp, _nodeEndpoints[1].InternalHttp });
            

            _nodes[0].Start();
            _nodes[1].Start();
            _nodes[2].Start();

            WaitHandle.WaitAll(new[] { _nodes[0].StartedEvent, _nodes[1].StartedEvent, _nodes[2].StartedEvent });
            QueueStatsCollector.WaitIdle();
            _conn = EventStoreConnection.Create(_nodes[0].ExternalTcpEndPoint);
            _conn.Connect();

            _manager = new ProjectionsManager(new ConsoleLogger(), _nodes[0].ExternalHttpEndPoint);
            if (GivenStandardProjectionsRunning())
                EnableStandardProjections();
            QueueStatsCollector.WaitIdle();
            Given();
            When();
        }

        private MiniClusterNode CreateNode(int index, Endpoints endpoints, IPEndPoint[] gossipSeeds)
        {
            _projections = new ProjectionsSubsystem(1, runProjections: RunProjections.All);
            var node = new MiniClusterNode(
                PathName, index, endpoints.InternalTcp, endpoints.InternalTcpSec, endpoints.InternalHttp, endpoints.ExternalTcp,
                endpoints.ExternalTcpSec, endpoints.ExternalHttp, skipInitializeStandardUsersCheck: false,
                subsystems: new ISubsystem[] {_projections}, gossipSeeds: gossipSeeds);
            WaitIdle();
            return node;
        }

        [TearDown]
        public void PostTestAsserts()
        {
            var all = _manager.ListAll(_admin);
            if (all.Contains("Faulted"))
                Assert.Fail("Projections faulted while running the test" + "\r\n" + all);
        }

        protected void EnableStandardProjections()
        {
            EnableProjection(ProjectionNamesBuilder.StandardProjections.EventByCategoryStandardProjection);
            EnableProjection(ProjectionNamesBuilder.StandardProjections.EventByTypeStandardProjection);
            EnableProjection(ProjectionNamesBuilder.StandardProjections.StreamByCategoryStandardProjection);
            EnableProjection(ProjectionNamesBuilder.StandardProjections.StreamsStandardProjection);
        }

        protected void DisableStandardProjections()
        {
            DisableProjection(ProjectionNamesBuilder.StandardProjections.EventByCategoryStandardProjection);
            DisableProjection(ProjectionNamesBuilder.StandardProjections.EventByTypeStandardProjection);
            DisableProjection(ProjectionNamesBuilder.StandardProjections.StreamByCategoryStandardProjection);
            DisableProjection(ProjectionNamesBuilder.StandardProjections.StreamsStandardProjection);
        }

        protected virtual bool GivenStandardProjectionsRunning()
        {
            return true;
        }

        protected void EnableProjection(string name)
        {
            _manager.Enable(name, _admin);
        }

        protected void DisableProjection(string name)
        {
            _manager.Disable(name, _admin);
        }

        [TestFixtureTearDown]
        public override void TestFixtureTearDown()
        {
            _conn.Close();
            _nodes[0].Shutdown();
            _nodes[1].Shutdown();
            _nodes[2].Shutdown();
            base.TestFixtureTearDown();
#if DEBUG
            QueueStatsCollector.InitializeIdleDetection(false);
#endif
        }

        protected virtual void When()
        {
        }

        protected virtual void Given()
        {
        }

        protected void PostEvent(string stream, string eventType, string data)
        {
            _conn.AppendToStream(stream, ExpectedVersion.Any, new[] {CreateEvent(eventType, data)});
        }

        protected void HardDeleteStream(string stream)
        {
            _conn.DeleteStream(stream, ExpectedVersion.Any, true, _admin);
        }

        protected void SoftDeleteStream(string stream)
        {
            _conn.DeleteStream(stream, ExpectedVersion.Any, false, _admin);
        }

        protected static EventData CreateEvent(string type, string data)
        {
            return new EventData(Guid.NewGuid(), type, true, Encoding.UTF8.GetBytes(data), new byte[0]);
        }

        protected static void WaitIdle()
        {
            QueueStatsCollector.WaitIdle();
        }

        [Conditional("DEBUG")]
        protected void AssertStreamTail(string streamId, params string[] events)
        {
#if DEBUG
            var result = _conn.ReadStreamEventsBackward(streamId, -1, events.Length, true, _admin);
            switch (result.Status)
            {
                case SliceReadStatus.StreamDeleted:
                    Assert.Fail("Stream '{0}' is deleted", streamId);
                    break;
                case SliceReadStatus.StreamNotFound:
                    Assert.Fail("Stream '{0}' does not exist", streamId);
                    break;
                case SliceReadStatus.Success:
                    var resultEventsReversed = result.Events.Reverse().ToArray();
                    if (resultEventsReversed.Length < events.Length)
                        DumpFailed("Stream does not contain enough events", streamId, events, result.Events);
                    else
                    {
                        for (var index = 0; index < events.Length; index++)
                        {
                            var parts = events[index].Split(new char[] { ':' }, 2);
                            var eventType = parts[0];
                            var eventData = parts[1];

                            if (resultEventsReversed[index].Event.EventType != eventType)
                                DumpFailed("Invalid event type", streamId, events, resultEventsReversed);
                            else
                                if (resultEventsReversed[index].Event.DebugDataView != eventData)
                                    DumpFailed("Invalid event body", streamId, events, resultEventsReversed);
                        }
                    }
                    break;
            }
#endif
        }

        [Conditional("DEBUG")]
        protected void DumpStream(string streamId)
        {
#if DEBUG
            var result = _conn.ReadStreamEventsBackward(streamId, -1, 100, true, _admin);
            switch (result.Status)
            {
                case SliceReadStatus.StreamDeleted:
                    Assert.Fail("Stream '{0}' is deleted", streamId);
                    break;
                case SliceReadStatus.StreamNotFound:
                    Assert.Fail("Stream '{0}' does not exist", streamId);
                    break;
                case SliceReadStatus.Success:
                    Dump("Dumping..", streamId, result.Events.Reverse().ToArray());
                    break;
            }
#endif
        }

#if DEBUG
        private void DumpFailed(string message, string streamId, string[] events, ResolvedEvent[] resultEvents)
        {
            var expected = events.Aggregate("", (a, v) => a + ", " + v);
            var actual = resultEvents.Aggregate(
                "", (a, v) => a + ", " + v.Event.EventType + ":" + v.Event.DebugDataView);

            var actualMeta = resultEvents.Aggregate(
                "", (a, v) => a + "\r\n" + v.Event.EventType + ":" + v.Event.DebugMetadataView);


            Assert.Fail(
                "Stream: '{0}'\r\n{1}\r\n\r\nExisting events: \r\n{2}\r\n Expected events: \r\n{3}\r\n\r\nActual metas:{4}", streamId,
                message, actual, expected, actualMeta);

        }

        private void Dump(string message, string streamId, ResolvedEvent[] resultEvents)
        {
            var actual = resultEvents.Aggregate(
                "", (a, v) => a + ", " + v.OriginalEvent.EventType + ":" + v.OriginalEvent.DebugDataView);

            var actualMeta = resultEvents.Aggregate(
                "", (a, v) => a + "\r\n" + v.OriginalEvent.EventType + ":" + v.OriginalEvent.DebugMetadataView);


            Debug.WriteLine(
                "Stream: '{0}'\r\n{1}\r\n\r\nExisting events: \r\n{2}\r\n \r\nActual metas:{3}", streamId,
                message, actual, actualMeta);
        }
#endif

        protected void PostProjection(string query)
        {
            _manager.CreateContinuous("test-projection", query, _admin);
            WaitIdle();
        }
    }

    [TestFixture]
    public class TestTest : specification_with_standard_projections_runnning
    {
        [Test]
        public void Test()
        {
            Assert.Inconclusive();
        }
    }
}
