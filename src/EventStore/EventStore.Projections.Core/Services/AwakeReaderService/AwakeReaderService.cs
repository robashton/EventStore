﻿using System;
using System.Collections.Generic;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.AwakeReaderService
{
    public class AwakeReaderService : IHandle<AwakeReaderServiceMessage.SubscribeAwake>,
        IHandle<AwakeReaderServiceMessage.UnsubscribeAwake>,
        IHandle<StorageMessage.EventCommitted>,
        IHandle<StorageMessage.TfEofAtNonCommitRecord>

    {
        private readonly Dictionary<string, HashSet<AwakeReaderServiceMessage.SubscribeAwake>> _subscribers =
            new Dictionary<string, HashSet<AwakeReaderServiceMessage.SubscribeAwake>>();

        private readonly Dictionary<Guid, AwakeReaderServiceMessage.SubscribeAwake> _map =
            new Dictionary<Guid, AwakeReaderServiceMessage.SubscribeAwake>();

        private TFPos _lastPosition;

        private readonly List<AwakeReaderServiceMessage.SubscribeAwake> _batchedReplies =
            new List<AwakeReaderServiceMessage.SubscribeAwake>();

        private int _processedEvents;
        private int _processedEventsAwakeThreshold = 1000;

        private void BeginReplyBatch()
        {
            if (_batchedReplies.Count > 0)
                throw new Exception();
            _processedEvents = 0;
        }

        private void EndReplyBatch()
        {
            foreach (var subscriber in _batchedReplies)
            {
                subscriber.Envelope.ReplyWith(subscriber.ReplyWithMessage);
            }
            _batchedReplies.Clear();
        }

        private void CheckProcessedEventThreshold()
        {
            if (_processedEvents > _processedEventsAwakeThreshold)
            {
                EndReplyBatch();
                BeginReplyBatch();
            }
        }

        public void Handle(AwakeReaderServiceMessage.SubscribeAwake message)
        {
            if (message.From < _lastPosition)
            {
                message.Envelope.ReplyWith(message.ReplyWithMessage);
                return;
            }
            _map.Add(message.CorrelationId, message);
            HashSet<AwakeReaderServiceMessage.SubscribeAwake> list;
            string streamId = message.StreamId ?? "$all";
            if (!_subscribers.TryGetValue(streamId, out list))
            {
                list = new HashSet<AwakeReaderServiceMessage.SubscribeAwake>();
                _subscribers.Add(streamId, list);
            }
            list.Add(message);
        }

        public void Handle(StorageMessage.EventCommitted message)
        {
            _processedEvents++;
            _lastPosition = new TFPos(message.CommitPosition, message.Event.LogPosition);
            NotifyEventInStream("$all", message);
            NotifyEventInStream(message.Event.EventStreamId, message);
            if (message.TfEof)
            {
                EndReplyBatch();
                BeginReplyBatch();
            }
            CheckProcessedEventThreshold();
        }

        private void NotifyEventInStream(string streamId, StorageMessage.EventCommitted message)
        {
            HashSet<AwakeReaderServiceMessage.SubscribeAwake> list;
            List<AwakeReaderServiceMessage.SubscribeAwake> toRemove = null;
            if (_subscribers.TryGetValue(streamId, out list))
            {
                foreach (var subscriber in list)
                {
                    if (subscriber.From < new TFPos(message.CommitPosition, message.Event.LogPosition))
                    {
                        _batchedReplies.Add(subscriber);
                        _map.Remove(subscriber.CorrelationId);
                        if (toRemove == null)
                            toRemove = new List<AwakeReaderServiceMessage.SubscribeAwake>();
                        toRemove.Add(subscriber);
                    }
                }
                if (toRemove != null)
                {
                    foreach (var item in toRemove)
                        list.Remove(item);
                    if (list.Count == 0)
                    {
                        _subscribers.Remove(streamId);
                    }
                }
            }
        }

        public void Handle(AwakeReaderServiceMessage.UnsubscribeAwake message)
        {
            AwakeReaderServiceMessage.SubscribeAwake subscriber;
            if (_map.TryGetValue(message.CorrelationId, out subscriber))
            {
                _map.Remove(message.CorrelationId);
                var list = _subscribers[subscriber.StreamId ?? "$all"];
                list.Remove(subscriber);
            }
        }

        public void Handle(StorageMessage.TfEofAtNonCommitRecord message)
        {
            EndReplyBatch();
            BeginReplyBatch();
        }
    }
}
