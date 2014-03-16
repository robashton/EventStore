#pragma once

// Note: I'm doing includes here because this header is never surfaced
// and it makes my life easier, feel free to complain
#include "json/json.h"
#include "CLucene.h"
#include "js1.h"


// TODO: This really has nothing to do with js1
namespace js1
{
    // With apologies
    class LuceneException
    {
        public:
            enum Codes { NO_INDEX = 1, INDEX_ALREADY_EXISTS = 2 , LUCENE_ERROR = 3 };

            Codes Code() { return _code; }
            LuceneException(Codes code)
            {
                _code = code;
            }
        private:
            Codes _code;
    };

    struct QueryResult
    {
        unsigned char* json;
        int num_results;
        int num_bytes;
    };

    class LuceneEngine
    {
        public:
            LuceneEngine(std::string index_path, LOG_CALLBACK logger);
            ~LuceneEngine();
            void handle(const std::string& ev, const std::string& body);
            QueryResult* create_query_result(const std::string& index, const std::string& query);
            void free_query_result(QueryResult* result);
            void flush(const std::string& index, int position);
        private:

            // Handlers
            void create_index(const Json::Value& body);
            void reset_index(const Json::Value& body);
            void create_item(const Json::Value& body);

            void log(const std::string& msg);

            lucene::index::IndexWriter* get_writer(const std::string& name);
            lucene::index::IndexReader* get_reader(const std::string& name);
            lucene::store::Directory* get_directory(const std::string& name);
            lucene::store::Directory* create_directory(const std::string& name);

            void populate_document(lucene::document::Document& doc, const Json::Value& fields);
            std::wstring utf8_to_wstr(const std::string& in);
            std::string wstr_to_utf8(const std::wstring& in);

            lucene::analysis::WhitespaceAnalyzer default_writing_analyzer;
            std::map<std::string, lucene::index::IndexWriter*> writers;
            std::map<std::string, lucene::index::IndexReader*> readers;
            std::map<std::string, lucene::store::Directory*> directories;
            LOG_CALLBACK logger;
            std::string index_path;
    };
}

