﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sembium.ContentStorage.Service.Security
{
    public interface IContentStorageSystemAccountProvider
    {
        IContentStorageAccount GetAccount();
    }
}
