﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sembium.ContentStorage.Common.MonthHash
{
    public interface IMonthHashAndCount
    {
        DateTimeOffset Month { get; }
        byte[] Hash { get; }
        int Count { get; }
        DateTimeOffset? LastModifiedMoment { get; }
    }
}
