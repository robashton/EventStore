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

        private void NativeLogHandler(string message)
        {
            _logger.Info("Message from native lucene: {0}", message);
        }

		private Lucene() 
		{

		}

		private void Initialize()
		{
            _indexingHandle = Js1.OpenIndexingSystem("Indexes", NativeLogHandler);
		}

		public static Lucene Create()
		{
			var lucene = new Lucene();
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
			Js1.HandleIndexCommand(_indexingHandle.Value, ev, data);
		}

		public string Query(string index, string query) 
		{
			IntPtr? result = null;
			NativeQueryResult unpackedResult;
			Byte[] unpackedJson;

			try
			{
				result = Js1.CreateIndexQueryResult(_indexingHandle.Value, index, query);
				unpackedResult = (NativeQueryResult)Marshal.PtrToStructure(result.Value, typeof(NativeQueryResult));
				unpackedJson = new Byte[unpackedResult.num_bytes];
				Marshal.Copy(unpackedResult.json, unpackedJson, 0, unpackedResult.num_bytes);
				return Encoding.UTF8.GetString(unpackedJson);
			}
			finally
			{
				if(result != null)
				  Js1.FreeIndexQueryResult(_indexingHandle.Value, result.Value);
			}
		}

		public void Flush() 
		{
			Js1.FlushIndexingSystem(_indexingHandle.Value);
		}

		public void Dispose() 
		{
			if(_indexingHandle != null) 
			{
				Js1.CloseIndexingSystem(_indexingHandle.Value);
				_indexingHandle = null;
			}
		}
	}
}
