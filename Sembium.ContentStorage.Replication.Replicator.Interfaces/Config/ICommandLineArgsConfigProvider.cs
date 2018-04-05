﻿using Sembium.ContentStorage.Replication.Common.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sembium.ContentStorage.Replication.Replicator.Config
{
    public interface ICommandLineArgsConfigProvider
    {
        IConfig GetConfig(IEnumerable<string> args);
    }
}
