using System;
using System.Collections.Generic;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class HeadingEventReader
    {
        private readonly ILogger _logger = LogManager.GetLoggerFor<HeadingEventReader>();
        private IEventReader _headEventReader;
        private TFPos _subscribeFromPosition = new TFPos(long.MaxValue, long.MaxValue);

        private abstract class Item
        {
            public readonly TFPos Position;

            public Item(TFPos position)
            {
                Position = position;
            }

            public abstract void Handle(IReaderSubscription subscription);
        }

        private class CommittedEventItem : Item
        {
            public readonly ReaderSubscriptionMessage.CommittedEventDistributed Message;

            public CommittedEventItem(ReaderSubscriptionMessage.CommittedEventDistributed message)
                : base(message.Data.Position)
            {
                Message = message;
            }

            public override void Handle(IReaderSubscription subscription)
            {
                subscription.Handle(Message);
            }
        }

        private class PartitionDeletedItem : Item
        {
            public readonly ReaderSubscriptionMessage.EventReaderPartitionDeleted Message;

            public PartitionDeletedItem(ReaderSubscriptionMessage.EventReaderPartitionDeleted message)
                : base(message.DeleteLinkOrEventPosition.Value)
            {
                Message = message;
            }

            public override void Handle(IReaderSubscription subscription)
            {
                subscription.Handle(Message);
            }
        }

        private readonly Queue<Item> _lastMessages = new Queue<Item>();

        private readonly int _eventCacheSize;

        private readonly Dictionary<Guid, IReaderSubscription> _headSubscribers =
            new Dictionary<Guid, IReaderSubscription>();

        private bool _headEventReaderPaused;

        private Guid _eventReaderId;

        private bool _started;

        private TFPos _lastEventPosition = new TFPos(0, -1);
        private TFPos _lastDeletePosition = new TFPos(0, -1);

        public HeadingEventReader(int eventCacheSize)
        {
            _eventCacheSize = eventCacheSize;
        }

        public bool Handle(ReaderSubscriptionMessage.CommittedEventDistributed message)
        {
            EnsureStarted();
            if (message.CorrelationId != _eventReaderId)
                return false;
            if (message.Data == null)
                return true;
            ValidateEventOrder(message);

            CacheRecentMessage(message);
            DistributeMessage(message);
            if (_headSubscribers.Count == 0 && !_headEventReaderPaused)
            {
//                _headEventReader.Pause();
//                _headEventReaderPaused = true;
            }
            return true;
        }

        public bool Handle(ReaderSubscriptionMessage.EventReaderPartitionDeleted message)
        {
            EnsureStarted();
            if (message.CorrelationId != _eventReaderId)
                return false;

            ValidateEventOrder(message);


            CacheRecentMessage(message);
            DistributeMessage(message);
            if (_headSubscribers.Count == 0 && !_headEventReaderPaused)
            {
                //                _headEventReader.Pause();
                //                _headEventReaderPaused = true;
            }
            return true;
        }

        public bool Handle(ReaderSubscriptionMessage.EventReaderIdle message)
        {
            EnsureStarted();
            if (message.CorrelationId != _eventReaderId)
                return false;
            DistributeMessage(message);
            return true;
        }

        private void ValidateEventOrder(ReaderSubscriptionMessage.CommittedEventDistributed message)
        {
            if (_lastEventPosition >= message.Data.Position || _lastDeletePosition > message.Data.Position)
                throw new InvalidOperationException(
                    string.Format(
                        "Invalid committed event order.  Last: '{0}' Received: '{1}'  LastDelete: '{2}'",
                        _lastEventPosition, message.Data.Position, _lastEventPosition));
            _lastEventPosition = message.Data.Position;
        }

        private void ValidateEventOrder(ReaderSubscriptionMessage.EventReaderPartitionDeleted message)
        {
            if (_lastEventPosition > message.DeleteLinkOrEventPosition.Value
                || _lastDeletePosition >= message.DeleteLinkOrEventPosition.Value)
                throw new InvalidOperationException(
                    string.Format(
                        "Invalid partition deleted event order.  Last: '{0}' Received: '{1}'  LastDelete: '{2}'",
                        _lastEventPosition, message.DeleteLinkOrEventPosition.Value, _lastEventPosition));
            _lastDeletePosition = message.DeleteLinkOrEventPosition.Value;
        }

        public void Start(Guid eventReaderId, IEventReader eventReader)
        {
            if (_started)
                throw new InvalidOperationException("Already started");
            _eventReaderId = eventReaderId;
            _headEventReader = eventReader;
            //Guid.Empty means head distribution point
            _started = true; // started before resume due to old style test with immediate callback
            _headEventReader.Resume();
        }

        public void Stop()
        {
            EnsureStarted();
            _headEventReader.Pause();
            _headEventReader = null;
            _started = false;
        }

        public bool TrySubscribe(
            Guid projectionId, IReaderSubscription readerSubscription, long fromTransactionFilePosition)
        {
            EnsureStarted();
            if (_headSubscribers.ContainsKey(projectionId))
                throw new InvalidOperationException(
                    string.Format("Projection '{0}' has been already subscribed", projectionId));
            // if first available event commit position is before the safe TF (prepare) position - join
            if (_subscribeFromPosition.CommitPosition <= fromTransactionFilePosition)
            {
                _logger.Trace(
                    "The '{0}' subscription has joined the heading distribution point at '{1}'", projectionId,
                    fromTransactionFilePosition);
                DispatchRecentMessagesTo(readerSubscription, fromTransactionFilePosition);
                AddSubscriber(projectionId, readerSubscription);
                return true;
            }
            return false;
        }

        public void Unsubscribe(Guid projectionId)
        {
            EnsureStarted();
            if (!_headSubscribers.ContainsKey(projectionId))
                throw new InvalidOperationException(
                    string.Format("Projection '{0}' has not been subscribed", projectionId));
            _logger.Trace(
                "The '{0}' subscription has unsubscribed from the '{1}' heading distribution point", projectionId,
                _eventReaderId);
            _headSubscribers.Remove(projectionId);
        }

        private void DispatchRecentMessagesTo(IReaderSubscription subscription, long fromTransactionFilePosition)
        {
            foreach (var m in _lastMessages)
                if (m.Position.CommitPosition >= fromTransactionFilePosition)
                    m.Handle(subscription);
        }

        private void DistributeMessage(ReaderSubscriptionMessage.CommittedEventDistributed message)
        {
            foreach (var subscriber in _headSubscribers.Values)
                subscriber.Handle(message);
        }

        private void DistributeMessage(ReaderSubscriptionMessage.EventReaderPartitionDeleted message)
        {
            foreach (var subscriber in _headSubscribers.Values)
                subscriber.Handle(message);
        }

        private void DistributeMessage(ReaderSubscriptionMessage.EventReaderIdle message)
        {
            foreach (var subscriber in _headSubscribers.Values)
                subscriber.Handle(message);
        }

        private void CacheRecentMessage(ReaderSubscriptionMessage.CommittedEventDistributed message)
        {
            _lastMessages.Enqueue(new CommittedEventItem(message));
            if (_lastMessages.Count > _eventCacheSize)
            {
                _lastMessages.Dequeue();
            }
            var lastAvailableCommittedevent = _lastMessages.Peek();
            _subscribeFromPosition = lastAvailableCommittedevent.Position;
        }

        private void CacheRecentMessage(ReaderSubscriptionMessage.EventReaderPartitionDeleted message)
        {
            _lastMessages.Enqueue(new PartitionDeletedItem(message));
            if (_lastMessages.Count > _eventCacheSize)
            {
                _lastMessages.Dequeue();
            }
            var lastAvailableCommittedevent = _lastMessages.Peek();
            _subscribeFromPosition = lastAvailableCommittedevent.Position;
        }

        private void AddSubscriber(Guid publishWithCorrelationId, IReaderSubscription subscription)
        {
            _logger.Trace(
                "The '{0}' projection subscribed to the '{1}' heading distribution point", publishWithCorrelationId,
                _eventReaderId);
            _headSubscribers.Add(publishWithCorrelationId, subscription);
            if (_headEventReaderPaused)
            {
                _headEventReaderPaused = false;
                //_headEventReader.Resume();
            }
        }

        private void EnsureStarted()
        {
            if (!_started)
                throw new InvalidOperationException("Not started");
        }

    }
}
