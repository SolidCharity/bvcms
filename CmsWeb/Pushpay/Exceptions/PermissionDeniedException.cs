﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CmsWeb.Pushpay.Exceptions
{
    public class PermissionDeniedException : UserException
    {
        public PermissionDeniedException() : base("You do not have permission to perform this action")
        {
        }

        public PermissionDeniedException(string msg) : base(msg)
        {
        }
    }
}
