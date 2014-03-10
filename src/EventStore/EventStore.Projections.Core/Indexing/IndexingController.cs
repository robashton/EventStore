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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Core.Services;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.Services.Transport.Http.Controllers;
using EventStore.Core.Util;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.EventReaders.Feeds;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Codecs;
using EventStore.Transport.Http.EntityManagement;
using Newtonsoft.Json.Linq;

namespace EventStore.Projections.Core.Indexing
{
    public class IndexingController : CommunicationController
    {
        private static readonly ILogger _logger = LogManager.GetLoggerFor<IndexingController>();

        private static readonly ICodec[] SupportedCodecs = new ICodec[] {Codec.Json};
        private readonly IHttpForwarder _httpForwarder;
        private readonly IPublisher _networkSendQueue;

        public IndexingController(IHttpForwarder httpForwarder, IPublisher publisher, IPublisher networkSendQueue)
            : base(publisher)
        {
            _httpForwarder = httpForwarder;
            _networkSendQueue = networkSendQueue;
        }

        protected override void SubscribeCore(IHttpService service)
        {
            Register(service, "/rob",
                     HttpMethod.Get, OnRob, Codec.NoCodecs, SupportedCodecs);
            Register(service, "/query/{index}/{query}",
                     HttpMethod.Get, OnQuery, Codec.NoCodecs, SupportedCodecs);
        }

        private void OnQuery(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;

            var envelope = new SendToHttpEnvelope<IndexingMessage.QueryResult>(
                _networkSendQueue, http, StateFormatter, StateConfigurator, ErrorsEnvelope(http));

            _logger.Info("Publishing");

            Publish(new IndexingMessage.QueryRequest(envelope, match.BoundVariables["index"], match.BoundVariables["query"]));
        }

        private string StateFormatter(ICodec codec, IndexingMessage.QueryResult msg)
        {
              _logger.Info("StateFormatter");
//            if (state.Exception != null)
//                return state.Exception.ToString();
//            else
                return msg.Data;
        }

        private ResponseConfiguration StateConfigurator(ICodec codec, IndexingMessage.QueryResult msg)
        {
            _logger.Info("StateConfigurator");
            return Configure.Ok("text/html", Helper.UTF8NoBom, null, null, false);
//            if (state.Exception != null)
//                return Configure.InternalServerError();
//            else
//                return state.Position != null
//                           ? Configure.Ok("application/json", Helper.UTF8NoBom, null, null, false,
//                                          new KeyValuePair<string, string>(SystemHeaders.ProjectionPosition, state.Position.ToJsonString()))
//                           : Configure.Ok("application/json", Helper.UTF8NoBom, null, null, false);
        }

        private IEnvelope ErrorsEnvelope(HttpEntityManager http)
        {
            _logger.Info("ErrorsEnvelope");
            return new SendToHttpEnvelope<IndexingMessage.NotFound>(
                _networkSendQueue, http, NotFoundFormatter, NotFoundConfigurator,
                new SendToHttpEnvelope<IndexingMessage.OperationFailed>(
                  _networkSendQueue, http, OperationFailedFormatter, OperationFailedConfigurator, null));
        }

        private ResponseConfiguration NotFoundConfigurator(ICodec codec, IndexingMessage.NotFound message)
        {
            return new ResponseConfiguration(404, "Not Found", "text/plain", Helper.UTF8NoBom);
        }

        private string NotFoundFormatter(ICodec codec, IndexingMessage.NotFound message)
        {
            return message.Reason;
        }

        private ResponseConfiguration OperationFailedConfigurator(
            ICodec codec, IndexingMessage.OperationFailed message)
        {
            return new ResponseConfiguration(500, "Failed", "text/plain", Helper.UTF8NoBom);
        }

        private string OperationFailedFormatter(ICodec codec, IndexingMessage.OperationFailed message)
        {
            return message.Reason;
        }

        private void OnRob(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;

            Publish( new IndexingMessage.Start());

            http.ReplyStatus(200, "Ok", Console.WriteLine);
        }
    }
}
