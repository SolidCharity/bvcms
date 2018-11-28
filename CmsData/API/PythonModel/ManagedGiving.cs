﻿using System.Linq;
using UtilityExtensions;

namespace CmsData
{
    public partial class PythonModel
    {
        public void SetRecurringGivingEnabled(int peopleId, int fundId, bool enable)
        {
            using (var db2 = NewDataContext())
            {
                RecurringAmount ra = (from m in db2.RecurringAmounts
                                      where m.PeopleId == peopleId
                                      where m.FundId == fundId
                                      select m).SingleOrDefault();
                if (ra != null)
                {                
                    if (enable == true)
                    {
                        ra.Disabled = false;
                        db2.SubmitChanges();
                    }
                    else
                    {
                        ra.Disabled = true;
                        db2.SubmitChanges();
                    }
                }
            }
        }
    }
}
