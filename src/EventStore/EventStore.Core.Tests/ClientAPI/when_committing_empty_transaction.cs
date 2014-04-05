﻿using System;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture, Category("LongRunning")]
    public class when_committing_empty_transaction : SpecificationWithDirectory
    {
        private MiniNode _node;
        private IEventStoreConnection _connection;
        private EventData _firstEvent;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            _node = new MiniNode(PathName);
            _node.Start();

            _firstEvent = TestEvent.NewTestEvent();

            _connection = TestConnection.Create(_node.TcpEndPoint);
            _connection.Connect();

            Assert.AreEqual(2, _connection.AppendToStream("test-stream",
                                                          ExpectedVersion.NoStream,
                                                          _firstEvent,
                                                          TestEvent.NewTestEvent(),
                                                          TestEvent.NewTestEvent()).NextExpectedVersion);

            using (var transaction = _connection.StartTransaction("test-stream", 2))
            {
                Assert.AreEqual(2, transaction.Commit().NextExpectedVersion);
            }
        }

        [TearDown]
        public override void TearDown()
        {
            _connection.Close();
            _node.Shutdown();
            base.TearDown();
        }

        [Test]
        public void following_append_with_correct_expected_version_are_commited_correctly()
        {
            Assert.AreEqual(4, _connection.AppendToStream("test-stream", 2, TestEvent.NewTestEvent(), TestEvent.NewTestEvent()).NextExpectedVersion);

            var res = _connection.ReadStreamEventsForward("test-stream", 0, 100, false);
            Assert.AreEqual(SliceReadStatus.Success, res.Status);
            Assert.AreEqual(5, res.Events.Length);
            for (int i=0; i<5; ++i)
            {
                Assert.AreEqual(i, res.Events[i].OriginalEventNumber);
            }
        }

        [Test]
        public void following_append_with_expected_version_any_are_commited_correctly()
        {
            Assert.AreEqual(4, _connection.AppendToStream("test-stream", ExpectedVersion.Any, TestEvent.NewTestEvent(), TestEvent.NewTestEvent()).NextExpectedVersion);

            var res = _connection.ReadStreamEventsForward("test-stream", 0, 100, false);
            Assert.AreEqual(SliceReadStatus.Success, res.Status);
            Assert.AreEqual(5, res.Events.Length);
            for (int i = 0; i < 5; ++i)
            {
                Assert.AreEqual(i, res.Events[i].OriginalEventNumber);
            }
        }

        [Test]
        public void committing_first_event_with_expected_version_no_stream_is_idempotent()
        {
            Assert.AreEqual(0, _connection.AppendToStream("test-stream", ExpectedVersion.NoStream, _firstEvent).NextExpectedVersion);

            var res = _connection.ReadStreamEventsForward("test-stream", 0, 100, false);
            Assert.AreEqual(SliceReadStatus.Success, res.Status);
            Assert.AreEqual(3, res.Events.Length);
            for (int i = 0; i < 3; ++i)
            {
                Assert.AreEqual(i, res.Events[i].OriginalEventNumber);
            }
        }

        [Test]
        public void trying_to_append_new_events_with_expected_version_no_stream_fails()
        {
            Assert.That(() => _connection.AppendToStream("test-stream", ExpectedVersion.NoStream, TestEvent.NewTestEvent()),
                        Throws.Exception.InstanceOf<AggregateException>()
                        .With.InnerException.InstanceOf<WrongExpectedVersionException>());
        }
    }
}
