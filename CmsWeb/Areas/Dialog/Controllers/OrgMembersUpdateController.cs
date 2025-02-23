using CmsWeb.Areas.Dialog.Models;
using CmsWeb.Lifecycle;
using System;
using System.Web.Mvc;

namespace CmsWeb.Areas.Dialog.Controllers
{
    // todo: use bootstrap
    [RouteArea("Dialog", AreaPrefix = "OrgMembersUpdate"), Route("{action}")]
    public class OrgMembersUpdateController : CmsStaffController
    {
        public OrgMembersUpdateController(IRequestManager requestManager) : base(requestManager)
        {
        }

        [Route("~/OrgMembersUpdate/{qid:guid}")]
        public ActionResult Index(Guid qid)
        {
            var m = new OrgMembersUpdate(qid, CurrentDatabase.Host);
            return View(m);
        }
        [HttpPost, Route("Update")]
        public ActionResult Update(OrgMembersUpdate m)
        {
            m.Host = CurrentDatabase.Host;
            m.Update();
            return View("Updated", m);
        }
        [HttpPost, Route("SmallGroups")]
        public ActionResult SmallGroups(OrgMembersUpdate m)
        {
            m.Host = CurrentDatabase.Host;
            return View(m);
        }
        [HttpPost, Route("AddSmallGroup/{sgid:int}")]
        public ActionResult AddSmallGroup(int sgid, OrgMembersUpdate m)
        {
            m.Host = CurrentDatabase.Host;
            ViewBag.numberadded = m.AddSmallGroup(sgid);
            return View("SmallGroups", m);
        }
        [HttpPost, Route("RemoveSmallGroup/{sgid:int}")]
        public ActionResult RemoveSmallGroup(int sgid, OrgMembersUpdate m)
        {
            m.Host = CurrentDatabase.Host;
            m.RemoveSmallGroup(sgid);
            return View("SmallGroups", m);
        }
        [HttpPost, Route("AddNewSmallGroup")]
        public ActionResult AddNewSmallGroup(OrgMembersUpdate m)
        {
            m.Host = CurrentDatabase.Host;
            m.AddNewSmallGroup();
            ModelState.Clear();
            return View("SmallGroups", m);
        }
        [HttpPost, Route("AddTransaction")]
        public ActionResult AddTransaction(OrgMembersUpdate m)
        {
            m.Host = CurrentDatabase.Host;
            return View(m);
        }
        [HttpPost, Route("AddFeeAdjustment")]
        public ActionResult AddFeeAdjustment(OrgMembersUpdate m)
        {
            m.Host = CurrentDatabase.Host;
            m.AdjustFee = true;
            return View(m);
        }
        [HttpPost, Route("PostTransactions")]
        public ActionResult PostTransactions(OrgMembersUpdate m)
        {
            m.Host = CurrentDatabase.Host;
            if (!ModelState.IsValid)
            {
                return View("AddTransaction", m);
            }

            m.PostTransactions();
            return View("AddTransactionDone", m);
        }

        protected override void OnException(ExceptionContext filterContext)
        {
            if (filterContext.ExceptionHandled)
            {
                return;
            }

            filterContext.Result = Message2(filterContext.Exception.Message);
            filterContext.ExceptionHandled = true;
        }
    }
}
