﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sembium.ContentStorage.Misc.Utils
{
    public class UserAuthorizationException : UserException
    {
        public UserAuthorizationException(string message) : base(message)
        {
        }
    }
}
