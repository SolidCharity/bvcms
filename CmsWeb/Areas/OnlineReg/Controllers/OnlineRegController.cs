using CmsData;
using CmsData.API;
using CmsData.Classes;
using CmsData.Codes;
using CmsData.Registration;
using CmsWeb.Areas.OnlineReg.Models;
using CmsWeb.Code;
using CmsWeb.Lifecycle;
using CmsWeb.Models;
using CmsWeb.Services.RogueIpService;
using Dapper;
using Elmah;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;
using System.Xml.Linq;
using UtilityExtensions;

namespace CmsWeb.Areas.OnlineReg.Controllers
{
    [ValidateInput(false)]
    [RouteArea("OnlineReg", AreaPrefix = "OnlineReg"), Route("{action}/{id?}")]
    public partial class OnlineRegController : CmsController
    {
        #region Private Fields

        private readonly IRogueIpService _rogueIpService;

        #endregion

        #region Constructors

        public OnlineRegController(IRequestManager requestManager, IRogueIpService rogueIpService)
            : base(requestManager)
        {
            _rogueIpService = rogueIpService;
        }

        #endregion

        #region Local Model Classes

        public class ConfirmTestInfo
        {
            public RegistrationDatum ed;
            public OnlineRegModel m;
        }

        public class TransactionTestInfo
        {
            public RegistrationDatum ed;
            public TransactionInfo ti;
        }

        public class LinkInfo
        {
            private readonly CMSDataContext _dataContext;
            private readonly string from;
            private readonly string link;

            internal string[] a;
            internal string error;
            internal int? oid;
            internal OneTimeLink ot;
            internal int? pid;

            public LinkInfo(CMSDataContext dataContext, string link, string from, string id, bool hasorg = true)
            {
                _dataContext = dataContext;
                this.link = link;
                this.from = from;

                try
                {
                    if (!id.HasValue())
                    {
                        throw LinkException("missing id");
                    }

                    var guid = id.ToGuid();
                    if (guid == null)
                    {
                        throw LinkException("invalid id");
                    }

                    ot = _dataContext.OneTimeLinks.SingleOrDefault(oo => oo.Id == guid.Value);
                    if (ot == null)
                    {
                        throw LinkException("missing link");
                    }

                    a = ot.Querystring.SplitStr(",", 5);
                    if (hasorg)
                    {
                        oid = a[0].ToInt();
                    }

                    pid = a[1].ToInt();
#if !DEBUG
                    if (ot.Used) {
                        throw LinkException("link used");
                    }
                    if (ot.Expires.HasValue && ot.Expires < DateTime.Now) {
                        throw LinkException("link expired");
                    }
#endif
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }
            }

            internal Exception LinkException(string msg)
            {
                _dataContext.LogActivity($"{link}{@from}Error: {msg}", oid, pid);
                return new Exception(msg);
            }
        }


        #endregion

        #region Private Methods

        private string EleVal(XElement r, string name)
        {
            var e = r.Element(name);
            if (e != null)
            {
                return e.Value;
            }

            return null;
        }

        #endregion

        #region ConfirmTest

        [Authorize(Roles = "Admin")]
        public ActionResult ConfirmTest(int? start, int? count)
        {
            IEnumerable<RegistrationDatum> q;
            q = from ed in CurrentDatabase.RegistrationDatas
                orderby ed.Stamp descending
                select ed;
            var list = q.Skip(start ?? 0).Take(count ?? 200).ToList();
            var q2 = new List<ConfirmTestInfo>();
            foreach (var ed in list)
            {
                try
                {
                    var m = Util.DeSerialize<OnlineRegModel>(ed.Data);
                    var i = new ConfirmTestInfo
                    {
                        ed = ed,
                        m = m
                    };
                    q2.Add(i);
                }
                catch (Exception)
                {
                }
            }
            return View("Other/ConfirmTest", q2);
        }

        [Authorize(Roles = "Admin")]
        public ActionResult ConfirmTestXml(int id)
        {
            var rd = (from i in CurrentDatabase.RegistrationDatas
                      where i.Id == id
                      select i).SingleOrDefault();
            if (rd == null)
            {
                return Content("no data");
            }

            return Content(rd.Data, contentType: "text/xml");
        }

        [Authorize(Roles = "ManageTransactions,Finance,Admin")]
        public ActionResult RegPeople(int id)
        {
            var q = from i in CurrentDatabase.RegistrationDatas
                    where i.Id == id
                    select i;
            if (!q.Any())
            {
                return Content("no data");
            }

            var q2 = new List<ConfirmTestInfo>();
            foreach (var ed in q)
            {
                try
                {
                    var m = Util.DeSerialize<OnlineRegModel>(ed.Data);
                    m.Datum = ed;
                    var i = new ConfirmTestInfo
                    {
                        ed = ed,
                        m = m
                    };
                    q2.Add(i);
                }
                catch (Exception)
                {
                }
            }
            return View("Other/RegPeople", q2[0].m);
        }

        [HttpPost]
        [Authorize(Roles = "ManageTransactions,Finance,Admin")]
        public ActionResult DeleteRegData(int id)
        {
            var ed = (from i in CurrentDatabase.RegistrationDatas
                      where i.Id == id
                      select i).Single();
            CurrentDatabase.RegistrationDatas.DeleteOnSubmit(ed);
            CurrentDatabase.SubmitChanges();
            return Content("ok");
        }

        #endregion

        #region Coupon

        [HttpPost]
        public ActionResult ApplyCoupon(PaymentForm pf)
        {
            OnlineRegModel m = null;
            if (pf.PayBalance == false)
            {
                m = OnlineRegModel.GetRegistrationFromDatum(pf.DatumId);
                if (m == null)
                {
                    return Json(new { error = "coupon not find your registration" });
                }

                m.ParseSettings();
            }

            if (!pf.Coupon.HasValue())
            {
                return Json(new { error = "empty coupon" });
            }

            var coupon = pf.Coupon.ToUpper().Replace(" ", "");
            var admincoupon = CurrentDatabase.Setting("AdminCoupon", "ifj4ijweoij").ToUpper().Replace(" ", "");
            if (coupon == admincoupon)
            {
                if (pf.PayBalance)
                {
                    var tic = pf.CreateTransaction(CurrentDatabase, pf.AmtToPay);
                    return Json(new { confirm = $"/onlinereg/ConfirmDuePaid/{tic.Id}?TransactionID=AdminCoupon&Amount={tic.Amt}" });
                }
                else
                {
                    return Json(new { confirm = $"/OnlineReg/Confirm/{pf.DatumId}?TransactionId=AdminCoupon" });
                }
            }

            var c = CurrentDatabase.Coupons.SingleOrDefault(cp => cp.Id == coupon);
            if (c == null)
            {
                return Json(new { error = "coupon not found" });
            }

            if (pf.OrgId.HasValue && c.Organization != null && c.Organization.OrgPickList.HasValue())
            {
                var a = c.Organization.OrgPickList.SplitStr(",").Select(ss => ss.ToInt()).ToArray();
                if (!a.Contains(pf.OrgId.Value))
                {
                    return Json(new { error = "coupon and org do not match" });
                }
            }
            else if (pf.OrgId != c.OrgId)
            {
                return Json(new { error = "coupon and org do not match" });
            }

            if (c.Used.HasValue && c.Id.Length == 12)
            {
                return Json(new { error = "coupon already used" });
            }

            if (c.Canceled.HasValue)
            {
                return Json(new { error = "coupon canceled" });
            }

            var ti = pf.CreateTransaction(CurrentDatabase, Math.Min(c.Amount ?? 0m, pf.AmtToPay ?? 0m));
            if (m != null) // Start this transaction in the chain
            {
                m.HistoryAdd("ApplyCoupon");
                m.TranId = ti.OriginalId;
                m.UpdateDatum();
            }
            var tid = $"Coupon({Util.fmtcoupon(coupon)})";

            if (!pf.PayBalance)
            {
                OnlineRegModel.ConfirmDuePaidTransaction(ti, tid, false);
            }

            var msg = $"<i class='red'>Your coupon for {c.Amount:n2} has been applied, your balance is now {ti.Amtdue:n2}</i>.";
            if (ti.Amt < pf.AmtToPay)
            {
                msg += "You still must complete this transaction with a payment";
            }

            if (m != null)
            {
                m.UseCoupon(ti.TransactionId, ti.Amt ?? 0);
            }
            else
            {
                c.UseCoupon(ti.FirstTransactionPeopleId(), ti.Amt ?? 0);
            }

            CurrentDatabase.SubmitChanges();

            if (pf.PayBalance)
            {
                return Json(new { confirm = $"/onlinereg/ConfirmDuePaid/{ti.Id}?TransactionID=Coupon({Util.fmtcoupon(coupon)})&Amount={ti.Amt}" });
            }

            pf.AmtToPay -= ti.Amt;
            if (pf.AmtToPay <= 0)
            {
                return Json(new { confirm = $"/OnlineReg/Confirm/{pf.DatumId}?TransactionId={"Coupon"}" });
            }

            return Json(new { tiamt = pf.AmtToPay, amtdue = ti.Amtdue, amt = pf.AmtToPay.ToString2("N2"), msg });
        }

        [HttpPost]
        public ActionResult PayWithCoupon(int id, string Coupon)
        {
            if (!Coupon.HasValue())
            {
                return Json(new { error = "empty coupon" });
            }

            var m = OnlineRegModel.GetRegistrationFromDatum(id);
            m.ParseSettings();
            var coupon = Coupon.ToUpper().Replace(" ", "");
            var admincoupon = CurrentDatabase.Setting("AdminCoupon", "ifj4ijweoij").ToUpper().Replace(" ", "");
            if (coupon == admincoupon)
            {
                return Json(new { confirm = $"/onlinereg/Confirm/{id}?TransactionID=Coupon(Admin)" });
            }

            var c = CurrentDatabase.Coupons.SingleOrDefault(cp => cp.Id == coupon);
            if (c == null)
            {
                return Json(new { error = "coupon not found" });
            }

            if (m.Orgid != c.OrgId)
            {
                return Json(new { error = "coupon org not match" });
            }

            if (DateTime.Now.Subtract(c.Created).TotalHours > 24)
            {
                return Json(new { error = "coupon expired" });
            }

            if (c.Used.HasValue && c.Id.Length == 12)
            {
                return Json(new { error = "coupon already used" });
            }

            if (c.Canceled.HasValue)
            {
                return Json(new { error = "coupon canceled" });
            }

            return Json(new
            {
                confirm = $"/onlinereg/confirm/{id}?TransactionID=Coupon({Util.fmtcoupon(coupon)})"
            });
        }

        [HttpPost]
        public ActionResult PayWithCoupon2(int id, string Coupon, decimal Amount)
        {
            if (!Coupon.HasValue())
            {
                return Json(new { error = "empty coupon" });
            }

            var ti = CurrentDatabase.Transactions.SingleOrDefault(tt => tt.Id == id);
            var coupon = Coupon.ToUpper().Replace(" ", "");
            var admincoupon = CurrentDatabase.Setting("AdminCoupon", "ifj4ijweoij").ToUpper().Replace(" ", "");
            if (coupon == admincoupon)
            {
                return Json(new { confirm = $"/onlinereg/ConfirmDuePaid/{id}?TransactionID=Coupon(Admin)&Amount={Amount}" });
            }

            var c = CurrentDatabase.Coupons.SingleOrDefault(cp => cp.Id == coupon);
            if (c == null)
            {
                return Json(new { error = "coupon not found" });
            }

            if (ti.OrgId != c.OrgId)
            {
                return Json(new { error = "coupon org not match" });
            }

            if (DateTime.Now.Subtract(c.Created).TotalHours > 24)
            {
                return Json(new { error = "coupon expired" });
            }

            if (c.Used.HasValue)
            {
                return Json(new { error = "coupon already used" });
            }

            if (c.Canceled.HasValue)
            {
                return Json(new { error = "coupon canceled" });
            }

            if (c.Amount.HasValue)
            {
                Amount = c.Amount.Value;
            }

            return Json(new
            {
                confirm = $"/onlinereg/ConfirmDuePaid/{id}?TransactionID=Coupon({Util.fmtcoupon(coupon)})&Amount={Amount}"
            });
        }

        // todo: I hope we can get rid of this method
        [HttpPost]
        public ActionResult PayWithCouponOld(int id, string Coupon, decimal Amount)
        {
            if (!Coupon.HasValue())
            {
                return Json(new { error = "empty coupon" });
            }

            var ed = CurrentDatabase.ExtraDatas.SingleOrDefault(e => e.Id == id);
            var ti = Util.DeSerialize<TransactionInfo>(ed.Data.Replace("CMSWeb.Models", "CmsWeb.Models"));
            var coupon = Coupon.ToUpper().Replace(" ", "");
            var admincoupon = CurrentDatabase.Setting("AdminCoupon", "ifj4ijweoij").ToUpper().Replace(" ", "");
            if (coupon == admincoupon)
            {
                return Json(new { confirm = $"/onlinereg/Confirm2/{id}?TransactionID=Coupon(Admin)&Amount={Amount}" });
            }

            var c = CurrentDatabase.Coupons.SingleOrDefault(cp => cp.Id == coupon);
            if (c == null)
            {
                return Json(new { error = "coupon not found" });
            }

            if (ti.orgid != c.OrgId)
            {
                return Json(new { error = "coupon org not match" });
            }

            if (DateTime.Now.Subtract(c.Created).TotalHours > 24)
            {
                return Json(new { error = "coupon expired" });
            }

            if (c.Used.HasValue)
            {
                return Json(new { error = "coupon already used" });
            }

            if (c.Canceled.HasValue)
            {
                return Json(new { error = "coupon canceled" });
            }

            return Json(new
            {
                confirm = $"/onlinereg/Confirm2/{id}?TransactionID=Coupon({Util.fmtcoupon(coupon)})&Amount={Amount}"
            });
        }


        #endregion

        #region ManageGiving

        public ActionResult ManagePledge(string id)
        {
            if (!id.HasValue())
            {
                return Content("bad link");
            }

            ManagePledgesModel m = null;
            var td = TempData["PeopleId"];
            if (td != null)
            {
                m = new ManagePledgesModel(td.ToInt(), id.ToInt());
            }
            else
            {
                var guid = id.ToGuid();
                if (guid == null)
                {
                    return Content("invalid link");
                }

                var ot = CurrentDatabase.OneTimeLinks.SingleOrDefault(oo => oo.Id == guid.Value);
                if (ot == null)
                {
                    return Content("invalid link");
                }

                if (ot.Used)
                {
                    return Content("link used");
                }

                if (ot.Expires.HasValue && ot.Expires < DateTime.Now)
                {
                    return Content("link expired");
                }

                var a = ot.Querystring.Split(',');
                m = new ManagePledgesModel(a[1].ToInt(), a[0].ToInt());
                ot.Used = true;
                CurrentDatabase.SubmitChanges();
            }
            SetHeaders(m.orgid);
            m.Log("Start");
            return View("ManagePledge/Index", m);
        }

        [HttpGet]
        public ActionResult ManageGiving(string id, bool? testing, string campus = "", string funds = "")
        {
            if (!id.HasValue())
            {
                return Message("bad link");
            }

            ManageGivingModel m = null;
            var td = TempData["PeopleId"];

            SetCampusAndDefaultFunds(campus, funds);

            funds = Session["DefaultFunds"]?.ToString();

            if (td != null)
            {
                m = new ManageGivingModel(CurrentDatabase, _rogueIpService, td.ToInt(), id.ToInt(), funds);
                if (m.person == null)
                {
                    return Message("person not found");
                }
            }
            else
            {
                var guid = id.ToGuid();
                if (guid == null)
                {
                    return Content("invalid link");
                }

                var ot = CurrentDatabase.OneTimeLinks.SingleOrDefault(oo => oo.Id == guid.Value);
                if (ot == null)
                {
                    return Content("invalid link");
                }
#if !DEBUG2
                if (ot.Used)
                {
                    return Content("link used");
                }
#endif
                if (ot.Expires.HasValue && ot.Expires < DateTime.Now)
                {
                    return Content("link expired");
                }

                var a = ot.Querystring.Split(',');
                m = new ManageGivingModel(CurrentDatabase, _rogueIpService, a[1].ToInt(), a[0].ToInt(), funds);
                if (m.person == null)
                {
                    return Message("person not found");
                }

                ot.Used = true;
                CurrentDatabase.SubmitChanges();
            }
            Session["CreditCardOnFile"] = m.CreditCard;
            Session["ExpiresOnFile"] = m.Expires;
            if (!m.testing)
            {
                m.testing = testing ?? false;
            }

            SetHeaders(m.orgid);
            m.Log("Start");
            return View("ManageGiving/Setup", m);
        }

        [HttpPost]
        public ActionResult ManageGiving(ManageGivingModel m)
        {
            SetHeaders(m.orgid);

            // only validate if the amounts are greater than zero.
            if (m.FundItemsChosen().Sum(f => f.amt) > 0)
            {
                m.ValidateModel(ModelState);
                if (!ModelState.IsValid)
                {
                    if (m.person == null)
                    {
                        return Message("person not found");
                    }

                    m.total = 0;
                    foreach (var ff in m.FundItemsChosen())
                    {
                        m.total += ff.amt;
                    }

                    return View("ManageGiving/Setup", m);
                }
            }
            if (CurrentDatabase.Setting("UseRecaptchaForManageGiving"))
            {
                if (!GoogleRecaptcha.IsValidResponse(HttpContext, CurrentDatabase))
                {
                    ModelState.AddModelError("TranId", "ReCaptcha validation failed.");
                    return View("ManageGiving/Setup", m);
                }
            }

            try
            {
                m.Update();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("form", ex.Message);
            }
            if (!ModelState.IsValid)
            {
                return View("ManageGiving/Setup", m);
            }

            TempData["managegiving"] = m;
            return Redirect("/OnlineReg/ConfirmRecurringGiving");
        }

        public ActionResult ConfirmRecurringGiving()
        {
            var m = TempData["managegiving"] as ManageGivingModel;
            if (m == null)
            {
                return Content("No active registration");
            }

            if (Util.IsDebug())
            {
                m.testing = true;
            }

            if (!m.ManagedGivingStopped)
            {
                m.Confirm(this);
            }

            SetHeaders(m.orgid);
            OnlineRegModel.LogOutOfOnlineReg();

            m.Log("Confirm");
            return View("ManageGiving/Confirm", m);
        }

        [HttpPost]
        public ActionResult ConfirmPledge(ManagePledgesModel m)
        {
            m.Confirm();
            SetHeaders(m.orgid);
            OnlineRegModel.LogOutOfOnlineReg();

            m.Log("Confirm");
            return View("ManagePledge/Confirm", m);
        }

        [HttpPost]
        public ActionResult RemoveManagedGiving(int peopleId, int orgId)
        {
            var m = new ManageGivingModel(CurrentDatabase, _rogueIpService, peopleId, orgId);
            m.CancelManagedGiving(peopleId);
            m.ThankYouMessage = "Your recurring giving has been stopped.";

            m.Log("Remove");
            TempData["managegiving"] = m;
            return Json(new { Url = Url.Action("ConfirmRecurringGiving") });
        }

        private void SetCampusAndDefaultFunds(string campus, string funds)
        {
            if (!string.IsNullOrWhiteSpace(campus))
            {
                Session["Campus"] = campus;
            }
            if (!string.IsNullOrWhiteSpace(funds))
            {
                Session["DefaultFunds"] = funds;
            }
        }

        #endregion

        #region ManageSubscriptions

        public ActionResult ManageSubscriptions(string id)
        {
            if (!id.HasValue())
            {
                return Content("bad link");
            }

            ManageSubsModel m;
            var td = TempData["PeopleId"];
            if (td != null)
            {
                m = new ManageSubsModel(td.ToInt(), id.ToInt());
            }
            else
            {
                var guid = id.ToGuid();
                if (guid == null)
                {
                    return Content("invalid link");
                }

                var ot = CurrentDatabase.OneTimeLinks.SingleOrDefault(oo => oo.Id == guid.Value);
                if (ot == null)
                {
                    return Content("invalid link");
                }

                if (ot.Used)
                {
                    return Content("link used");
                }

                if (ot.Expires.HasValue && ot.Expires < DateTime.Now)
                {
                    return Content("link expired");
                }

                var a = ot.Querystring.Split(',');
                m = new ManageSubsModel(a[1].ToInt(), a[0].ToInt());
                id = a[0];
                ot.Used = true;
                CurrentDatabase.SubmitChanges();
            }
            m.Log("Start");
            SetHeaders(id.ToInt());
            return View("ManageSubscriptions/Choose", m);
        }

        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult ConfirmSubscriptions(ManageSubsModel m)
        {
            m.UpdateSubscriptions();

            var Staff = CurrentDatabase.StaffPeopleForOrg(m.masterorgid);

            var msg = CurrentDatabase.ContentHtml("ConfirmSubscriptions", Resource1.ConfirmSubscriptions);
            var orgname = m.Description();
            msg = msg.Replace("{org}", orgname).Replace("{details}", m.Summary);
            CurrentDatabase.Email(Staff.First().FromEmail, m.person, "Subscription Confirmation", msg);

            CurrentDatabase.Email(m.person.FromEmail, Staff, "Subscriptions managed",
                $@"{m.person.Name} managed subscriptions to {m.Description()}<br/>{m.Summary}");

            SetHeaders(m.masterorgid);
            m.Log("Confirm");
            return View("ManageSubscriptions/Confirm", m);
        }

        #endregion

        #region ManageVolunteers

        private const string Fromcalendar = "fromcalendar";

        [HttpGet]
        [Route("VolRequestReport/{mid:int}/{pid:int}/{ticks:long}")]
        public ActionResult VolRequestReport(int mid, int pid, long ticks)
        {
            var vs = new VolunteerRequestModel(mid, pid, ticks);
            SetHeaders(vs.org.OrganizationId);
            return View("ManageVolunteer/VolRequestReport", vs);
        }

        [HttpGet]
        [Route("VolRequestResponse")]
        [Route("VolRequestResponse/{ans}/{guid}")]
        public ActionResult RequestResponse(string ans, string guid)
        {
            ViewBag.Answer = ans;
            ViewBag.Guid = guid;
            return View("ManageVolunteer/RequestResponse");
        }

        [HttpPost]
        [Route("VolRequestResponse")]
        [Route("VolRequestResponse/{ans}/{guid}")]
        public ActionResult RequestResponse(string ans, string guid, FormCollection formCollection)
        {
            try
            {
                var vs = new VolunteerRequestModel(guid);
                vs.ProcessReply(ans);
                return Content(vs.DisplayMessage);
            }
            catch (Exception ex)
            {
                return Content(ex.Message);
            }
        }

        [HttpGet]
        [Route("GetVolSub/{aid:int}/{pid:int}")]
        public ActionResult GetVolSub(int aid, int pid)
        {
            var token = TempData[Fromcalendar] as bool?;
            if (token == true)
            {
                var vs = new VolSubModel(aid, pid);
                SetHeaders(vs.org.OrganizationId);
                vs.ComposeMessage();
                return View("ManageVolunteer/GetVolSub", vs);
            }
            return Message("Must come to GetVolSub from calendar");
        }

        [HttpPost]
        [ValidateInput(false)]
        public ActionResult GetVolSub(int aid, int pid, long ticks, int[] pids, string subject, string message)
        {
            var m = new VolSubModel(aid, pid, ticks);
            m.subject = subject;
            m.message = message;
            if (pids == null)
            {
                return Content("no emails sent (no recipients were selected)");
            }

            m.pids = pids;
            m.SendEmails();
            return Content("Emails are being sent, thank you.");
        }

        [Route("VolSubReport/{aid:int}/{pid:int}/{ticks:long}")]
        public ActionResult VolSubReport(int aid, int pid, long ticks)
        {
            var vs = new VolSubModel(aid, pid, ticks);
            SetHeaders(vs.org.OrganizationId);
            return View("ManageVolunteer/VolSubReport", vs);
        }

        [Route("ClaimVolSub/{ans}/{guid}")]
        public ActionResult ClaimVolSub(string ans, string guid)
        {
            try
            {
                var vs = new VolSubModel();
                vs.PrepareToClaim(ans, guid);
                ViewBag.Answer = ans;
                ViewBag.Guid = guid;
                return View("ManageVolunteer/ClaimVolSub");
            }
            catch (Exception ex)
            {
                return Message(ex.Message);
            }
        }

        [HttpPost]
        [Route("ClaimVolSub/{ans}/{guid}")]
        public ActionResult ClaimVolSub(string ans, string guid, FormCollection formCollection)
        {
            try
            {
                var vs = new VolSubModel(guid);
                vs.ProcessReply(ans);
                return Content(vs.DisplayMessage);
            }
            catch (Exception ex)
            {
                return Message(ex.Message);
            }
        }

        [Route("ManageVolunteer/{id}")]
        [Route("ManageVolunteer/{id}/{pid:int}")]
        public ActionResult ManageVolunteer(string id, int? pid)
        {
            if (!id.HasValue())
            {
                return Content("bad link");
            }

            VolunteerModel m = null;

            var td = TempData["PeopleId"];
            if (td != null)
            {
                m = new VolunteerModel(id.ToInt(), td.ToInt());
            }
            else if (pid.HasValue)
            {
                var leader = OrganizationMember.VolunteerLeaderInOrg(CurrentDatabase, id.ToInt2());
                if (leader)
                {
                    m = new VolunteerModel(id.ToInt(), pid.Value, true);
                }
            }
            if (m == null)
            {
                var guid = id.ToGuid();
                if (guid == null)
                {
                    return Content("invalid link");
                }

                var ot = CurrentDatabase.OneTimeLinks.SingleOrDefault(oo => oo.Id == guid.Value);
                if (ot == null)
                {
                    return Content("invalid link");
                }
#if !DEBUG2
                if (ot.Used)
                {
                    return Content("link used");
                }
#endif
                if (ot.Expires.HasValue && ot.Expires < DateTime.Now)
                {
                    return Content("link expired");
                }

                var a = ot.Querystring.Split(',');
                m = new VolunteerModel(a[0].ToInt(), a[1].ToInt());
                id = a[0];
                ot.Used = true;
                CurrentDatabase.SubmitChanges();
            }

            SetHeaders(id.ToInt());
            DbUtil.LogActivity($"Pick Slots: {m.Org.OrganizationName} ({m.Person.Name})");
            TempData[Fromcalendar] = true;
            return View("ManageVolunteer/PickSlots", m);
        }

        [HttpPost]
        public ActionResult ConfirmVolunteerSlots(VolunteerModel m)
        {
            m.UpdateCommitments();
            if (m.SendEmail || !m.IsLeader)
            {
                List<Person> Staff = null;
                Staff = CurrentDatabase.StaffPeopleForOrg(m.OrgId);
                var staff = Staff[0];

                var summary = m.Summary(this);
                var text = Util.PickFirst(m.Setting.Body, "confirmation email body not found");
                text = text.Replace("{church}", CurrentDatabase.Setting("NameOfChurch", "church"), true);
                text = text.Replace("{name}", m.Person.Name, true);
                text = text.Replace("{date}", DateTime.Now.ToString("d"), true);
                text = text.Replace("{email}", m.Person.EmailAddress, true);
                text = text.Replace("{phone}", m.Person.HomePhone.FmtFone(), true);
                text = text.Replace("{contact}", staff.Name, true);
                text = text.Replace("{contactemail}", staff.EmailAddress, true);
                text = text.Replace("{contactphone}", m.Org.PhoneNumber.FmtFone(), true);
                text = text.Replace("{details}", summary, true);
                CurrentDatabase.Email(staff.FromEmail, m.Person, m.Setting.Subject, text);

                CurrentDatabase.Email(m.Person.FromEmail, Staff, "Volunteer Commitments managed", $@"{m.Person.Name} managed volunteer commitments to {m.Org.OrganizationName}<br/>
The following Commitments:<br/>
{summary}");
            }
            ViewData["Organization"] = m.Org.OrganizationName;
            SetHeaders(m.OrgId);
            if (m.IsLeader)
            {
                TempData[Fromcalendar] = true;
                return View("ManageVolunteer/PickSlots", m);
            }
            return View("ManageVolunteer/ConfirmVolunteerSlots", m);
        }

        #endregion

        #region OnePageGiving

        [HttpGet]
        [Route("~/OnePageGiving/{id:int}")]
        public ActionResult OnePageGiving(int id, bool? testing, string source)
        {
            Response.NoCache();
            try
            {
                var m = new OnlineRegModel(Request, CurrentDatabase, id, testing, null, null, source);

                var pid = Util.UserPeopleId;
                if (pid.HasValue)
                {
                    PrePopulate(m, pid.Value);
                }

                SetHeaders(m);
                m.CheckRegisterLink(null);

                if (m.NotActive())
                {
                    return View("OnePageGiving/NotActive", m);
                }

                var pf = PaymentForm.CreatePaymentForm(m);
                pf.AmtToPay = null;

                if (string.IsNullOrWhiteSpace(pf.Type))
                {
                    pf.Type = pf.NoCreditCardsAllowed ? "B" : "C";
                }

#if DEBUG
                if (!pid.HasValue)
                {
                    pf.First = "Otis";
                    pf.Last = "Sukamotis";
                    pf.Email = "davcar@pobox.com";
                    pf.Address = "135 Riveredge Cv";
                    pf.Zip = "";
                    pf.CreditCard = "3111111111111111";
                    pf.Expires = "1018";
                    pf.CVV = "123";
                    pf.AmtToPay = 23M;
                }
#endif

                var p = m.List[0];
                if (pf.ShowCampusOnePageGiving)
                {
                    pf.Campuses = p.Campuses().ToList();
                }

                var designatedFund = p.DesignatedDonationFund().FirstOrDefault();
                pf.Description = designatedFund != null ? designatedFund.Text : m.DescriptionForPayment;

                SetInstructions(m);

                return View("OnePageGiving/Index", new OnePageGivingModel() { OnlineRegPersonModel = m.List[0], PaymentForm = pf });
            }
            catch (Exception ex)
            {
                if (ex is BadRegistrationException)
                {
                    return Message(ex.Message);
                }

                throw;
            }
        }

        private void PrePopulate(OnlineRegModel m, int pid)
        {
            m.UserPeopleId = pid;
            var p = m.LoadExistingPerson(pid, 0);
            m.List[0] = p;
        }

        private void SetInstructions(OnlineRegModel m)
        {
            var s = m.SubmitInstructions();
            ViewBag.Instructions = s.HasValue() ? s : $"<h4>{m.Header}</h4>";
        }

        [HttpPost, Route("~/OnePageGiving")]
        public ActionResult OnePageGiving(PaymentForm pf, Dictionary<int, decimal?> fundItem)
        {
            // need save off the original amt to pay if there is an error later on.
            var amtToPay = pf.AmtToPay;

            var id = pf.OrgId;
            if (id == null)
            {
                return Message("Missing OrgId");
            }

            if (!Util.ValidEmail(pf.Email))
            {
                ModelState.AddModelError("Email", "Need a valid email address");
            }

            if (pf.IsUs && !pf.Zip.HasValue())
            {
                ModelState.AddModelError("Zip", "Zip is Required for US");
            }

            if (pf.ShowCampusOnePageGiving)
            {
                if ((pf.CampusId ?? 0) == 0)
                {
                    ModelState.AddModelError("CampusId", "Campus is Required");
                }
            }

            var m = new OnlineRegModel(Request, CurrentDatabase, pf.OrgId, pf.testing, null, null, pf.source)
            {
                URL = $"/OnePageGiving/{pf.OrgId}"
            };

            var pid = Util.UserPeopleId;
            if (pid.HasValue)
            {
                PrePopulate(m, pid.Value);
            }

            // we need to always retrieve the entire list of funds for one page giving calculations.
            m.List[0].RetrieveEntireFundList = true;

            // first re-build list of fund items with only ones that contain a value (amt).
            var fundItems = fundItem.Where(f => f.Value.GetValueOrDefault() > 0).ToDictionary(f => f.Key, f => f.Value);

            var designatedFund = m.settings[id.Value].DonationFundId ?? 0;
            if (designatedFund != 0)
            {
                fundItems.Add(designatedFund, pf.AmtToPay);
            }

            // set the fund items on online reg person if there are any.
            if (fundItems.Any())
            {
                m.List[0].FundItem = fundItems;
                pf.AmtToPay = m.List[0].FundItemsChosen().Sum(f => f.Amt);
            }

            if (pf.AmtToPay.GetValueOrDefault() == 0)
            {
                ModelState.AddModelError("AmtToPay", "Invalid Amount");
            }

            SetHeaders(m);
            SetInstructions(m);

            var p = m.List[0];
            if (pf.ShowCampusOnePageGiving)
            {
                pf.Campuses = p.Campuses().ToList();
            }

            if (!ModelState.IsValid)
            {
                m.List[0].FundItem = fundItem;
                pf.AmtToPay = amtToPay;
                return View("OnePageGiving/Index", new OnePageGivingModel() { OnlineRegPersonModel = m.List[0], PaymentForm = pf });
            }

            if (CheckAddress(pf) == false)
            {
                m.List[0].FundItem = fundItem;
                pf.AmtToPay = amtToPay;
                return View("OnePageGiving/Index", new OnePageGivingModel() { OnlineRegPersonModel = m.List[0], PaymentForm = pf });
            }


            if (!ModelState.IsValid)
            {
                m.List[0].FundItem = fundItem;
                pf.AmtToPay = amtToPay;
                return View("OnePageGiving/Index", new OnePageGivingModel() { OnlineRegPersonModel = m.List[0], PaymentForm = pf });
            }

            p.orgid = m.Orgid;
            p.FirstName = pf.First;
            p.LastName = pf.Last;
            p.EmailAddress = pf.Email;
            p.Phone = pf.Phone;
            p.AddressLineOne = pf.Address;
            p.City = pf.City;
            p.State = pf.State;
            p.ZipCode = pf.Zip;
            p.Country = pf.Country;
            if (pf.ShowCampusOnePageGiving)
            {
                p.Campus = pf.CampusId.ToString();
            }

            p.IsNew = p.person == null;

            if (pf.testing)
            {
                pf.CheckTesting();
            }

            if (pf.Country.HasValue() && !pf.Zip.HasValue())
            {
                pf.Zip = "NA";
            }

            pf.ValidatePaymentForm(ModelState, false);
            if (!ModelState.IsValid)
            {
                m.List[0].FundItem = fundItem;
                pf.AmtToPay = amtToPay;
                return View("OnePageGiving/Index", new OnePageGivingModel() { OnlineRegPersonModel = m.List[0], PaymentForm = pf });
            }


            if (m?.UserPeopleId != null && m.UserPeopleId > 0)
            {
                pf.CheckStoreInVault(ModelState, m.UserPeopleId.Value);
            }

            if (!ModelState.IsValid)
            {
                m.List[0].FundItem = fundItem;
                pf.AmtToPay = amtToPay;
                return View("OnePageGiving/Index", new OnePageGivingModel() { OnlineRegPersonModel = m.List[0], PaymentForm = pf });
            }

            if (CurrentDatabase.Setting("UseRecaptcha"))
            {
                if (!GoogleRecaptcha.IsValidResponse(HttpContext, CurrentDatabase))
                {
                    m.List[0].FundItem = fundItem;
                    pf.AmtToPay = amtToPay;
                    ModelState.AddModelError("TranId", "ReCaptcha validation failed.");
                    return View("OnePageGiving/Index", new OnePageGivingModel
                    {
                        OnlineRegPersonModel = m.List[0],
                        PaymentForm = pf
                    });
                }
            }

            var ti = pf.ProcessPaymentTransaction(m);
            if ((ti.Approved ?? false) == false)
            {
                ModelState.AddModelError("TranId", ti.Message);

                m.List[0].FundItem = fundItem;
                pf.AmtToPay = amtToPay;
                return View("OnePageGiving/Index", new OnePageGivingModel() { OnlineRegPersonModel = m.List[0], PaymentForm = pf });
            }
            if (pf.Zip == "NA")
            {
                pf.Zip = null;
            }

            var ret = m.ConfirmTransaction(ti);
            switch (ret.Route)
            {
                case RouteType.ModelAction:
                    if (ti.Approved == true)
                    {
                        var url = $"/OnePageGiving/ThankYou/{id}{(pf.testing ? $"?testing=true&source={pf.source}" : $"?source={pf.source}")}";
                        return Redirect(url);
                    }
                    ErrorSignal.FromCurrentContext().Raise(new Exception(ti.Message));
                    ModelState.AddModelError("TranId", ti.Message);

                    m.List[0].FundItem = fundItem;
                    pf.AmtToPay = amtToPay;
                    return View("OnePageGiving/Index", new OnePageGivingModel() { OnlineRegPersonModel = m.List[0], PaymentForm = pf });
                case RouteType.Error:
                    DbUtil.LogActivity("OnePageGiving Error " + ret.Message, pf.OrgId);
                    return Message(ret.Message);
                default: // unexptected Route
                    ErrorSignal.FromCurrentContext().Raise(new Exception("OnePageGiving Unexpected route"));
                    DbUtil.LogActivity("OnlineReg Unexpected Route " + ret.Message, pf.OrgId);
                    ModelState.AddModelError("TranId", "unexpected error in payment processing");

                    m.List[0].FundItem = fundItem;
                    pf.AmtToPay = amtToPay;
                    return View(ret.View ?? "OnePageGiving/Index", new OnePageGivingModel() { OnlineRegPersonModel = m.List[0], PaymentForm = pf });
            }
        }

        [HttpGet, Route("~/OnePageGiving/ThankYou/{id:int}")]
        public ActionResult OnePageGivingThankYou(int id, bool? testing, string source)
        {
            Response.NoCache();
            var m = new OnlineRegModel(Request, CurrentDatabase, id, testing, null, null, source)
            { URL = "/OnePageGiving/" + id };
            return View("OnePageGiving/ThankYou", m);
        }

        [HttpGet, Route("~/OnePageGiving/Login/{id:int}")]
        public ActionResult OnePageGivingLogin(int id, bool? testing, string source)
        {
            var m = new OnlineRegModel(Request, CurrentDatabase, id, testing, null, null, source);
            SetHeaders(m);
            return View("OnePageGiving/Login", m);
        }

        [HttpPost, Route("~/OnePageGiving/Login/{id:int}")]
        public ActionResult OnePageGivingLogin(int id, string username, string password, bool? testing, string source)
        {
            var ret = AccountModel.AuthenticateLogon(username, password, Session, Request);

            if (ret is string)
            {
                ModelState.AddModelError("loginerror", ret.ToString());
                var m = new OnlineRegModel(Request, CurrentDatabase, id, testing, null, null, source);
                SetHeaders(m);
                return View("OnePageGiving/Login", m);
            }
            Session["OnlineRegLogin"] = true;


            var ev = CurrentDatabase.OrganizationExtras.SingleOrDefault(vv => vv.OrganizationId == id && vv.Field == "LoggedInOrgId");
            id = ev?.IntValue ?? id;
            var url = $"/OnePageGiving/{id}{(testing == true ? "?testing=true" : "")}";
            return Redirect(url);
        }

        private bool CheckAddress(PaymentForm pf)
        {
            if (!pf.IsUs)
            {
                pf.NeedsCityState = true;
                return pf.City.HasValue() && pf.State.HasValue();
            }
            var r = AddressVerify.LookupAddress(pf.Address, null, pf.City, pf.State, pf.Zip);
            if (r.Line1 == "error" || r.found == false)
            {
                if (pf.City.HasValue()
                    && pf.State.HasValue()
                    && pf.Zip.HasValue()
                    && pf.Address.HasValue())
                {
                    return true; // not found but complete
                }

                pf.NeedsCityState = true;
                return false;
            }

            // populate Address corrections
            if (r.Line1 != pf.Address)
            {
                pf.Address = r.Line1;
            }

            if (r.City != (pf.City ?? ""))
            {
                pf.City = r.City;
            }

            if (r.State != (pf.State ?? ""))
            {
                pf.State = r.State;
            }

            if (r.Zip != (pf.Zip ?? ""))
            {
                pf.Zip = r.Zip;
            }

            return true;
        }

        #endregion

        #region OnlineReg

        private string fromMethod;

        [HttpGet]
        [Route("~/OnlineReg/Index/{id:int}")]
        [Route("~/OnlineReg/{id:int}")]
        public ActionResult Index(int? id, bool? testing, string email, bool? login, string registertag, bool? showfamily, int? goerid, int? gsid, string source)
        {
            Response.NoCache();
            try
            {
                var m = new OnlineRegModel(Request, CurrentDatabase, id, testing, email, login, source);

                if (m.ManageGiving())
                {
                    Session["Campus"] = Request.QueryString["campus"];
                    Session["DefaultFunds"] = Request.QueryString["funds"];
                    m.Campus = Session["Campus"]?.ToString();
                    m.DefaultFunds = Session["DefaultFunds"]?.ToString();
                }

                if (m.org != null && m.org.IsMissionTrip == true)
                {
                    if (gsid != null || goerid != null)
                    {
                        m.PrepareMissionTrip(gsid, goerid);
                    }
                }

                SetHeaders(m);
                var pid = m.CheckRegisterLink(registertag);
                if (m.NotActive())
                {
                    return View("OnePageGiving/NotActive", m);
                }
                if (m.MissionTripSelfSupportPaylink.HasValue() && m.GoerId > 0)
                {
                    return Redirect(m.MissionTripSelfSupportPaylink);
                }

                return RouteRegistration(m, pid, showfamily);
            }
            catch (Exception ex)
            {
                if (ex is BadRegistrationException)
                {
                    return Message(ex.Message);
                }

                throw;
            }
        }

        [HttpPost]
        public ActionResult Login(OnlineRegModel m)
        {
            fromMethod = "Login";
            var ret = AccountModel.AuthenticateLogon(m.username, m.password, Session, Request);

            if (ret is string)
            {
                ModelState.AddModelError("authentication", ret.ToString());
                return FlowList(m);
            }
            Session["OnlineRegLogin"] = true;

            if (m.Orgid == Util.CreateAccountCode)
            {
                DbUtil.LogActivity("OnlineReg CreateAccount Existing", peopleid: Util.UserPeopleId, datumId: m.DatumId);
                return Content("/Person2/" + Util.UserPeopleId); // they already have an account, so take them to their page
            }
            m.UserPeopleId = Util.UserPeopleId;
            var route = RouteSpecialLogin(m);
            if (route != null)
            {
                return route;
            }

            m.HistoryAdd("login");
            if (m.org != null && m.org.IsMissionTrip == true && m.SupportMissionTrip)
            {
                OnlineRegPersonModel p;
                PrepareFirstRegistrant(ref m, m.UserPeopleId.Value, false, out p);
            }
            return FlowList(m);
        }

        private void PrepareFirstRegistrant(ref OnlineRegModel m, int pid, bool? showfamily, out OnlineRegPersonModel p)
        {
            p = null;
            if (showfamily != true)
            {
                // No need to pick family, so prepare first registrant ready to answer questions
                p = m.LoadExistingPerson(pid, 0);
                if (p == null)
                {
                    throw new Exception($"No person found with PeopleId = {pid}");
                }

                p.ValidateModelForFind(ModelState, 0);
                if (m.masterorg == null)
                {
                    if (m.List.Count == 0)
                    {
                        m.List.Add(p);
                    }
                    else
                    {
                        m.List[0] = p;
                    }
                }
            }
        }

        [HttpPost]
        public ActionResult NoLogin(OnlineRegModel m)
        {
            fromMethod = "NoLogin";
            // Clicked the register without logging in link
            m.nologin = true;
            m.CreateAnonymousList();
            m.Log("NoLogin");
            return FlowList(m);
        }

        [HttpPost]
        public ActionResult YesLogin(OnlineRegModel m)
        {
            fromMethod = "YesLogin";
            // clicked the Login Here button
            m.HistoryAdd("yeslogin");
            m.nologin = false;
            m.List = new List<OnlineRegPersonModel>();
#if DEBUG
            m.username = "David";
#endif
            return FlowList(m);
        }

        [HttpPost]
        public ActionResult RegisterFamilyMember(int id, OnlineRegModel m)
        {
            // got here by clicking on a link in the Family list
            var msg = m.CheckExpiredOrCompleted();
            if (msg.HasValue())
            {
                return PageMessage(msg);
            }

            fromMethod = "Register";

            m.StartRegistrationForFamilyMember(id, ModelState);

            // show errors or take them to the Questions page
            return FlowList(m);
        }

        [HttpPost]
        public ActionResult Cancel(int id, OnlineRegModel m)
        {
            // After clicking Cancel, remove a person from the completed registrants list
            fromMethod = "Cancel";
            m.CancelRegistrant(id);
            return FlowList(m);
        }

        [HttpPost]
        public ActionResult FindRecord(int id, OnlineRegModel m)
        {
            // Anonymous person clicks submit to find their record
            var msg = m.CheckExpiredOrCompleted();
            if (msg.HasValue())
            {
                return PageMessage(msg);
            }

            fromMethod = "FindRecord";
            m.HistoryAdd("FindRecord id=" + id);
            if (id >= m.List.Count)
            {
                return FlowList(m);
            }

            var p = m.GetFreshFindInfo(id);

            if (p.NeedsToChooseClass())
            {
                p.RegistrantProblem = "Please Make Selection Above";
                return FlowList(m);
            }
            p.ValidateModelForFind(ModelState, id);
            if (!ModelState.IsValid)
            {
                return FlowList(m);
            }

            if (p.AnonymousReRegistrant())
            {
                return View("Continue/ConfirmReregister", m); // send email with link to reg-register
            }

            if (p.IsSpecialReg())
            {
                p.QuestionsOK = true;
            }
            else if (p.RegistrationFull())
            {
                m.Log("Closed");
                ModelState.AddModelError(m.GetNameFor(mm => mm.List[id].DateOfBirth), "Sorry, but registration is closed.");
            }

            p.FillPriorInfo();
            p.SetSpecialFee();

            if (!ModelState.IsValid || p.count == 1)
            {
                return FlowList(m);
            }

            // form is ok but not found, so show AddressGenderMarital Form
            p.PrepareToAddNewPerson(ModelState, id);
            p.Found = false;
            return FlowList(m);
        }

        [HttpPost]
        public ActionResult SubmitNew(int id, OnlineRegModel m)
        {
            // Submit from AddressMaritalGenderForm
            var msg = m.CheckExpiredOrCompleted();
            if (msg.HasValue())
            {
                return PageMessage(msg);
            }

            fromMethod = "SubmitNew";
            ModelState.Clear();
            m.HistoryAdd("SubmitNew id=" + id);
            var p = m.List[id];
            if (p.ComputesOrganizationByAge())
            {
                p.orgid = null; // forget any previous information about selected org, may have new information like gender
            }

            p.ValidateModelForNew(ModelState, id);

            SetHeaders(m);
            var ret = p.AddNew(ModelState, id);
            return ret.HasValue()
                ? View(ret, m)
                : FlowList(m);
        }

        [HttpPost]
        public ActionResult SubmitQuestions(int id, OnlineRegModel m)
        {
            var ret = m.CheckExpiredOrCompleted();
            if (ret.HasValue())
            {
                return PageMessage(ret);
            }

            fromMethod = "SubmitQuestions";
            m.HistoryAdd("SubmitQuestions id=" + id);
            if (m.List.Count <= id)
            {
                return Content("<p style='color:red'>error: cannot find person on submit other info</p>");
            }

            m.List[id].ValidateModelQuestions(ModelState, id);
            return FlowList(m);
        }

        [HttpPost]
        public ActionResult AddAnotherPerson(OnlineRegModel m)
        {
            var ret = m.CheckExpiredOrCompleted();
            if (ret.HasValue())
            {
                return PageMessage(ret);
            }

            fromMethod = "AddAnotherPerson";
            m.HistoryAdd("AddAnotherPerson");
            m.ParseSettings();
            if (!ModelState.IsValid)
            {
                return FlowList(m);
            }

            m.List.Add(new OnlineRegPersonModel
            {
                orgid = m.Orgid,
                masterorgid = m.masterorgid,
            });
            return FlowList(m);
        }

        [HttpPost]
        public ActionResult AskDonation(OnlineRegModel m)
        {
            m.HistoryAdd("AskDonation");
            if (m.List.Count == 0)
            {
                m.Log("AskDonationError NoRegistrants");
                return Content("Can't find any registrants");
            }
            m.RemoveLastRegistrantIfEmpty();
            SetHeaders(m);
            return View("Other/AskDonation", m);
        }

        [HttpPost]
        public ActionResult PostDonation(OnlineRegModel m)
        {
            if (m.donor == null && m.donation > 0)
            {
                ModelState.AddModelError("donation", "Please indicate who is the donor");
                SetHeaders(m);
                return View("Other/AskDonation", m);
            }
            TempData["onlineregmodel"] = Util.Serialize(m);
            return Redirect("/OnlineReg/CompleteRegistration");
        }

#if DEBUG

        // For testing only
        [HttpGet]
        [Route("~/OnlineReg/CompleteRegistration/{id:int}")]
        public ActionResult CompleteRegistration(int id)
        {
            var ed = CurrentDatabase.RegistrationDatas.SingleOrDefault(e => e.Id == id);
            var m = Util.DeSerialize<OnlineRegModel>(ed?.Data);
            TempData["onlineregmodel"] = Util.Serialize(m);
            return Redirect("/OnlineReg/CompleteRegistration");
        }

#endif
        [HttpPost]
        public ActionResult CompleteRegistration(OnlineRegModel m)
        {
            if (m.org != null && m.org.RegistrationTypeId == RegistrationTypeCode.SpecialJavascript)
            {
                m.List[0].SpecialTest = SpecialRegModel.ParseResults(Request.Form);
            }

            TempData["onlineregmodel"] = Util.Serialize(m);
            return Redirect("/OnlineReg/CompleteRegistration");
        }

        [HttpGet]
        public ActionResult CompleteRegistration()
        {
            Response.NoCache();
            var s = (string)TempData["onlineregmodel"];
            if (s == null)
            {
                DbUtil.LogActivity("OnlineReg Error PageRefreshNotAllowed");
                return Message("Registration cannot be completed after a page refresh.");
            }
            var m = Util.DeSerialize<OnlineRegModel>(s);
            var msg = m.CheckExpiredOrCompleted();
            if (msg.HasValue())
            {
                return Message(msg);
            }

            var ret = m.CompleteRegistration(this);
            switch (ret.Route)
            {
                case RouteType.Error:
                    m.Log(ret.Message);
                    return Message(ret.Message);
                case RouteType.Action:
                    return View(ret.View);
                case RouteType.Redirect:
                    return RedirectToAction(ret.View, ret.RouteData);
                case RouteType.Terms:
                    return View(ret.View, m);
                case RouteType.Payment:
                    return View(ret.View, ret.PaymentForm);
            }
            m.Log("BadRouteOnCompleteRegistration");
            return Message("unexpected value on CompleteRegistration");
        }

        [HttpPost]
        public JsonResult CityState(string id)
        {
            var z = CurrentDatabase.ZipCodes.SingleOrDefault(zc => zc.Zip == id);
            if (z == null)
            {
                return Json(null);
            }

            return Json(new { city = z.City.Trim(), state = z.State });
        }

        public ActionResult Timeout(string ret)
        {
            FormsAuthentication.SignOut();
            Session.Abandon();
            ViewBag.Url = ret;
            return View("Other/Timeout");
        }

        private ActionResult FlowList(OnlineRegModel m)
        {
            try
            {
                m.UpdateDatum();
                m.Log(fromMethod);
                var content = ViewExtensions2.RenderPartialViewToString2(this, "Flow2/List", m);
                return Content(content);
            }
            catch (Exception ex)
            {
                return ErrorResult(m, ex, "In " + fromMethod + "<br>" + ex.Message);
            }
        }

        private ActionResult ErrorResult(OnlineRegModel m, Exception ex, string errorDisplay)
        {
            // ReSharper disable once EmptyGeneralCatchClause
            try
            {
                m.UpdateDatum();
            }
            catch
            {
            }

            var ex2 = new Exception($"{errorDisplay}, {CurrentDatabase.ServerLink("/OnlineReg/RegPeople/") + m.DatumId}", ex);
            ErrorSignal.FromCurrentContext().Raise(ex2);
            m.Log(ex2.Message);
            TempData["error"] = errorDisplay;
            TempData["stack"] = ex.StackTrace;
            return Content("/Error/");
        }

        protected override void OnException(ExceptionContext filterContext)
        {
            if (filterContext.ExceptionHandled)
            {
                return;
            }

            ErrorSignal.FromCurrentContext().Raise(filterContext.Exception);
            DbUtil.LogActivity("OnlineReg Error:" + filterContext.Exception.Message);
            filterContext.Result = Message(filterContext.Exception.Message, filterContext.Exception.StackTrace);
            filterContext.ExceptionHandled = true;
        }

        protected override void Initialize(RequestContext requestContext)
        {
            base.Initialize(requestContext);
            requestContext.HttpContext.Items["controller"] = this;
        }

        [HttpGet]
        [Route("~/OnlineReg/{id:int}/Giving/")]
        [Route("~/OnlineReg/{id:int}/Giving/{goerid:int}")]
        public ActionResult Giving(int id, int? goerid, int? gsid)
        {
            var m = new OnlineRegModel(Request, CurrentDatabase, id, false, null, null, null);
            if (m.org != null && m.org.IsMissionTrip == true && m.org.TripFundingPagesEnable == true)
            {
                m.PrepareMissionTrip(gsid, goerid);
            }
            else
            {
                return new HttpNotFoundResult();
            }

            SetHeaders(m);
            if (m.MissionTripCost == null)
            {
                // goer specified isn't part of this trip
                return new HttpNotFoundResult();
            }
            if (Util.UserPeopleId == goerid)
            {
                return View("Giving/Goer", m);
            }
            else if (m.org.TripFundingPagesPublic)
            {
                return View("Giving/Guest", m);
            }
            else
            {
                return new HttpNotFoundResult();
            }
        }

        #endregion

        #region Payment

        [HttpPost]
        public ActionResult ProcessPayment(PaymentForm pf)
        {
            Response.NoCache();

#if !DEBUG
            if (Session["FormId"] != null) {
                if ((Guid)Session["FormId"] == pf.FormId) {
                    return Message("Already submitted");
                }
            }
#endif

            OnlineRegModel m = null;
            var ed = CurrentDatabase.RegistrationDatas.SingleOrDefault(e => e.Id == pf.DatumId);
            if (ed != null)
            {
                m = Util.DeSerialize<OnlineRegModel>(ed.Data);
            }

#if !DEBUG
            if (m != null && m.History.Any(h => h.Contains("ProcessPayment"))) {
                return Content("Already submitted");
            }
#endif

            int? datumid = null;
            if (m != null)
            {
                datumid = m.DatumId;
                var msg = m.CheckDuplicateGift(pf.AmtToPay);
                if (Util.HasValue(msg))
                {
                    return Message(msg);
                }
            }
            if (IsCardTester(pf, "Payment Page"))
            {
                return Message("Found Card Tester");
            }

            if (CurrentDatabase.Setting("UseRecaptcha"))
            {
                if (!GoogleRecaptcha.IsValidResponse(HttpContext, CurrentDatabase))
                {
                    CurrentDatabase.LogActivity("OnlineReg Error ReCaptcha validation failed.", pf.OrgId, did: datumid);
                    ModelState.AddModelError("form", "ReCaptcha validation failed.");
                    return View("Payment/Process", pf);
                }
            }

            SetHeaders(pf.OrgId ?? 0);
            var ret = pf.ProcessPayment(ModelState, m);
            switch (ret.Route)
            {
                case RouteType.ModelAction:
                    return View(ret.View, ret.Model);
                case RouteType.AmtDue:
                    ViewBag.amtdue = ret.AmtDue;
                    return View(ret.View, ret.Transaction);
                case RouteType.Error:
                    CurrentDatabase.LogActivity("OnlineReg Error " + ret.Message, pf.OrgId, did: datumid);
                    return Message(ret.Message);
                case RouteType.ValidationError:
                    return View(ret.View, pf);
                default: // unexptected Route
                    if (ModelState.IsValid)
                    {
                        ErrorSignal.FromCurrentContext().Raise(new Exception("OnlineReg Unexpected route datum= " + datumid));
                        CurrentDatabase.LogActivity("OnlineReg Unexpected Route " + ret.Message, oid: pf.OrgId, did: datumid);
                        ModelState.AddModelError("form", "unexpected error in payment processing");
                    }
                    return View(ret.View ?? "Payment/Process", pf);
            }
        }

        private bool IsCardTester(PaymentForm pf, string from)
        {
            if (!Util.IsHosted || !pf.CreditCard.HasValue())
            {
                return false;
            }

            var hash = Pbkdf2Hasher.HashString(pf.CreditCard);
            CurrentDatabase.InsertIpLog(Request.UserHostAddress, hash);

            if (pf.IsProblemUser())
            {
                return _rogueIpService.LogRogueUser("Problem User", from);
            }

            var iscardtester = ConfigurationManager.AppSettings["IsCardTester"];
            if (!iscardtester.HasValue())
            {
                return false;
            }
            var result = CurrentDatabase.Connection.ExecuteScalar<string>(iscardtester, new { ip = Request.UserHostAddress });
            if (result.Equal("OK"))
            {
                return false;
            }

            return _rogueIpService.LogRogueUser(result, from);
        }

        [ActionName("Confirm")]
        [HttpPost]
        public ActionResult Confirm_Post(int? id, string transactionId, decimal? amount)
        {
            if (!id.HasValue)
            {
                return View("Other/Unknown");
            }

            var m = OnlineRegModel.GetRegistrationFromDatum(id ?? 0);
            if (m == null || m.Completed)
            {
                if (m == null)
                {
                    DbUtil.LogActivity("OnlineReg NoPendingConfirmation");
                }
                else
                {
                    m.Log("NoPendingConfirmation");
                }

                return Content("no pending confirmation found");
            }
            if (!Util.HasValue(transactionId))
            {
                m.Log("NoTransactionId");
                return Content("error no transaction");
            }
            if (m.List.Count == 0)
            {
                m.Log("NoRegistrants");
                return Content("no registrants found");
            }
            try
            {
                OnlineRegModel.LogOutOfOnlineReg();
                var view = m.ConfirmTransaction(transactionId);
                m.UpdateDatum(completed: true);
                SetHeaders(m);
                if (view == ConfirmEnum.ConfirmAccount)
                {
                    m.Log("ConfirmAccount");
                    return View("Continue/ConfirmAccount", m);
                }
                m.Log("Confirm");
                return View("Confirm", m);
            }
            catch (Exception ex)
            {
                m.Log("Error " + ex.Message);
                ErrorSignal.FromCurrentContext().Raise(ex);
                TempData["error"] = ex.Message;
                return Redirect("/Error");
            }
        }
        [HttpGet]
        public ActionResult Confirm(int? id, string transactionId, decimal? amount)
        {
            if (!id.HasValue)
            {
                return View("Other/Unknown");
            }

            var m = OnlineRegModel.GetRegistrationFromDatum(id ?? 0);
            if (m == null || m.Completed)
            {
                if (m == null)
                {
                    DbUtil.LogActivity("OnlineReg NoPendingConfirmation");
                }
                else
                {
                    m.Log("NoPendingConfirmation");
                }

                return Content("no pending confirmation found");
            }
            if (!Util.HasValue(transactionId))
            {
                m.Log("NoTransactionId");
                return Content("error no transaction");
            }
            if (m.List.Count == 0)
            {
                m.Log("NoRegistrants");
                return Content("no registrants found");
            }
            try
            {
                OnlineRegModel.LogOutOfOnlineReg();
                var view = m.ConfirmTransaction(transactionId);
                m.UpdateDatum(completed: true);
                SetHeaders(m);
                if (view == ConfirmEnum.ConfirmAccount)
                {
                    m.Log("ConfirmAccount");
                    return View("Continue/ConfirmAccount", m);
                }
                m.Log("Confirm");
                return View("Confirm", m);
            }
            catch (Exception ex)
            {
                m.Log("Error " + ex.Message);
                ErrorSignal.FromCurrentContext().Raise(ex);
                TempData["error"] = ex.Message;
                return Redirect("/Error");
            }
        }

        [HttpGet]
        public ActionResult PayAmtDue(string q)
        {
            // reached by the paylink in the confirmation email
            // which is produced in EnrollAndConfirm
            Response.NoCache();

            if (!Util.HasValue(q))
            {
                return Message("unknown");
            }

            var id = Util.Decrypt(q).ToInt2();
            var qq = from t in CurrentDatabase.Transactions
                     where t.OriginalId == id || t.Id == id
                     orderby t.Id descending
                     select new { t, email = t.TransactionPeople.FirstOrDefault().Person.EmailAddress };
            var i = qq.FirstOrDefault();
            if (i == null)
            {
                return Message("no outstanding transaction");
            }

            var ti = i.t;
            var email = i.email;
            var amtdue = PaymentForm.AmountDueTrans(CurrentDatabase, ti);
            if (amtdue == 0)
            {
                return Message("no outstanding transaction");
            }

#if DEBUG
            ti.Testing = true;
            if (!Util.HasValue(ti.Address))
            {
                ti.Address = "235 Riveredge";
                ti.City = "Cordova";
                ti.Zip = "38018";
                ti.State = "TN";
            }
#endif
            var pf = PaymentForm.CreatePaymentFormForBalanceDue(ti, amtdue, email);

            SetHeaders(pf.OrgId ?? 0);

            DbUtil.LogActivity("OnlineReg PayDueStart", ti.OrgId, ti.LoginPeopleId ?? ti.FirstTransactionPeopleId());
            return View("Payment/Process", pf);
        }

        [ActionName("ConfirmDuePaid")]
        [HttpPost]
        public ActionResult ConfirmDuePaid_Post(int? id, string transactionId, decimal amount)
        {
            Response.NoCache();
            if (!id.HasValue)
            {
                return View("Other/Unknown");
            }

            if (!Util.HasValue(transactionId))
            {
                DbUtil.LogActivity("OnlineReg PayDueNoTransactionId");
                return Message("error no transactionid");
            }
            var ti = CurrentDatabase.Transactions.SingleOrDefault(tt => tt.Id == id);
            if (ti == null)
            {
                DbUtil.LogActivity("OnlineReg PayDueNoPendingTrans");
                return Message("no pending transaction");
            }
#if DEBUG
            ti.Testing = true;
#endif
            OnlineRegModel.ConfirmDuePaidTransaction(ti, transactionId, sendmail: true);
            ViewBag.amtdue = PaymentForm.AmountDueTrans(CurrentDatabase, ti).ToString("C");
            SetHeaders(ti.OrgId ?? 0);
            DbUtil.LogActivity("OnlineReg PayDueConfirm", ti.OrgId, ti.LoginPeopleId ?? ti.FirstTransactionPeopleId());
            return View("PayAmtDue/Confirm", ti);
        }

        [HttpGet]
        public ActionResult ConfirmDuePaid(int? id, string transactionId, decimal amount)
        {
            Response.NoCache();
            if (!id.HasValue)
            {
                return View("Other/Unknown");
            }

            if (!Util.HasValue(transactionId))
            {
                DbUtil.LogActivity("OnlineReg PayDueNoTransactionId");
                return Message("error no transactionid");
            }
            var ti = CurrentDatabase.Transactions.SingleOrDefault(tt => tt.Id == id);
            if (ti == null)
            {
                DbUtil.LogActivity("OnlineReg PayDueNoPendingTrans");
                return Message("no pending transaction");
            }
#if DEBUG
            ti.Testing = true;
#endif
            OnlineRegModel.ConfirmDuePaidTransaction(ti, transactionId, sendmail: true);
            ViewBag.amtdue = PaymentForm.AmountDueTrans(CurrentDatabase, ti).ToString("C");
            SetHeaders(ti.OrgId ?? 0);
            DbUtil.LogActivity("OnlineReg PayDueConfirm", ti.OrgId, ti.LoginPeopleId ?? ti.FirstTransactionPeopleId());
            return View("PayAmtDue/Confirm", ti);
        }

        [HttpGet]
        public ActionResult PayDueTest(string q)
        {
            if (!Util.HasValue(q))
            {
                return Message("unknown");
            }

            var id = Util.Decrypt(q);
            var ed = CurrentDatabase.ExtraDatas.SingleOrDefault(e => e.Id == id.ToInt());
            if (ed == null)
            {
                return Message("no outstanding transaction");
            }

            return Content(ed.Data);
        }


        #endregion

        #region Routing

        private ActionResult RouteRegistration(OnlineRegModel m, int pid, bool? showfamily)
        {
            if (pid == 0)
            {
                return View(m);
            }
#if DEBUG
            m.DebugCleanUp();
#endif

            var link = RouteExistingRegistration(m, pid);
            if (link.HasValue())
            {
                return Redirect(link);
            }

            OnlineRegPersonModel p;
            PrepareFirstRegistrant(ref m, pid, showfamily, out p);

            if (!ModelState.IsValid)
            {
                m.Log("CannotProceed");
                return View(m);
            }

            link = RouteManageGivingSubscriptionsPledgeVolunteer(m);
            if (link.HasValue())
            {
                if (m.ManageGiving()) // use Direct ActionResult instead of redirect
                {
                    return ManageGiving(m.Orgid.ToString(), m.testing);
                }
                else if (m.RegisterLinkMaster())
                {
                    return Redirect(link);
                }
                else
                {
                    return Redirect(link);
                }
            }

            // check for forcing show family, master org, or not found
            if (showfamily == true || p.org == null || p.Found != true)
            {
                return View(m);
            }

            // ready to answer questions, make sure registration is ok to go
            m.Log("Authorized");
            if (!m.SupportMissionTrip)
            {
                p.IsFilled = p.org.RegLimitCount(CurrentDatabase) >= p.org.Limit;
            }

            if (p.IsFilled)
            {
                m.Log("Closed");
                ModelState.AddModelError(m.GetNameFor(mm => mm.List[0].Found), "Sorry, but registration is closed.");
            }

            p.FillPriorInfo();
            p.SetSpecialFee();

            m.HistoryAdd($"index, pid={pid}, !showfamily, p.org, found=true");
            return View(m);
        }

        private string RouteManageGivingSubscriptionsPledgeVolunteer(OnlineRegModel m)
        {
            if (m.RegisterLinkMaster())
            {
                if (m.registerLinkType.HasValue())
                {
                    if (m.registerLinkType.StartsWith("registerlink") || m.registerLinkType == "masterlink" || User.Identity.IsAuthenticated)
                    {
                        TempData["token"] = m.registertag;
                        TempData["PeopleId"] = m.UserPeopleId ?? Util.UserPeopleId;
                    }
                }

                return $"/OnlineReg/RegisterLinkMaster/{m.Orgid}";
            }
            TempData["PeopleId"] = m.UserPeopleId ?? Util.UserPeopleId;
            if (m.ManagingSubscriptions())
            {
                return $"/OnlineReg/ManageSubscriptions/{m.masterorgid}";
            }

            if (m.ManageGiving())
            {
                return $"/OnlineReg/ManageGiving/{m.Orgid}";
            }

            if (m.OnlinePledge())
            {
                return $"/OnlineReg/ManagePledge/{m.Orgid}";
            }

            if (m.ChoosingSlots())
            {
                return $"/OnlineReg/ManageVolunteer/{m.Orgid}";
            }

            TempData.Remove("PeopleId");
            return null;
        }

        private string RouteExistingRegistration(OnlineRegModel m, int? pid = null)
        {
            if (m.SupportMissionTrip)
            {
                return null;
            }

            var existingRegistration = m.GetExistingRegistration(pid ?? Util.UserPeopleId ?? 0);
            if (existingRegistration == null)
            {
                return null;
            }

            m.Log("Existing");
            TempData["PeopleId"] = existingRegistration.UserPeopleId;
            return "/OnlineReg/Existing/" + existingRegistration.DatumId;
        }

        private ActionResult RouteSpecialLogin(OnlineRegModel m)
        {
            if (Util.UserPeopleId == null)
            {
                throw new Exception("Util.UserPeopleId is null on login");
            }

            var link = RouteExistingRegistration(m);
            if (link.HasValue())
            {
                return Redirect(link);
            }

            m.CreateAnonymousList();
            m.UserPeopleId = Util.UserPeopleId;

            if (m.OnlineGiving())
            {
                m.Log("Login OnlineGiving");
                return RegisterFamilyMember(Util.UserPeopleId.Value, m);
            }

            link = RouteManageGivingSubscriptionsPledgeVolunteer(m);
            if (link.HasValue())
            {
                return Content(link); // this will be used for a redirect in javascript
            }

            return null;
        }

        #endregion

        #region SaveAndContinue

        [HttpGet]
        public ActionResult Continue(int id)
        {
            var m = OnlineRegModel.GetRegistrationFromDatum(id);
            if (m == null)
            {
                return Message("no Existing registration available");
            }

            var n = m.List.Count - 1;
            m.HistoryAdd("continue");
            m.UpdateDatum();
            SetHeaders(m);
            if (m.RegistrantComplete)
            {
                return Redirect("/OnlineReg/CompleteRegistration/" + id);
            }
            return View("Index", m);
        }

        [HttpGet]
        public ActionResult StartOver(int id)
        {
            var pid = (int?)TempData["PeopleId"];
            if (!pid.HasValue || pid == 0)
            {
                return Message("not logged in");
            }

            var m = OnlineRegModel.GetRegistrationFromDatum(id);
            if (m == null)
            {
                return Message("no Existing registration available");
            }

            m.StartOver();
            return Redirect(m.URL);
        }

        [HttpPost]
        public ActionResult AutoSaveProgress(OnlineRegModel m)
        {
            try { m.UpdateDatum(); }
            catch { }
            return Content(m.DatumId.ToString());
        }

        [HttpPost]
        public ActionResult SaveProgress(OnlineRegModel m)
        {
            m.HistoryAdd("saveprogress");
            if (m.UserPeopleId == null)
            {
                m.UserPeopleId = Util.UserPeopleId;
            }

            m.UpdateDatum();
            var p = m.UserPeopleId.HasValue ? CurrentDatabase.LoadPersonById(m.UserPeopleId.Value) : m.List[0].person;

            if (p == null)
            {
                return Content("We have not found your record yet, cannot save progress, sorry");
            }

            if (m.masterorgid == null && m.Orgid == null)
            {
                return Content("Registration is not far enough along to save, sorry.");
            }

            var msg = CurrentDatabase.ContentHtml("ContinueRegistrationLink", @"
<p>Hi {first},</p>
<p>Here is the link to continue your registration:</p>
Resume [registration for {orgname}]
").Replace("{orgname}", m.Header);
            var linktext = Regex.Match(msg, @"(\[(.*)\])", RegexOptions.Singleline).Groups[2].Value;
            var registerlink = EmailReplacements.CreateRegisterLink(m.masterorgid ?? m.Orgid, linktext);
            msg = Regex.Replace(msg, @"(\[.*\])", registerlink, RegexOptions.Singleline);

            var notifyids = CurrentDatabase.NotifyIds((m.masterorg ?? m.org).NotifyIds);
            CurrentDatabase.Email(notifyids[0].FromEmail, p, $"Continue your registration for {m.Header}", msg);

            /* We use Content as an ActionResult instead of Message because we want plain text sent back
             * This is an HttpPost ajax call and will have a SiteLayout wrapping this.
             */
            return Content(@"
We have saved your progress. An email with a link to finish this registration will come to you shortly.
<input type='hidden' id='SavedProgress' value='true'/>
");
        }

        [HttpGet]
        public ActionResult Existing(int id)
        {
            if (!TempData.ContainsKey("PeopleId"))
            {
                return Message("not logged in");
            }

            var pid = (int?)TempData["PeopleId"];
            if (!pid.HasValue || pid == 0)
            {
                return Message("not logged in");
            }

            var m = OnlineRegModel.GetRegistrationFromDatum(id);
            if (m == null)
            {
                return Message("no Existing registration available");
            }

            if (m.UserPeopleId != m.Datum.UserPeopleId)
            {
                return Message("incorrect user");
            }

            TempData["PeopleId"] = pid;
            return View("Continue/Existing", m);
        }

        [HttpPost]
        public ActionResult SaveProgressPayment(int id)
        {
            var ed = CurrentDatabase.RegistrationDatas.SingleOrDefault(e => e.Id == id);
            if (ed != null)
            {
                var m = Util.DeSerialize<OnlineRegModel>(ed.Data);
                m.HistoryAdd("saveprogress");
                if (m.UserPeopleId == null)
                {
                    m.UserPeopleId = Util.UserPeopleId;
                }

                m.UpdateDatum();
                return Json(new
                {
                    confirm = "/OnlineReg/FinishLater/" + id,
                    formmethod = "GET"
                });
            }
            return Json(new { confirm = "/OnlineReg/Unknown" });
        }

        [HttpGet]
        public ActionResult FinishLater(int id)
        {
            var ed = CurrentDatabase.RegistrationDatas.SingleOrDefault(e => e.Id == id);
            if (ed == null)
            {
                return View("Other/Unknown");
            }

            var m = Util.DeSerialize<OnlineRegModel>(ed.Data);
            m.FinishLaterNotice();
            return View("Continue/FinishLater", m);
        }

        #endregion

        #region SetHeaders

        private const string ManagedGivingShellSettingKey = "UX-ManagedGivingShell";
        private Dictionary<int, Settings> _settings;

        public Dictionary<int, Settings> settings
        {
            get
            {
                if (_settings == null)
                {
                    _settings = HttpContext.Items["RegSettings"] as Dictionary<int, Settings>;
                }

                return _settings;
            }
        }

        public void SetHeaders(OnlineRegModel m2)
        {
            Session["gobackurl"] = m2.URL;
            ViewBag.Title = m2.Header;
            SetHeaders2(m2.Orgid ?? m2.masterorgid ?? 0);
        }

        private void SetHeaders2(int id)
        {
            var org = CurrentDatabase.LoadOrganizationById(id);
            var shell = SetAlternativeManagedGivingShell();

            if (!shell.HasValue() && (settings == null || !settings.ContainsKey(id)) && org != null)
            {
                var setting = CurrentDatabase.CreateRegistrationSettings(id);
                shell = CurrentDatabase.ContentOfTypeHtml(setting.ShellBs)?.Body;
            }
            if (!shell.HasValue() && settings != null && settings.ContainsKey(id))
            {
                shell = CurrentDatabase.ContentOfTypeHtml(settings[id].ShellBs)?.Body;
            }

            if (!shell.HasValue())
            {
                shell = CurrentDatabase.ContentOfTypeHtml("ShellDefaultBs")?.Body;
                if (!shell.HasValue())
                {
                    shell = CurrentDatabase.ContentOfTypeHtml("DefaultShellBs")?.Body;
                }
            }


            if (shell != null && shell.HasValue())
            {
                shell = shell.Replace("{title}", ViewBag.Title);
                var re = new Regex(@"(.*<!--FORM START-->\s*).*(<!--FORM END-->.*)", RegexOptions.Singleline);
                var t = re.Match(shell).Groups[1].Value.Replace("<!--FORM CSS-->", ViewExtensions2.Bootstrap3Css());
                ViewBag.hasshell = true;
                ViewBag.top = t;
                var b = re.Match(shell).Groups[2].Value;
                ViewBag.bottom = b;
            }
            else
            {
                ViewBag.hasshell = false;
            }
        }

        private void SetHeaders(int id)
        {
            Settings setting = null;
            var org = CurrentDatabase.LoadOrganizationById(id);
            if (org != null)
            {
                SetHeaders2(id);
                return;
            }

            var shell = SetAlternativeManagedGivingShell();
            if (!shell.HasValue() && (settings == null || !settings.ContainsKey(id)))
            {
                setting = CurrentDatabase.CreateRegistrationSettings(id);
                shell = CurrentDatabase.Content(setting.Shell, null);
            }
            if (!shell.HasValue() && settings != null && settings.ContainsKey(id))
            {
                shell = CurrentDatabase.Content(settings[id].Shell, null);
            }
            if (!shell.HasValue())
            {
                shell = CurrentDatabase.Content("ShellDiv-" + id, CurrentDatabase.Content("ShellDefault", ""));
            }

            var s = shell;
            if (s.HasValue())
            {
                var re = new Regex(@"(.*<!--FORM START-->\s*).*(<!--FORM END-->.*)", RegexOptions.Singleline);
                var t = re.Match(s).Groups[1].Value.Replace("<!--FORM CSS-->",
                ViewExtensions2.jQueryUICss() +
                "\r\n<link href=\"/Content/styles/onlinereg.css?v=8\" rel=\"stylesheet\" type=\"text/css\" />\r\n");
                ViewBag.hasshell = true;
                var b = re.Match(s).Groups[2].Value;
                ViewBag.bottom = b;
            }
            else
            {
                ViewBag.hasshell = false;
                ViewBag.header = CurrentDatabase.Content("OnlineRegHeader-" + id, CurrentDatabase.Content("OnlineRegHeader", ""));
                ViewBag.top = CurrentDatabase.Content("OnlineRegTop-" + id, CurrentDatabase.Content("OnlineRegTop", ""));
                ViewBag.bottom = CurrentDatabase.Content("OnlineRegBottom-" + id, CurrentDatabase.Content("OnlineRegBottom", ""));
            }
        }

        private string SetAlternativeManagedGivingShell()
        {
            var shell = string.Empty;
            var managedGivingShellSettingKey = ManagedGivingShellSettingKey;
            var campus = Session["Campus"]?.ToString(); // campus is only set for managed giving flow.
            if (!string.IsNullOrWhiteSpace(campus))
            {
                managedGivingShellSettingKey = $"{managedGivingShellSettingKey}-{campus.ToUpper()}";
            }
            var alternateShellSetting = CurrentDatabase.Settings.SingleOrDefault(x => x.Id == managedGivingShellSettingKey);
            if (alternateShellSetting != null)
            {
                var alternateShell = CurrentDatabase.Contents.SingleOrDefault(x => x.Name == alternateShellSetting.SettingX);
                if (alternateShell != null)
                {
                    shell = alternateShell.Body;
                }
            }

            return shell;
        }

        #endregion

        #region OtherRegistrations

        private const string registerlinkSTR = "RegisterLink";
        private const string votelinkSTR = "VoteLink";
        private const string rsvplinkSTR = "RsvpLink";
        private const string sendlinkSTR = "SendLink";
        private const string landingSTR = "Landing";
        private const string confirmSTR = "Confirm";

        public ActionResult VoteLinkSg(string id, string message, bool? confirm)
        {
            var li = new LinkInfo(CurrentDatabase, votelinkSTR, landingSTR, id);
            if (li.error.HasValue())
            {
                return Message(li.error);
            }

            ViewBag.Id = id;
            ViewBag.Message = message;
            ViewBag.Confirm = confirm.GetValueOrDefault().ToString();

            var smallgroup = li.a[4];
            DbUtil.LogActivity($"{votelinkSTR}{landingSTR}: {smallgroup}", li.oid, li.pid);
            return View("Other/VoteLinkSg");
        }

        [HttpPost]
        public ActionResult VoteLinkSg(string id, string message, bool? confirm, FormCollection formCollection)
        {
            var li = new LinkInfo(CurrentDatabase, votelinkSTR, confirmSTR, id);
            if (li.error.HasValue())
            {
                return Message(li.error);
            }

            try
            {
                var smallgroup = li.a[4];

                if (!li.oid.HasValue)
                {
                    throw new Exception("orgid missing");
                }

                if (!li.pid.HasValue)
                {
                    throw new Exception("peopleid missing");
                }

                var q = (from pp in CurrentDatabase.People
                         where pp.PeopleId == li.pid
                         let org = CurrentDatabase.Organizations.SingleOrDefault(oo => oo.OrganizationId == li.oid)
                         let om = CurrentDatabase.OrganizationMembers.SingleOrDefault(oo => oo.OrganizationId == li.oid && oo.PeopleId == li.pid)
                         select new { p = pp, org, om }).Single();

                if (q.org == null && CurrentDatabase.Host == "trialdb")
                {
                    var oid = li.oid + Util.TrialDbOffset;
                    q = (from pp in CurrentDatabase.People
                         where pp.PeopleId == li.pid
                         let org = CurrentDatabase.Organizations.SingleOrDefault(oo => oo.OrganizationId == oid)
                         let om = CurrentDatabase.OrganizationMembers.SingleOrDefault(oo => oo.OrganizationId == oid && oo.PeopleId == li.pid)
                         select new { p = pp, org, om }).Single();
                }

                if (q.org == null)
                {
                    throw new Exception("org missing, bad link");
                }
                if ((q.org.RegistrationTypeId ?? RegistrationTypeCode.None) == RegistrationTypeCode.None)
                {
                    throw new Exception("votelink is no longer active");
                }

                if (q.om == null && q.org.Limit <= q.org.RegLimitCount(CurrentDatabase))
                {
                    throw new Exception("sorry, maximum limit has been reached");
                }

                if (q.om == null &&
                    (q.org.RegistrationClosed == true || q.org.OrganizationStatusId == OrgStatusCode.Inactive))
                {
                    throw new Exception("sorry, registration has been closed");
                }

                var setting = CurrentDatabase.CreateRegistrationSettings(li.oid.Value);
                if (IsSmallGroupFilled(setting, li.oid.Value, smallgroup))
                {
                    throw new Exception("sorry, maximum limit has been reached for " + smallgroup);
                }

                var omb = OrganizationMember.Load(CurrentDatabase, li.pid.Value, li.oid.Value) ??
                          OrganizationMember.InsertOrgMembers(CurrentDatabase,
                              li.oid.Value, li.pid.Value, MemberTypeCode.Member, Util.Now, null, false);

                if (q.org.AddToSmallGroupScript.HasValue())
                {
                    var script = CurrentDatabase.Content(q.org.AddToSmallGroupScript);
                    if (script != null && script.Body.HasValue())
                    {
                        try
                        {
                            var pe = new PythonModel(Util.Host, "RegisterEvent", script.Body);
                            pe.instance.AddToSmallGroup(smallgroup, omb);
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
                omb.AddToGroup(CurrentDatabase, smallgroup);
                li.ot.Used = true;
                CurrentDatabase.SubmitChanges();

                DbUtil.LogActivity($"{votelinkSTR}{confirmSTR}: {smallgroup}", li.oid, li.pid);

                if (confirm == true)
                {
                    var subject = Util.PickFirst(setting.Subject, "no subject");
                    var msg = Util.PickFirst(setting.Body, "no message");
                    msg = APIOrganization.MessageReplacements(CurrentDatabase, q.p, q.org.DivisionName, q.org.OrganizationId, q.org.OrganizationName, q.org.Location, msg);
                    msg = msg.Replace("{details}", smallgroup);
                    var NotifyIds = CurrentDatabase.StaffPeopleForOrg(q.org.OrganizationId);

                    try
                    {
                        CurrentDatabase.Email(NotifyIds[0].FromEmail, q.p, subject, msg); // send confirmation
                    }
                    catch (Exception ex)
                    {
                        CurrentDatabase.Email(q.p.FromEmail, NotifyIds,
                            q.org.OrganizationName,
                            "There was a problem sending confirmation from org: " + ex.Message);
                    }
                    CurrentDatabase.Email(q.p.FromEmail, NotifyIds,
                        q.org.OrganizationName,
                        $"{q.p.Name} has registered for {q.org.OrganizationName}<br>{smallgroup}<br>(from votelink)");
                }
            }
            catch (Exception ex)
            {
                DbUtil.LogActivity($"{votelinkSTR}{confirmSTR}Error: {ex.Message}", li.oid, li.pid);
                return Message(ex.Message);
            }

            return Message(message);
        }

        public ActionResult RsvpLinkSg(string id, string message, bool? confirm, bool regrets = false)
        {
            var li = new LinkInfo(CurrentDatabase, rsvplinkSTR, landingSTR, id, false);
            if (li.error.HasValue())
            {
                return Message(li.error);
            }

            ViewBag.Id = id;
            ViewBag.Message = message;
            ViewBag.Confirm = confirm.GetValueOrDefault().ToString();
            ViewBag.Regrets = regrets.ToString();

            DbUtil.LogActivity($"{rsvplinkSTR}{landingSTR}: {regrets}", li.oid, li.pid);
            return View("Other/RsvpLinkSg");
        }

        [HttpPost]
        public ActionResult RsvpLinkSg(string id, string message, bool? confirm, FormCollection formCollection, bool regrets = false)
        {
            var li = new LinkInfo(CurrentDatabase, rsvplinkSTR, landingSTR, id, false);
            if (li.error.HasValue())
            {
                return Message(li.error);
            }

            try
            {
                if (!li.pid.HasValue)
                {
                    throw new Exception("missing peopleid");
                }

                var meetingid = li.a[0].ToInt();
                var emailid = li.a[2].ToInt();
                var smallgroup = li.a[3];
                if (meetingid == 0 && li.a[0].EndsWith(".next"))
                {
                    var orgid = li.a[0].Split('.')[0].ToInt();
                    var nextmeet = (from mm in CurrentDatabase.Meetings
                                    where mm.OrganizationId == orgid
                                    where mm.MeetingDate > DateTime.Now
                                    orderby mm.MeetingDate
                                    select mm).FirstOrDefault();
                    if (nextmeet == null)
                    {
                        return Message("no meeting");
                    }

                    meetingid = nextmeet.MeetingId;
                }
                var q = (from pp in CurrentDatabase.People
                         where pp.PeopleId == li.pid
                         let meeting = CurrentDatabase.Meetings.SingleOrDefault(mm => mm.MeetingId == meetingid)
                         let org = meeting.Organization
                         select new { p = pp, org, meeting }).Single();

                if (q.org.RegistrationClosed == true || q.org.OrganizationStatusId == OrgStatusCode.Inactive)
                {
                    throw new Exception("sorry, registration has been closed");
                }

                if (q.org.RegistrationTypeId == RegistrationTypeCode.None)
                {
                    throw new Exception("rsvp is no longer available");
                }

                if (q.org.Limit <= q.meeting.Attends.Count(aa => aa.Commitment == 1))
                {
                    throw new Exception("sorry, maximum limit has been reached");
                }

                var omb = OrganizationMember.Load(CurrentDatabase, li.pid.Value, q.meeting.OrganizationId) ??
                          OrganizationMember.InsertOrgMembers(CurrentDatabase,
                              q.meeting.OrganizationId, li.pid.Value, MemberTypeCode.Member, DateTime.Now, null, false);
                if (smallgroup.HasValue())
                {
                    omb.AddToGroup(CurrentDatabase, smallgroup);
                }

                li.ot.Used = true;
                CurrentDatabase.SubmitChanges();
                Attend.MarkRegistered(CurrentDatabase, li.pid.Value, meetingid, regrets ? AttendCommitmentCode.Regrets : AttendCommitmentCode.Attending);
                DbUtil.LogActivity($"{rsvplinkSTR}{confirmSTR}: {regrets}", q.org.OrganizationId, li.pid);
                var setting = CurrentDatabase.CreateRegistrationSettings(q.meeting.OrganizationId);

                if (confirm == true)
                {
                    var subject = Util.PickFirst(setting.Subject, "no subject");
                    var msg = Util.PickFirst(setting.Body, "no message");
                    msg = APIOrganization.MessageReplacements(CurrentDatabase, q.p, q.org.DivisionName, q.org.OrganizationId, q.org.OrganizationName, q.org.Location, msg);
                    msg = msg.Replace("{details}", q.meeting.MeetingDate.ToString2("f"));
                    var NotifyIds = CurrentDatabase.StaffPeopleForOrg(q.org.OrganizationId);

                    CurrentDatabase.Email(NotifyIds[0].FromEmail, q.p, subject, msg); // send confirmation
                    CurrentDatabase.Email(q.p.FromEmail, NotifyIds,
                        q.org.OrganizationName,
                        $"{q.p.Name} has registered for {q.org.OrganizationName}<br>{q.meeting.MeetingDate.ToString2("f")}");
                }
            }
            catch (Exception ex)
            {
                DbUtil.LogActivity($"{rsvplinkSTR}{confirmSTR}Error: {regrets}", peopleid: li.pid);
                return Message(ex.Message);
            }
            return Message(message);
        }

        [ValidateInput(false)]
        // ReSharper disable once FunctionComplexityOverflow
        public ActionResult RegisterLink(string id, bool? showfamily, string source)
        {
            var li = new LinkInfo(CurrentDatabase, registerlinkSTR, landingSTR, id);

            if (li.error.HasValue())
            {
                return Message(li.error);
            }

            try
            {
                if (!li.pid.HasValue)
                {
                    throw new Exception("missing peopleid");
                }

                if (!li.oid.HasValue)
                {
                    throw new Exception("missing orgid");
                }

                var linktype = li.a.Length > 3 ? li.a[3].Split(':') : "".Split(':');
                int? gsid = null;
                if (linktype[0].Equal("supportlink"))
                {
                    gsid = linktype.Length > 1 ? linktype[1].ToInt() : 0;
                }

                var q = (from pp in CurrentDatabase.People
                         where pp.PeopleId == li.pid
                         let org = CurrentDatabase.Organizations.SingleOrDefault(oo => oo.OrganizationId == li.oid)
                         let om = CurrentDatabase.OrganizationMembers.SingleOrDefault(oo => oo.OrganizationId == li.oid && oo.PeopleId == li.pid)
                         select new { p = pp, org, om }).Single();

                if (q.org == null && CurrentDatabase.Host == "trialdb")
                {
                    var oid = li.oid + Util.TrialDbOffset;
                    q = (from pp in CurrentDatabase.People
                         where pp.PeopleId == li.pid
                         let org = CurrentDatabase.Organizations.SingleOrDefault(oo => oo.OrganizationId == oid)
                         let om = CurrentDatabase.OrganizationMembers.SingleOrDefault(oo => oo.OrganizationId == oid && oo.PeopleId == li.pid)
                         select new { p = pp, org, om }).Single();
                }

                if (q.org == null)
                {
                    throw new Exception("org missing, bad link");
                }

                if (q.om == null && !gsid.HasValue && q.org.Limit <= q.org.RegLimitCount(CurrentDatabase))
                {
                    throw new Exception("sorry, maximum limit has been reached");
                }

                if (q.om == null && (q.org.RegistrationClosed == true || q.org.OrganizationStatusId == OrgStatusCode.Inactive))
                {
                    throw new Exception("sorry, registration has been closed");
                }

                DbUtil.LogActivity($"{registerlinkSTR}{landingSTR}", li.oid, li.pid);

                var url = string.IsNullOrWhiteSpace(source)
                    ? $"/OnlineReg/{li.oid}?registertag={id}"
                    : $"/OnlineReg/{li.oid}?registertag={id}&source={source}";
                if (gsid.HasValue)
                {
                    url += "&gsid=" + gsid;
                }

                if (showfamily == true)
                {
                    url += "&showfamily=true";
                }

                if (linktype[0].Equal("supportlink") && q.org.TripFundingPagesEnable && q.org.TripFundingPagesPublic)
                {
                    url = $"/OnlineReg/{li.oid}/Giving/?gsid={gsid}";
                }

                return Redirect(url);
            }
            catch (Exception ex)
            {
                DbUtil.LogActivity($"{registerlinkSTR}{landingSTR}Error: {ex.Message}", li.oid, li.pid);
                return Message(ex.Message);
            }
        }

        [ValidateInput(false)]
        public ActionResult SendLink(string id)
        {
            var li = new LinkInfo(CurrentDatabase, sendlinkSTR, landingSTR, id);
            if (li.error.HasValue())
            {
                return Message(li.error);
            }

            ViewBag.Id = id;
            DbUtil.LogActivity($"{sendlinkSTR}{landingSTR}", li.oid, li.pid);
            return View("Other/SendLink");
        }

        [HttpPost]
        [ValidateInput(false)]
        public ActionResult SendLink(string id, FormCollection formCollection)
        {
            var li = new LinkInfo(CurrentDatabase, sendlinkSTR, landingSTR, id);
            if (li.error.HasValue())
            {
                return Message(li.error);
            }

            try
            {
                if (!li.pid.HasValue)
                {
                    throw new Exception("missing peopleid");
                }

                if (!li.oid.HasValue)
                {
                    throw new Exception("missing orgid");
                }

                var queueid = li.a[2].ToInt();
                var linktype = li.a[3]; // for supportlink, this will also have the goerid
                var q = (from pp in CurrentDatabase.People
                         where pp.PeopleId == li.pid
                         let org = CurrentDatabase.LoadOrganizationById(li.oid)
                         select new { p = pp, org }).Single();

                if (q.org == null && CurrentDatabase.Host == "trialdb")
                {
                    var oid = li.oid + Util.TrialDbOffset;
                    q = (from pp in CurrentDatabase.People
                         where pp.PeopleId == li.pid
                         let org = CurrentDatabase.LoadOrganizationById(oid)
                         select new { p = pp, org }).Single();
                }

                if (q.org.RegistrationClosed == true || q.org.OrganizationStatusId == OrgStatusCode.Inactive)
                {
                    throw new Exception("sorry, registration has been closed");
                }

                if (q.org.RegistrationTypeId == RegistrationTypeCode.None)
                {
                    throw new Exception("sorry, registration is no longer available");
                }

                DbUtil.LogActivity($"{sendlinkSTR}{confirmSTR}", li.oid, li.pid);

                var expires = DateTime.Now.AddMinutes(CurrentDatabase.Setting("SendlinkExpireMinutes", "30").ToInt());
                string action = (linktype.Contains("supportlink") ? "support" : "register for");
                string minutes = CurrentDatabase.Setting("SendLinkExpireMinutes", "30");
                var c = CurrentDatabase.Content("SendLinkMessage");
                if (c == null)
                {
                    c = new Content
                    {
                        Name = "SendLinkMessage",
                        Title = "Your Link for {org}",
                        Body = @"
<p>Here is your temporary <a href='{url}'>LINK</a> to {action} {org}.</p>

<p>This link will expire at {time} ({minutes} minutes).
You may request another link by clicking the link in the original email you received.</p>

<p>Note: If you did not request this link, please ignore this email,
or contact the church if you need help.</p>
"
                    };
                    CurrentDatabase.Contents.InsertOnSubmit(c);
                    CurrentDatabase.SubmitChanges();
                }
                var url = EmailReplacements.RegisterLinkUrl(CurrentDatabase,
                    li.oid.Value, li.pid.Value, queueid, linktype, expires);
                var subject = c.Title.Replace("{org}", q.org.OrganizationName);
                var msg = c.Body.Replace("{org}", q.org.OrganizationName)
                    .Replace("{time}", expires.ToString("f"))
                    .Replace("{url}", url)
                    .Replace("{action}", action)
                    .Replace("{minutes}", minutes)
                    .Replace("%7Burl%7D", url);

                var NotifyIds = CurrentDatabase.StaffPeopleForOrg(q.org.OrganizationId);
                CurrentDatabase.Email(NotifyIds[0].FromEmail, q.p, subject, msg); // send confirmation

                return Message($"Thank you, {q.p.PreferredName}, we just sent an email to {Util.ObscureEmail(q.p.EmailAddress)} with your link...");
            }
            catch (Exception ex)
            {
                DbUtil.LogActivity($"{sendlinkSTR}{confirmSTR}Error: {ex.Message}", li.oid, li.pid);
                return Message(ex.Message);
            }
        }

        private bool IsSmallGroupFilled(Settings setting, int orgid, string sg)
        {
            var GroupTags = (from mt in CurrentDatabase.OrgMemMemTags
                             where mt.OrgId == orgid
                             select mt.MemberTag.Name).ToList();
            return setting.AskItems.Where(aa => aa.Type == "AskDropdown").Any(aa => ((AskDropdown)aa).IsSmallGroupFilled(GroupTags, sg))
                   || setting.AskItems.Where(aa => aa.Type == "AskCheckboxes").Any(aa => ((AskCheckboxes)aa).IsSmallGroupFilled(GroupTags, sg));
        }

        private const string otherRegisterlinkmaster = "Other/RegisterLinkMaster";

        public ActionResult RegisterLinkMaster(int id)
        {
            var pid = TempData["PeopleId"] as int?;
            ViewBag.Token = TempData["token"];

            var m = new OnlineRegModel { Orgid = id };
            if (User.Identity.IsAuthenticated)
            {
                return View(otherRegisterlinkmaster, m);
            }

            if (pid == null)
            {
                return Message("Must start with a registerlink");
            }

            SetHeaders(id.ToInt());
            return View(otherRegisterlinkmaster, m);
        }

        public ActionResult DropFromOrgLink(string id)
        {
            var li = new LinkInfo(CurrentDatabase, "dropfromorg", confirmSTR, id);

            ViewBag.Id = id;

            DbUtil.LogActivity($"dropfromorg click: {li.oid}", li.oid, li.pid);
            return View("Other/DropFromOrg");
        }

        [HttpPost]
        public ActionResult DropFromOrgLink(string id, FormCollection formCollection)
        {
            var li = new LinkInfo(CurrentDatabase, "dropfromorg", confirmSTR, id);
            if (li.error.HasValue())
            {
                return Message(li.error);
            }

            if (!li.oid.HasValue)
            {
                throw new Exception("orgid missing");
            }

            if (!li.pid.HasValue)
            {
                throw new Exception("peopleid missing");
            }

            var org = CurrentDatabase.LoadOrganizationById(li.oid);
            if (org == null)
            {
                throw new Exception("no such organization");
            }

            var om = CurrentDatabase.OrganizationMembers.SingleOrDefault(mm => mm.OrganizationId == li.oid && mm.PeopleId == li.pid);

            om?.Drop(CurrentDatabase);
            li.ot.Used = true;
            CurrentDatabase.SubmitChanges();

            DbUtil.LogActivity($"dropfromorg confirm: {id}", li.oid, li.pid);
            return Message($"You have been successfully removed from {org.Title ?? org.OrganizationName}");
        }

        #endregion
    }
}
