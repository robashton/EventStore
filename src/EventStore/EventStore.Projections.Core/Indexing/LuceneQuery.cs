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

namespace EventStore.Projections.Core.Indexing
{
	public class LuceneQuery : IDisposable
	{
		[StructLayout(LayoutKind.Sequential)]
		public struct NativeQueryResult
		{
			public IntPtr json;
			public int num_bytes;
		}

		IntPtr? _data;
		private NativeQueryResult _result;
		private readonly IntPtr _handle;
		private readonly string _index;
		private readonly string _query; 

		public string Result
		{
			get { return "This is not a result"; }
		}

		public LuceneQuery(IntPtr handle, string index, string query)
		{
			_handle = handle;
			_index = index;
			_query = query;
		}

		public void Execute()
		{
			_data = Js1.CreateIndexQueryResult(_handle, _index, _query);
			_result = (NativeQueryResult)Marshal.PtrToStructure(_data.Value, typeof(NativeQueryResult));
			Console.WriteLine("GOT A RESULT {0}", _result.num_bytes);
		}

		public void Dispose()
		{
			if(_data != null)
			{
				Js1.FreeIndexQueryResult(_handle, _data.Value);
				_data = null;
			}
		}

		~LuceneQuery()
		{
			this.Dispose();
		}
	}
}
