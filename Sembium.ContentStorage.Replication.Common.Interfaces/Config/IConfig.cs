﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sembium.ContentStorage.Replication.Common.Config
{
    public interface IConfig
    {
        IEnumerable<IRouteConfig> RouteConfigs { get; }
        string LogFileName { get; }
    }
}
