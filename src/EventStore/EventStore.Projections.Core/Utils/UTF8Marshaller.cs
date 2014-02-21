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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace EventStore.Projections.Core.Utils
{
	public class UTF8Marshaler : ICustomMarshaler {
		static UTF8Marshaler _instance;

		public IntPtr MarshalManagedToNative(object managedObj) {
			if (managedObj == null)
				return IntPtr.Zero;
			if (!(managedObj is string))
				throw new MarshalDirectiveException(
					  "UTF8Marshaler must be used on a string.");
			byte[] strbuf = Encoding.UTF8.GetBytes((string)managedObj); 
			IntPtr buffer = Marshal.AllocHGlobal(strbuf.Length + 1);
			Marshal.Copy(strbuf, 0, buffer, strbuf.Length);
			Marshal.WriteByte(buffer + strbuf.Length, 0); 
			return buffer;
		}

		public object MarshalNativeToManaged(IntPtr nativeData) {
			throw new NotSupportedException("This would require unsafe code");
		}

		public void CleanUpNativeData(IntPtr nativeData) {
			Marshal.FreeHGlobal(nativeData);            
		}

		public void CleanUpManagedData(object managedObj) {
		}

		public int GetNativeDataSize() {
			return -1;
		}

		public static ICustomMarshaler GetInstance(string cookie) {
			return _instance ?? (_instance = new UTF8Marshaler());
		}
	}
}
