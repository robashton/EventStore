#include "stdafx.h"
#include "Indexing.h"

#include "CLucene.h"
#include "CLucene/util/gzipcompressstream.h"
#include "CLucene/util/gzipinputstream.h"
#include "CLucene/util/byteinputstream.h"
#include "CLucene/util/streamarray.h"
#include "CLucene/config/repl_wchar.h"
#include <iostream>

using namespace lucene::document;
using namespace lucene::index;
using namespace lucene::search;
using namespace lucene::store;
using namespace lucene::queryParser;
using namespace lucene::util;

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
        std::map<std::string, IndexWriter*>::iterator writer_iter;
        for (writer_iter = this->writers.begin(); writer_iter != this->writers.end(); writer_iter++)
        {
            delete writer_iter->second;
        }
        std::map<std::string, Directory*>::iterator directory_iter;
        for (directory_iter = this->directories.begin(); directory_iter != this->directories.end(); directory_iter++)
        {
            delete directory_iter->second;
        }
    };

    void LuceneEngine::handle(const std::string& ev, const std::string& body)
    {
        Json::Value parsed_body;
        Json::Reader reader;
        reader.parse(body, parsed_body);

        this->log(ev);

        if(ev == "index-creation-requested") {
            this->create_index(parsed_body);
        }
        else if(ev == "index-reset-requested") {
            this->reset_index(parsed_body);
        }
        else if(ev == "item-created") {
            this->create_item(parsed_body);
        }
    };

    void  LuceneEngine::create_index(const Json::Value& body)
    {
        std::string name = body["index_name"].asString();
        Directory* dir = this->create_directory(name);
        this->writers[name] = new IndexWriter(dir, &default_writing_analyzer, true); // TODO: Try false
    };

    void  LuceneEngine::reset_index(const Json::Value& body)
    {
        // TODO: Determine behaviour
        std::string name = body["index_name"].asString();
        this->get_writer(name);
    };

    void LuceneEngine::create_item(const Json::Value& body)
    {
        std::string id = body["item_id"].asString();
        std::string indexName = body["index_name"].asString();
        std::string indexData = body["index_data"].asString();

        IndexWriter* writer = this->get_writer(indexName);

        Document document;

        this->populate_document(document, body["fields"]);

        // Shove in our other crap
        document.add(*(new Field(L"__id",  utf8_to_wstr(id).c_str() , Field::Store::STORE_YES | Field::Index::INDEX_UNTOKENIZED)));
        document.add(*(new Field(L"__data",  utf8_to_wstr(indexData).c_str() , Field::STORE_YES | Field::Index::INDEX_NO)));

        // Save it (delete any existing though)
        // Pretty sure this is a leak
        Term* deleteTerm = new Term(L"__id", utf8_to_wstr(id).c_str());
        writer->updateDocument(deleteTerm, &document);
    };

    IndexWriter* LuceneEngine::get_writer(const std::string& name)
    {
        IndexWriter* writer = this->writers[name];
        if(writer == NULL)
        {
            throw LuceneException(LuceneException::Codes::NO_INDEX);
        }

        return writer;
    };

    IndexReader* LuceneEngine::get_reader(const std::string& name)
    {
        IndexReader* reader = this->readers[name];
        if(reader == NULL)
        {
            Directory* dir = this->get_directory(name);
            reader = this->readers[name] = IndexReader::open(dir, &default_writing_analyzer);
        }
        return reader;
    };

    Directory* LuceneEngine::create_directory(const std::string& name)
    {
        Directory* dir = this->directories[name];
        if(dir != NULL)
            throw LuceneException(LuceneException::Codes::INDEX_ALREADY_EXISTS);

        if(this->index_path == "")
        {
            dir = this->directories[name] = new RAMDirectory();
        }
        else
        {
            std::string full_path = this->index_path + name;
            if ( IndexReader::indexExists(full_path.c_str()) && IndexReader::isLocked(full_path.c_str()))
            {
                IndexReader::unlock(full_path.c_str());
            }
            dir = this->directories[name] = FSDirectory::getDirectory(full_path.c_str(), true);
        }
        return dir;
    };

    Directory* LuceneEngine::get_directory(const std::string& name)
    {
        Directory* dir = this->directories[name];
        if(dir == NULL)
        {
            throw LuceneException(LuceneException::Codes::NO_INDEX);
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

        Query* parsedQuery = QueryParser::parse(wquery.c_str(), L"__id", &this->default_writing_analyzer);

        Hits* hits = searcher.search(parsedQuery);

        QueryResult* result = new QueryResult();

        result->num_results = hits->length();

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

            results.append(data_val);
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

    int LuceneEngine::index_position(const std::string& index)
    {
        Directory* dir = this->get_directory(index);
        IndexSearcher searcher(dir);
        Term checkpointTerm(L"__id", L"checkpoint");
        TermQuery* query = new TermQuery(&checkpointTerm);

        Hits* hits = searcher.search((Query*)query);

        if(hits->length() == 0)
        {
            throw LuceneException(LuceneException::Codes::NO_INDEX);
        }

        Document &checkpointDoc = hits->doc(0);
        Field* checkpointField = checkpointDoc.getField(L"__value");
        const ValueArray<uint8_t>& bytes = *checkpointField->binaryValue();

        // Obviously not platform independent but there is probably a more official
        // way of extracting the binary data from this crap anyway
        int result = 0;
        uint8_t* pointer = reinterpret_cast<uint8_t*>(&result);
        for(int i = 0 ; i < 4 ; i++) {
            pointer[i] = bytes[i];
        }

        _CLLDELETE(query);
        _CLLDELETE(hits);
        searcher.close();
        return result;
    }

    void LuceneEngine::flush(const std::string& index, int position)
    {
        this->log(std::string("Flushing ") + index);
        int* buffer = new int[1];
        buffer[0] = position;
        ValueArray<uint8_t> stored(reinterpret_cast<uint8_t*>(buffer), 4);

        IndexWriter* writer = this->get_writer(index);
        Document checkpointDocument;
        checkpointDocument.add(*(new Field(L"__id",  L"checkpoint" , Field::Store::STORE_NO | Field::Index::INDEX_UNTOKENIZED)));
        checkpointDocument.add(*(new Field(L"__value", &stored, Field::Store::STORE_YES | Field::Index::INDEX_NO)));

        Term deleteTerm(L"__id", L"checkpoint");
        writer->updateDocument(&deleteTerm,  &checkpointDocument);
        writer->flush();
    }

    void LuceneEngine::log(const std::string& msg)
    {
        std::cout << msg << std::endl;
    }

}
