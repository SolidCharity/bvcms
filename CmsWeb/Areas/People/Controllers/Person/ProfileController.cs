using CmsData;
using CmsData.Codes;
using CmsWeb.Areas.People.Models;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace CmsWeb.Areas.People.Controllers
{
    public partial class PersonController
    {
        // Membership ---------------------------------------------------

        [HttpPost]
        public ActionResult Membership(int id)
        {
            var m = new MemberInfo(id);
            if (m.person == null)
            {
                return Content("Cannot find person");
            }

            return View("Profile/Membership/Display", m);
        }

        [HttpPost]
        public ActionResult MembershipEdit(int id)
        {
            var m = new MemberInfo(id);
            return View("Profile/Membership/Edit", m);
        }

        [HttpPost]
        public ActionResult JustAddedNotMember(int id)
        {
            var m = new MemberInfo(id);
            var newMemeberStatus = new Code.CodeInfo();
            newMemeberStatus.Value = MemberStatusCode.NotMember.ToString();

            m.MemberStatus = newMemeberStatus;
            var ret = m.UpdateMember();

            if (ret != "ok")
            {
                m.AutomationError = ret;
            }

            if (!ModelState.IsValid || ret != "ok")
            {
                return View("Profile/Membership/Edit", m);
            }
            return View("Profile/Membership/Display", m);
        }

        [HttpPost]
        public ActionResult MembershipUpdate(MemberInfo m)
        {
            var ret = m.UpdateMember();
            if (ret != "ok")
            {
                m.AutomationError = ret;
            }

            if (!ModelState.IsValid || ret != "ok")
            {
                return View("Profile/Membership/Edit", m);
            }

            DbUtil.LogPersonActivity($"Update Membership Info for: {m.person.Name}", m.PeopleId, m.person.Name);
            return View("Profile/Membership/Display", m);
        }

        // Member Documents ---------------------------------------------------

        [HttpPost]
        [Authorize(Roles = "Membership,MemberDocs")]
        public ActionResult MemberDocuments(int id)
        {
            return View("Profile/Membership/Documents", id);
        }

        [HttpPost]
        [Authorize(Roles = "Membership,MemberDocs")]
        public ActionResult UploadDocument(int id, HttpPostedFileBase doc)
        {
            if (doc == null)
            {
                return Redirect("/Person2/" + id);
            }

            var person = CurrentDatabase.People.Single(pp => pp.PeopleId == id);
            DbUtil.LogPersonActivity($"Uploading Document for {person.Name}", id, person.Name);
            person.UploadDocument(CurrentDatabase, CurrentImageDatabase, doc.InputStream, doc.FileName, doc.ContentType);
            return Redirect("/Person2/" + id);
        }

        [HttpPost]
        [Authorize(Roles = "Membership,MemberDocs")]
        public ActionResult MemberDocumentUpdateName(int pk, string name, string value)
        {
            MemberDocModel.UpdateName(CurrentDatabase, pk, value);
            return new EmptyResult();
        }

        [HttpPost, Route("DeleteDocument/{id:int}/{docid:int}")]
        [Authorize(Roles = "Membership,MemberDocs")]
        public ActionResult DeleteDocument(int id, int docid)
        {
            MemberDocModel.DeleteDocument(CurrentDatabase, CurrentImageDatabase, id, docid);
            return View("Profile/Membership/Documents", id);
        }

        // Comments ---------------------------------------------------

        [HttpPost]
        public ActionResult Comments(int id)
        {
            var m = new CommentsModel(id);
            return View("Profile/Comments/Display", m);
        }

        [HttpPost]
        public ActionResult CommentsEdit(int id)
        {
            var m = new CommentsModel(id);
            return View("Profile/Comments/Edit", m);
        }

        [HttpPost]
        public ActionResult CommentsUpdate(CommentsModel m)
        {
            m.UpdateComments();
            return View("Profile/Comments/Display", m);
        }
    }
}
