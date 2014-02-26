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
using System.Globalization;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.projections_manager;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.v8
{
    [TestFixture]
    public class when_creating_an_index : TestFixtureWithJsProjection
    {
        protected override void Given()
        {
            _projection = @"
			  fromAll().when({$any: 
				function(s, e) {
				  createIndexItem('TestItems', 'item-' + e.sequenceNumber,
					[{ name: 'somevalue', value: e.body.somevalue }]
				  ) } })";
            _state = @"{}";
        }

        [Test, Category("v8")]
        public void verify_index_has_data()
        {
            string state;
            EmittedEventEnvelope[] emittedEvents;
            _stateHandler.ProcessEvent(
                "", CheckpointTag.FromPosition(0, 10, 5), "stream1", "type1", "category", Guid.NewGuid(), 0, "metadata",
                @"{""somevalue"":""hello""}", out state, out emittedEvents);
            _stateHandler.ProcessEvent(
                "", CheckpointTag.FromPosition(0, 20, 15), "stream1", "type1", "category", Guid.NewGuid(), 1, "metadata",
                @"{""somevalue"":""world""}", out state, out emittedEvents);
            Assert.AreEqual(2, _logged.Count);
            Assert.AreEqual(@"1", _logged[0]);
            Assert.AreEqual(@"2", _logged[1]);
        }
    }
}
