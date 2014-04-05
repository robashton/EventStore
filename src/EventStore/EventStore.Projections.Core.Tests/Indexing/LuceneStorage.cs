using System;
using System.Linq;
using NUnit.Framework;
using EventStore.Projections.Core.Indexing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EventStore.Projections.Core.Tests.Indexing
{
    [Category("Indexing")]
    [TestFixture]
    public class LuceneStorage
    {
        Lucene _lucene;

        [SetUp]
        public void create_lucene()
        {
            _lucene = Lucene.Create("");
        }

        [TearDown]
        public void destroy_lucene()
        {
            _lucene.Dispose();
            _lucene = null;
        }

        private string[] ParseResults(string result)
        {
            return JsonConvert.DeserializeObject<string[]>(result);
        }

        private string IndexCreationEvent(string index = "textindex")
        {
            return JsonConvert.SerializeObject(new { index_name = index });
        }

        private string IndexResetEvent(string index = "textindex")
        {
            return JsonConvert.SerializeObject(new { index_name = index });
        }

        private string ItemWriteEvent(string index = "testindex", string id = "doc", string data = "{}")
        {
            return JsonConvert.SerializeObject(new {
                  index_name = index,
                  item_id = id,
                  index_data = data,
                  fields = new string [] {}
                });
        }

        [Test]
        public void writing_to_a_non_existent_index_throws_exception()
        {
            Assert.Throws<LuceneException>(()=>
                    _lucene.Write(IndexingEvents.ItemCreated, ItemWriteEvent()));
        }

        [Test]
        public void querying_a_non_existent_index_throws_exception()
        {
            Assert.Throws<LuceneException>(()=> _lucene.Query("testindex", "doc1"));
        }

        [Test]
        public void creating_an_already_existing_index_throws_exception()
        {
            _lucene.Write(IndexingEvents.IndexCreationRequested, IndexCreationEvent());
            Assert.Throws<LuceneException>(()=>
                    _lucene.Write(IndexingEvents.IndexCreationRequested, IndexCreationEvent()));
        }

        [Test]
        public void resetting_a_non_existant_index_throws_exception()
        {
            Assert.Throws<LuceneException>(()=>
                _lucene.Write(IndexingEvents.IndexResetRequested, IndexResetEvent()));
        }

        [Test]
        public void written_document_can_be_retrieved_by_id()
        {
            _lucene.Write(IndexingEvents.IndexCreationRequested, IndexCreationEvent(index: "testindex"));
            _lucene.Write(IndexingEvents.ItemCreated, ItemWriteEvent(id: "doc1", index: "testindex", data: "ignorethis"));
            _lucene.Flush("testindex", 10);

            var result = ParseResults(_lucene.Query("testindex", "doc1"));
            Assert.That(result, Is.EquivalentTo(new[] {"ignorethis"}));
        }


        [Test]
        public void document_update_overwrites_existing_document()
        {
            _lucene.Write(IndexingEvents.IndexCreationRequested, IndexCreationEvent(index: "testindex"));
            _lucene.Write(IndexingEvents.ItemCreated, ItemWriteEvent(id: "doc1", index: "testindex", data: "ignorethis"));
            _lucene.Flush("testindex", 10);
            _lucene.Write(IndexingEvents.ItemCreated, ItemWriteEvent(id: "doc1", index: "testindex", data: "updated"));
            _lucene.Flush("testindex", 11);

            var result = ParseResults(_lucene.Query("testindex", "doc1"));
            Assert.That(result, Is.EquivalentTo(new[] {"updated"}));
        }

        [Test]
        public void last_written_checkpoint_can_be_retrieved()
        {
            _lucene.Write(IndexingEvents.IndexCreationRequested, IndexCreationEvent(index: "testindex"));
            _lucene.Flush("testindex", 10);

            var result = _lucene.IndexPosition("testindex");
            Assert.That(result, Is.EqualTo(10));
        }
    }
}
