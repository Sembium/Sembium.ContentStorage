﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sembium.ContentStorage.Replication.Replicator.CompleteMoment
{
    public class FileStoredCompleteMomentConfig : IFileStoredCompleteMomentConfig
    {
        public string FileName { get; }
        public int ExpiryMinutes { get; }

        public FileStoredCompleteMomentConfig(string fileName, int expiryMinutes)
        {
            FileName = fileName;
            ExpiryMinutes = expiryMinutes;
        }

    }
}
