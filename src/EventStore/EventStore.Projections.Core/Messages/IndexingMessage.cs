// Copyright (c) 2012, Event Store LLP
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
using EventStore.Core.Data;
using EventStore.Core.Messaging;

namespace EventStore.Projections.Core.Messages
{
    public class IndexingMessage
    {
        public sealed class Start : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

            public override int MsgTypeId
            {
                get { return TypeId; }
            }

            public Start()
            {

            }
        }

        public sealed class Tick : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            private readonly Action _action;

            public override int MsgTypeId
            {
                get { return TypeId; }
            }

            public Tick(Action action)
            {
                _action = action;
            }

            public void Execute()
            {
                _action();
            }
        }

        public sealed class QueryRequest : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
			private readonly string _query;
			private readonly string _index;
			private readonly IEnvelope _envelope;

			public string Index
			{
				get { return _index; }
			}

			public string Query
			{
				get { return _query; }
			}

			public IEnvelope Envelope
			{
				get { return _envelope; }
			}

            public override int MsgTypeId
            {
                get { return TypeId; }
            }

            public QueryRequest(IEnvelope envelope, string index, string query)
            {
				this._envelope = envelope;
				this._query = query;
				this._index = index;
            }
        }

        public sealed class QueryResult : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
			private readonly string _data;
            private readonly Exception _exception;

            public Exception Exception
            {
                get { return _exception; }
            }

			public string Data
			{
				get { return _data; }
			}

            public override int MsgTypeId
            {
                get { return TypeId; }
            }

            public QueryResult(string data)
            {
				_data = data;
            }

            public QueryResult(Exception ex)
            {
                _exception = ex;
            }
        }

        public sealed class AddIndex : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
			private readonly string _indexName;

			public string IndexName
			{
				get { return _indexName; }
			}

            public override int MsgTypeId
            {
                get { return TypeId; }
            }

            public AddIndex(string indexName)
            {
                _indexName = indexName;
            }
        }

        public sealed class ResetIndex : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
			private readonly string _indexName;

			public string IndexName
			{
				get { return _indexName; }
			}

            public override int MsgTypeId
            {
                get { return TypeId; }
            }

            public ResetIndex(string indexName)
            {
                _indexName = indexName;
            }
        }

        public sealed class NotFound : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
			private readonly string _reason;

			public string Reason
			{
				get { return _reason; }
			}

            public override int MsgTypeId
            {
                get { return TypeId; }
            }

            public NotFound(string reason)
            {
				_reason = reason;
            }
        }

        public sealed class OperationFailed : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
			private readonly string _reason;

			public string Reason
			{
				get { return _reason; }
			}

            public override int MsgTypeId
            {
                get { return TypeId; }
            }

            public OperationFailed(string reason)
            {
				_reason = reason;
            }
        }
    }
}
