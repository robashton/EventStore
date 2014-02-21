#include "stdafx.h"
#include "Indexing.h"

#include "CLucene.h"

using namespace lucene::document;
using namespace lucene::index;

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
	  this->log(msg.c_str());
	  
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
	  std::string id = body["itemId"].asString();
	  std::string indexName = body["indexName"].asString();
	  IndexWriter* writer = this->get_writer(indexName);
	  Document document;

	  // Populate it
	  this->populate_document(document, body["fields"]);

	  // Save it
	  writer->addDocument(&document);

	  // Should think about batching
	  writer->flush();
  };

  void LuceneEngine::update_item(const Json::Value& body)
  {
	  std::string id = body["itemId"].asString();
	  std::string indexName = body["indexName"].asString();
	  IndexWriter* writer = this->get_writer(indexName);

	  // TODO: Get the original document out by id
	  Document document;

	  // Populate it
	  this->populate_document(document, body["fields"]);

	  // Save it
	  writer->addDocument(&document);
	  
	  // Should think about batching
	  writer->flush();
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
	  this->writers[name] = new lucene::index::IndexWriter(dir, &default_writing_analyzer, true);
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

	  this->log(msg.c_str());

	  std::wstringstream ws;
	  ws << name.c_str();
	  std::wstring wname = ws.str();
	  ws.str(L"");
	  ws << value.c_str();
	  std::wstring wvalue = ws.str();

	  Field* field = new Field(wname.c_str(), wvalue.c_str(), store | index | termVector);
	  doc.add(*field);
	}
  };

  void LuceneEngine::log(const char* msg)
  {
	  v8::HandleScope scope(v8::Isolate::GetCurrent());
	  v8::String::Value value(v8::String::NewFromUtf8(v8::Isolate::GetCurrent(), msg));
	  this->logger(*value);
  }

}
