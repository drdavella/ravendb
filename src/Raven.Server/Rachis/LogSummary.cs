﻿using System;
using System.Collections.Generic;
using Sparrow.Json;
using Sparrow.Json.Parsing;

namespace Raven.Server.Rachis
{
    public sealed class LogSummary : IDynamicJsonValueConvertible
    {
        public DateTime? LastCommitedTime { get; set; }
        public DateTime? LastAppendedTime { get; set; }
        public long CommitIndex { get; set; }
        public long LastTruncatedIndex { get; set; }
        public long LastTruncatedTerm { get; set; }
        public long FirstEntryIndex { get; set; }
        public long LastLogEntryIndex { get; set; }
        public IEnumerable<RachisConsensus.RachisDebugLogEntry> Logs { get; set; }
        public DynamicJsonValue ToJson()
        {
            return new DynamicJsonValue
            {
                [nameof(LastAppendedTime)] = LastAppendedTime,
                [nameof(LastCommitedTime)] = LastCommitedTime,
                [nameof(CommitIndex)] = CommitIndex,
                [nameof(LastTruncatedIndex)] = LastTruncatedIndex,
                [nameof(LastTruncatedTerm)] = LastTruncatedTerm,
                [nameof(FirstEntryIndex)] = FirstEntryIndex,
                [nameof(LastLogEntryIndex)] = LastLogEntryIndex,
                [nameof(Logs)] = new DynamicJsonArray(Logs)
            };
        }
    }
}
