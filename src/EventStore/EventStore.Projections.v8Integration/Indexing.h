#pragma once

// Note: I'm doing includes here because this header is never surfaced 
// and it makes my life easier, feel free to complain
#include "json/json.h"
#include "CLucene.h"
#include "js1.h"

// TODO: This really has nothing to do with js1
namespace js1 
{
  class LuceneEngine 
  {
	public:
		LuceneEngine(std::string index_path, LOG_CALLBACK logger);
		~LuceneEngine();
		void handle(const std::string& cmd, const std::string& body);
	private:
		void create_index(const Json::Value& body);
		void create_item(const Json::Value& body);
		void update_item(const Json::Value& body);

		void log(const char* msg);

		void touch_writer(const std::string& name);
		lucene::index::IndexWriter* get_writer(const std::string& name);
		void populate_document(lucene::document::Document& doc, const Json::Value& fields);


		lucene::analysis::WhitespaceAnalyzer default_writing_analyzer;
		std::map<std::string, lucene::index::IndexWriter*> writers;
		LOG_CALLBACK logger;
		std::string index_path;
  };
}

