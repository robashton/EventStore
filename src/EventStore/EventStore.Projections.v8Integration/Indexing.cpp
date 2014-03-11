#include "stdafx.h"
#include "Indexing.h"

#include "CLucene.h"
#include "CLucene/config/repl_wchar.h"
#include <iostream>

using namespace lucene::document;
using namespace lucene::index;
using namespace lucene::search;
using namespace lucene::store;
using namespace lucene::queryParser;

// NOTE: None of this is thread-safe
// So the read-operations have the possibility of completely balking if indexes haven't been opened yet
// by the streams - I guess I should be resolving this
namespace js1 
{
  LuceneEngine::LuceneEngine(std::string index_path, LOG_CALLBACK logger)
  {
      this->logger = logger;
      this->index_path = index_path;
  };

  LuceneEngine::~LuceneEngine() 
  {
      // TODO: Close writers
      // TODO: Close directories
  };

  void LuceneEngine::handle(const std::string& cmd, const std::string& body)
  {
      Json::Value parsed_body;
      Json::Reader reader;
      reader.parse(body, parsed_body);

      if(cmd == "index-creation-requested") {
        this->create_index(parsed_body);
      }
      else if(cmd == "index-reset-requested") {
        this->reset_index(parsed_body);
      }
      else if(cmd == "item-created") {
        this->create_item(parsed_body);
      }
      else if(cmd == "item-updated") {
        this->update_item(parsed_body);
      }
  };

  void  LuceneEngine::create_index(const Json::Value& body) 
  {
      // TODO: Determine behaviour
      std::string name = body["name"].asString();
      this->get_writer(name);
  };

  void  LuceneEngine::reset_index(const Json::Value& body) 
  {
      // TODO: Determine behaviour
      std::string name = body["name"].asString();
      this->get_writer(name);
  };

  void LuceneEngine::create_item(const Json::Value& body)
  {
      std::string id = body["item_id"].asString();
      std::string indexName = body["index_name"].asString();
      std::string indexData = body["index_data"].asString();

      IndexWriter* writer = this->get_writer(indexName);
      
      Document document;

      // Populate it
      this->populate_document(document, body["fields"]);

      // Shove in our other crap
      document.add(*(new Field(L"__id",  utf8_to_wstr(id).c_str() , Field::Store::STORE_YES | Field::Index::INDEX_UNTOKENIZED)));
      document.add(*(new Field(L"__data",  utf8_to_wstr(indexData).c_str() , Field::STORE_YES | Field::Index::INDEX_NO)));

      // Save it
      writer->addDocument(&document);
  };

  void LuceneEngine::update_item(const Json::Value& body)
  {
      std::string id = body["item_id"].asString();
      std::string indexName = body["index_name"].asString();
      IndexWriter* writer = this->get_writer(indexName);

      // TODO: Get the original document out by id
      Document document;

      // Populate it
      this->populate_document(document, body["fields"]);

      // Save it
      writer->addDocument(&document);
  };

  IndexWriter* LuceneEngine::get_writer(const std::string& name)
  {
      IndexWriter* writer = this->writers[name];
      if(writer == NULL)
      {
          Directory* dir = this->get_directory(name);
          writer = this->writers[name] = new IndexWriter(dir, &default_writing_analyzer, true); // TODO: Try false
      }
      return writer;
  };

  IndexReader* LuceneEngine::get_reader(const std::string& name)
  {
      IndexReader* reader = this->readers[name];
      if(reader == NULL)
      {
          Directory* dir = this->get_directory(name);

          // TODO: Check if dir is empty first
          reader = this->readers[name] = IndexReader::open(dir, &default_writing_analyzer);
      }
      return reader;
  };

  Directory* LuceneEngine::get_directory(const std::string& name)
  {
      Directory* dir = this->directories[name];
      if(dir == NULL)
      {
          if(this->index_path == "")
          {
              // RAM dir
              this->log(std::string("Creating ram directory for this: ") + name);
              dir = this->directories[name] = new RAMDirectory();
          }
          else
          {
              // FS dir
              this->log(std::string("Creating fs directory for this: ") + name);
              std::string full_path = this->index_path + name;
              if ( IndexReader::indexExists(full_path.c_str()) && IndexReader::isLocked(full_path.c_str())) 
              {
                IndexReader::unlock(full_path.c_str());
              }
              dir = this->directories[name] = FSDirectory::getDirectory(full_path.c_str(), true);
          }
      }
      return dir;
  };

  void LuceneEngine::populate_document(lucene::document::Document& doc, const Json::Value& fields)
  {
    //Store { STORE_YES = 1, STORE_NO = 2, STORE_COMPRESS = 4 }
    //enum      Index { INDEX_NO = 16, INDEX_TOKENIZED = 32, INDEX_UNTOKENIZED = 64, INDEX_NONORMS = 128 }
    //enum      TermVector { 
    //  TERMVECTOR_NO = 256, TERMVECTOR_YES = 512, TERMVECTOR_WITH_POSITIONS = TERMVECTOR_YES | 1024, TERMVECTOR_WITH_OFFSETS = TERMVECTOR_YES | 2048, 
    //  TERMVECTOR_WITH_POSITIONS_OFFSETS = TERMVECTOR_WITH_OFFSETS | TERMVECTOR_WITH_POSITIONS 
    //}
    
    for(Json::ValueIterator iterator = fields.begin() ;
        iterator != fields.end() ; iterator++) {

      Json::Value current = *iterator;
      int flagValue = 0;
      
      Field::Store store = current["store"].empty() ? Field::Store::STORE_NO : (Field::Store)current["store"].asInt();
      Field::Index index = current["index"].empty() ? Field::Index::INDEX_UNTOKENIZED : (Field::Index)current["index"].asInt();
      Field::TermVector termVector = current["termvector"].empty() ? Field::TermVector::TERMVECTOR_NO : (Field::TermVector)current["termvector"].asInt();

      std::string name = current["name"].asString();
      std::string value = current["value"].asString();
      std::string msg = name + value;

      this->log(msg);

      std::wstring wname = utf8_to_wstr(name);
      std::wstring wvalue = utf8_to_wstr(value);

      Field* field = new Field(wname.c_str(), wvalue.c_str(), store | index | termVector);
      doc.add(*field);
    }
  };

  // TODO: Replace this with Lucene's conversion things
  // Also TODO: Should probably start using wstring in our API
  // And get rid of our UTF8Marshaling code
  std::wstring LuceneEngine::utf8_to_wstr(const std::string& in)
  {
      // This apparently works even for non ansi characters
      // but I've not tested it. Consider this a placeholder until I 
      // think of a better string strategy
      std::wstringstream ws;
      ws << in.c_str();
      return ws.str();
  }


  QueryResult* LuceneEngine::create_query_result(const std::string& index, const std::string& query)
  {
      // Need to work out if reader is thread-safe like in Java Lucene
      // It might need re-opening and swapping though, in which case
      // our own code wouldn't be thread-safe
      Directory* dir = this->get_directory(index);

      IndexSearcher searcher(dir);

      std::wstring wquery = utf8_to_wstr(query);

      this->log(std::string("About to parse query: ") + query);

      Query* parsedQuery = QueryParser::parse(wquery.c_str(), L"__id", &this->default_writing_analyzer);

      this->log(std::string("Parsed: ") + query);

      Hits* hits = searcher.search(parsedQuery);

      this->log(std::string("Searching for: ") + query);
      QueryResult* result = new QueryResult();

      result->num_results = hits->length();

      this->log(std::string("Got some results"));

      Json::Value results(Json::arrayValue);
      Json::Reader jsonReader;

      for(int x = 0; x < hits->length() ; x++)
      {
          Document &doc = hits->doc(x);
          Field* stored_data = doc.getField(L"__data");

          // Ignore system docs
          if(stored_data == NULL) continue;

          std::wstring data_val_wide = stored_data->stringValue();
          std::string data_val = lucene_wcstoutf8string(data_val_wide.c_str(), data_val_wide.length());

          // TODO: Perhaps not to do this here
          // We're being given this as a string and perhaps we don't care that it is JSON
          Json::Value parsed_body;
          jsonReader.parse(data_val, parsed_body);
          results.append(parsed_body);
      }

      std::string resultJson = results.toStyledString();
      
      result->json = new unsigned char[resultJson.length()];
      result->num_bytes = resultJson.length();
      std::copy(resultJson.begin(), resultJson.end(), result->json);
      
      // TODO: try catch etc
      _CLLDELETE(parsedQuery);
      _CLLDELETE(hits);

      searcher.close();

      return result;
  };

  void LuceneEngine::free_query_result(QueryResult* result)
  {
      if(result->json != NULL)
        delete[] result->json;
      delete result;
  }

  void LuceneEngine::flush(const std::string& checkpoint)
  {
       std::map<std::string, IndexWriter*>::iterator iter;
       for (iter = this->writers.begin(); iter != this->writers.end(); iter++) 
       {
            Document checkpointDocument;
            checkpointDocument.add(*(new Field(L"__id",  L"checkpoint" , Field::Store::STORE_NO | Field::Index::INDEX_UNTOKENIZED)));
            checkpointDocument.add(*(new Field(L"__value",  utf8_to_wstr(checkpoint).c_str(), Field::Store::STORE_YES | Field::Index::INDEX_NO)));
            Term deleteTerm(L"__id", L"checkpoint");
            iter->second->updateDocument(&deleteTerm,  &checkpointDocument);
            iter->second->flush();
       }
  }

  void LuceneEngine::log(const std::string& msg)
  {
      std::cout << msg << std::endl;
  }

}
