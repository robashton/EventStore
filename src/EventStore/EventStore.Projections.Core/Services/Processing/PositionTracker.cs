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

namespace EventStore.Projections.Core.Services.Processing
{
    public class PositionTracker
    {
        private readonly PositionTagger _positionTagger;
        private CheckpointTag _lastTag = null;

        public PositionTracker(PositionTagger positionTagger)
        {
            _positionTagger = positionTagger;
        }

        public CheckpointTag LastTag
        {
            get { return _lastTag; }
        }

        public void UpdateByCheckpointTagForward(CheckpointTag newTag)
        {
            if (_lastTag == null)
                throw new InvalidOperationException("Initial position was not set");
            if (newTag <= _lastTag)
                throw new InvalidOperationException(
                    string.Format("Event at checkpoint tag {0} has been already processed", newTag));
            InternalUpdate(newTag);
        }

        public void UpdateByCheckpointTagInitial(CheckpointTag checkpointTag)
        {
            if (_lastTag != null)
                throw new InvalidOperationException("Position tagger has be already updated");
            InternalUpdate(checkpointTag);
        }

        private void InternalUpdate(CheckpointTag newTag)
        {
            if (!_positionTagger.IsCompatible(newTag))
                throw new InvalidOperationException("Cannot update by incompatible checkpoint tag");
            _lastTag = newTag;
        }

        public void Initialize()
        {
            _lastTag = null;
        }

    }
}