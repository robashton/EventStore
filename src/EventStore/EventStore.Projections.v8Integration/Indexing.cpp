#include "stdafx.h"
#include "Indexing.h"

#include "CLucene.h"
#include "CLucene/config/repl_wchar.h"
#include <iostream>

using namespace lucene::document;
using namespace lucene::index;
using namespace lucene::search;
using namespace lucene::queryParser;

namespace js1 
{
  LuceneEngine::LuceneEngine(std::string index_path, LOG_CALLBACK logger)
  {
	  this->logger = logger;
	  this->index_path = index_path;
  };

  LuceneEngine::~LuceneEngine() 
  {

  };

  void LuceneEngine::handle(const std::string& cmd, const std::string& body)
  {
	  std::string msg;
	  msg += "Handling ";
	  msg += cmd;
	  msg += "\r\n\r\n";
	  msg += body;
	  this->log(msg);
	  
	  Json::Value parsed_body;
	  Json::Reader reader;
	  reader.parse(body, parsed_body);

	  if(cmd == "index-creation-requested") {
	    this->create_index(parsed_body);
	  }
	  else if(cmd == "item-created") {
	    this->create_item(parsed_body);
	  }
	  else if(cmd == "item-updated") {
	    this->update_item(parsed_body);
	  }
  };

  void	LuceneEngine::create_index(const Json::Value& body) 
  {
	  // NOTE: Failure if we call create and it already exists?
	  // Provide explicit API for "create if not already there, reset if already there, etc"
	  std::string name = body["name"].asString();
	  this->touch_writer(name);
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

  void LuceneEngine::touch_writer(const std::string& name)
  {
	  if(this->writers[name]) return;

	  // TODO: Take this in from config
	  const char* dir = name.c_str();
	  if ( IndexReader::indexExists(dir) && IndexReader::isLocked(dir)) 
	  {
		IndexReader::unlock(dir);
	  }
	  this->writers[name] = new IndexWriter(dir, &default_writing_analyzer, true);
  };

  lucene::index::IndexWriter* LuceneEngine::get_writer(const std::string& name)
  {
	  this->touch_writer(name);
	  return this->writers[name];
  };

  void LuceneEngine::populate_document(lucene::document::Document& doc, const Json::Value& fields)
  {
	//Store { STORE_YES = 1, STORE_NO = 2, STORE_COMPRESS = 4 }
	//enum  	Index { INDEX_NO = 16, INDEX_TOKENIZED = 32, INDEX_UNTOKENIZED = 64, INDEX_NONORMS = 128 }
	//enum  	TermVector { 
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
	  // wow, so pointer, many leak, very C++
	  const char* dir = index.c_str();
	  IndexReader* reader = IndexReader::open(dir, false, NULL);
	  IndexSearcher* searcher = new IndexSearcher(reader);
	  QueryParser* parser = new QueryParser(L"", &this->default_writing_analyzer);

	  std::wstring wquery = utf8_to_wstr(query);

	  Query* parsedQuery = parser->parse(wquery.c_str());
	  Hits* hits = searcher->search(parsedQuery);
	  QueryResult* result = new QueryResult();

	  result->num_results = hits->length();

	  Json::Value results(Json::arrayValue);

	  for(int x = 0; x < hits->length() ; x++)
	  {
		  Document &doc = hits->doc(x);
		  Field* stored_data = doc.getField(L"__data");
		  Field* id = doc.getField(L"__id");

		  std::wstring data_val_wide = stored_data->stringValue();
		  std::wstring id_val_wide = id->stringValue();
		  std::string data_val = lucene_wcstoutf8string(data_val_wide.c_str(), data_val_wide.length());
		  std::string id_val = lucene_wcstoutf8string(id_val_wide.c_str(), id_val_wide.length());

		  Json::Value jsonDocument;
		  jsonDocument["id"] = Json::Value(id_val);
		  jsonDocument["data"] = Json::Value(data_val);
		  results.append(jsonDocument);
	  }

	  std::string resultJson = results.toStyledString();
	  
	  result->json = new unsigned char[resultJson.length()];
	  result->num_bytes = resultJson.length();
	  std::copy(resultJson.begin(), resultJson.end(), result->json);
	  
	  //delete searcher;
	  //delete reader;

	  return result;
  };

  void LuceneEngine::free_query_result(QueryResult* result)
  {
	  if(result->json != NULL)
		delete[] result->json;
	  delete result;
  }

  void LuceneEngine::flush()
  {
       std::map<std::string, IndexWriter*>::iterator iter;
       for (iter = this->writers.begin(); iter != this->writers.end(); iter++) 
	   {
		  iter->second->flush();
       }
  }

  void LuceneEngine::log(const std::string& msg)
  {
	  std::cout << msg;
  }

}
