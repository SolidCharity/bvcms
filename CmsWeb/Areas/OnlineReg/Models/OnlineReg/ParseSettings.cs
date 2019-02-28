using CmsData;
using CmsData.Registration;
using System;
using System.Collections.Generic;
using System.Web;
using UtilityExtensions;

namespace CmsWeb.Areas.OnlineReg.Models
{
    public partial class OnlineRegModel
    {
        public void ParseSettings()
        {
            var list = new Dictionary<int, Settings>();
            if (masterorgid.HasValue)
            {
                foreach (var o in UserSelectedClasses(masterorg, CurrentDatabase))
                {
                    list[o.OrganizationId] = CurrentDatabase.CreateRegistrationSettings(o.OrganizationId);
                }

                list[masterorg.OrganizationId] = CurrentDatabase.CreateRegistrationSettings(masterorg.OrganizationId);
            }
            else if (_orgid == null)
            {
                return;
            }
            else if (org != null)
            {
                list[_orgid.Value] = CurrentDatabase.CreateRegistrationSettings(_orgid.Value);
            }

            HttpContext.Current.Items["RegSettings"] = list; //todo: Get from <see ref="IRequestManager.CurrentHttpContext">

            if (org == null || !org.AddToSmallGroupScript.HasValue())
            {
                return;
            }

            var script = CurrentDatabase.Content(org.AddToSmallGroupScript);
            if (script == null || !script.Body.HasValue())
            {
                return;
            }

            Log("Script:" + org.AddToSmallGroupScript);
            try
            {
                var pe = new PythonModel(Util.Host, "RegisterEvent", script.Body);
                HttpContext.Current.Items["PythonEvents"] = pe;
            }
            catch (Exception ex)
            {
                Log("PythonError");
                org.AddToExtraText("Python.errors", ex.Message);
                throw;
            }
        }
    }
}
