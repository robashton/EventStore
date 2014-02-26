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

using EventStore.ClientAPI.Common;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.event_filter
{
    [TestFixture]
    public class include_everything_event_filter : TestFixtureWithEventFilter
    {
        protected override void Given()
        {
            _builder.FromAll();
            _builder.AllEvents();
        }

        [Test]
        public void can_be_built()
        {
            Assert.IsNotNull(_ef);
        }

        [Test]
        public void does_not_pass_categorized_event()
        {
            Assert.IsFalse(_ef.Passes(true, "$ce-stream", "event"));
        }

        [Test]
        public void passes_uncategorized_event()
        {
            Assert.IsTrue(_ef.Passes(false, "stream", "event"));
        }

        [Test]
        public void passes_stream_deleted_event()
        {
            Assert.IsTrue(_ef.Passes(false, "stream", SystemEventTypes.StreamMetadata, isStreamDeletedEvent: true));
        }


    }
}
