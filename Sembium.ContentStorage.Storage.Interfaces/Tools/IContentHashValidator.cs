﻿using Sembium.ContentStorage.Storage.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sembium.ContentStorage.Storage.Tools
{
    public interface IContentHashValidator
    {
        void ValidateHash(IContent content, string hash);
    }
}
