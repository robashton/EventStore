// Copyright (c) 2012, Event Store LLP // All rights reserved.
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

using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.v8;
using EventStore.Common.Log;
using System.Runtime.InteropServices;
using System;
using System.Text;

namespace EventStore.Projections.Core.Indexing
{
    public class Lucene
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct NativeQueryResult
        {
            public IntPtr json;
            public int num_results;
            public int num_bytes;
        }

        private IntPtr? _indexingHandle;
        private readonly Js1.LogDelegate _logHandler;
        private readonly ILogger _logger;
        private readonly string _indexPath;

        private void NativeLogHandler(string message)
        {
            _logger.Info("Message from native lucene: {0}", message);
        }

        private Lucene(string indexPath)
        {
            _indexPath = indexPath;
        }

        private void Initialize()
        {
            int lastStatus = 0;
            _indexingHandle = Js1.OpenIndexingSystem(_indexPath, NativeLogHandler, ref lastStatus);
            CheckForError(lastStatus);
        }

        public static Lucene Create(string indexPath)
        {
            var lucene = new Lucene(indexPath);
            try
            {
                lucene.Initialize();
            }
            catch(Exception ex)
            {
                lucene.Dispose();
                throw ex;
            }
            return lucene;
        }

        public void Write(string ev, string data)
        {
            int lastStatus = 0;
            Js1.HandleIndexCommand(_indexingHandle.Value, ev, data, ref lastStatus);
            CheckForError(lastStatus);
        }

        public string Query(string index, string query)
        {
            IntPtr? result = null;
            NativeQueryResult unpackedResult;
            Byte[] unpackedJson;
            int lastStatus = 0;

            try
            {
                result = Js1.CreateIndexQueryResult(_indexingHandle.Value, index, query, ref lastStatus);
                CheckForError(lastStatus);
                unpackedResult = (NativeQueryResult)Marshal.PtrToStructure(result.Value, typeof(NativeQueryResult));
                unpackedJson = new Byte[unpackedResult.num_bytes];
                Marshal.Copy(unpackedResult.json, unpackedJson, 0, unpackedResult.num_bytes);
                return Encoding.UTF8.GetString(unpackedJson);
            }
            finally
            {
                if(result != null && result != IntPtr.Zero)
                {
                  Js1.FreeIndexQueryResult(_indexingHandle.Value, result.Value, ref lastStatus);
                  CheckForError(lastStatus);
                }

            }
        }

        public int IndexPosition(string index)
        {
            int lastStatus = 0;
            var result = Js1.IndexPosition(_indexingHandle.Value, index, ref lastStatus);
            CheckForError(lastStatus);
            return result;
        }

        public void Flush(string index, int lastEventNumber)
        {
            int lastStatus = 0;
            Js1.FlushIndexingSystem(_indexingHandle.Value, index, lastEventNumber, ref lastStatus);
            CheckForError(lastStatus);
        }

        public void Dispose()
        {
            int lastStatus = 0;
            if(_indexingHandle != null)
            {
                Js1.CloseIndexingSystem(_indexingHandle.Value, ref lastStatus);
                CheckForError(lastStatus);
                _indexingHandle = null;
            }
        }

        private void CheckForError(int status)
        {
//            enum Codes { NO_INDEX = 1, INDEX_ALREADY_EXISTS = 2 , LUCENE_ERROR = 3 };
            switch(status)
            {
                case 0:
                    return;
                case 1:
                    throw new LuceneException("No index with this name exists");
                    break;
                case 2:
                    throw new LuceneException("Index already exists");
                    break;
                case 3:
                    throw new LuceneException("Lucene threw an error, I don't know why");
                    break;

            }
        }
    }
}
