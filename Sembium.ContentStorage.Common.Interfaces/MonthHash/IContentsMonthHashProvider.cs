﻿using Sembium.ContentStorage.Storage.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sembium.ContentStorage.Common.MonthHash
{
    public interface IContentsMonthHashProvider
    {
        IEnumerable<IMonthHashAndCount> GetMonthHashAndCounts(IEnumerable<IContentIdentifier> contentIdentifiers);
    }
}
