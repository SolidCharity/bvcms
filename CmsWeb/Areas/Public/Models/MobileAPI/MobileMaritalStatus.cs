﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CmsWeb.MobileAPI
{
	public class MobileMaritalStatus
	{
		public int id = 0;
		public string code = "";
		public string description = "";

		public MobileMaritalStatus populate(CmsData.MaritalStatus status)
		{
			id = status.Id;
			code = status.Code;
			description = status.Description;
			
			return this;
		}
	}
}