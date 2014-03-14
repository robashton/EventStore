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

using NUnit.Framework;
using System.Linq;

namespace EventStore.Projections.Core.Tests.ClientAPI.Indexing
{
    [TestFixture]
    public class querying_a_lucene_projection : specification_with_indexing_runnning
    {
        protected override void Given()
        {
            base.Given();
            PostProjection(@"
fromStream('chat-GeneralChat')
.when({
  $created: function() {
    createIndex('ChatMessages');
  },
  Message: function(s, e) {
    createIndexItem(
      'ChatMessages',
      'chat-' + e.sequenceNumber,
      [{ name: 'Sender', value: e.body.Sender }],  e.body )}})"); // Index the whole event
            WaitIdle();
        }

        protected override void When()
        {
            base.When();
            PostEvent("chat-GeneralChat", "Message", "{ \"Sender\": \"bob\" }");
            PostEvent("chat-GeneralChat", "Message", "{ \"Sender\": \"alice\" }");
            PostEvent("chat-GeneralChat", "Message", "{ \"Sender\": \"craig\" }");
            WaitIdle();
        }

        [Test, Category("Network")]
        public void can_lookup_by_id()
        {
            var expected = QueryIndex<ChatMessage>("ChatMessages", "chat-0").SingleOrDefault();
            Assert.That(expected.Sender, Is.EqualTo("bob"));
        }

        [Test, Category("Network")]
        public void can_search_by_exact_match()
        {
            var expected = QueryIndex<ChatMessage>("ChatMessages", "Sender:alice").SingleOrDefault();
            Assert.That(expected.Sender, Is.EqualTo("alice"));
        }
        
        [Test, Category("Network")]
        public void can_search_by_wildcard()
        {
            var expected = QueryIndex<ChatMessage>("ChatMessages", "*:*");
            Assert.That(expected.Length, Is.EqualTo(3));
        }

        class ChatMessage
        {
            public string Sender { get; set; }
        }
    }
}
