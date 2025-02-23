using CmsData;
using CmsData.Codes;
using CmsData.OnlineRegSummaryText;
using CmsData.Registration;
using CmsData.View;
using CmsWeb.Code;
using ImageData;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using UtilityExtensions;

namespace CmsWeb.Areas.Dialog.Models
{
    public class OrgMemberModel
    {
        public Organization Organization;
        public List<OrgMemMemTag> OrgMemMemTags;
        public bool IsMissionTrip;
        private bool TripFundingPagesEnable;
        private bool TripFundingPagesPublic;
        public TransactionSummary TransactionSummary;
        public string NewGroup { get; set; }
        private int? orgId;
        private int? peopleId;
        public string PassportNumber { get; set; }
        public DateTime PassportExpires { get; set; }

        private CMSDataContext _currentDatabase;
        public CMSDataContext CurrentDatabase
        {
            get => _currentDatabase ?? (_currentDatabase = DbUtil.Db);
            set => _currentDatabase = value;
        }

        private CMSImageDataContext _currentImageDatabase;
        public CMSImageDataContext CurrentImageDatabase
        {
            get => _currentImageDatabase ?? (_currentImageDatabase = CMSImageDataContext.Create(CurrentDatabase.Host));
            set => _currentImageDatabase = value;
        }

        private const string AutoOrgLeaderPromotion = "AutoOrgLeaderPromotion";
        private const string AutoOrgLeaderPromoteCustom = "AutoOrgLeaderPromoteCustom";
        private const int DefaultLeaderMemberTypeId = 140;
        private const string AccessRole = "Access";
        private const string OrgLeadersOnlyRole = "OrgLeadersOnly";

        public OrgMemberModel()
        {
        }

        public OrgMemberModel(string group, int oid, int pid)
        {
            OrgId = oid;
            PeopleId = pid;
            Group = group;
        }
        public OrgMemberModel(int oid, int pid) : this("", oid, pid) { }

        public OrganizationMember OrgMember;
        private void Populate()
        {
            if (dopopulate)
            {
                dopopulate = false;
                if (!OrgId.HasValue || !PeopleId.HasValue)
                {
                    return;
                }
            }
            var i = (from mm in CurrentDatabase.OrganizationMembers
                     where mm.OrganizationId == OrgId && mm.PeopleId == PeopleId
                     select new
                     {
                         mm,
                         mm.Person.Name,
                         mm.Organization.OrganizationName,
                         mm.Organization,
                         mm.OrgMemMemTags,
                         mm.Organization.IsMissionTrip,
                         mm.Organization.TripFundingPagesEnable,
                         mm.Organization.TripFundingPagesPublic,
                         ts = CurrentDatabase.ViewTransactionSummaries.SingleOrDefault(tt => tt.RegId == mm.TranId && tt.PeopleId == PeopleId && tt.OrganizationId == OrgId)
                     }).SingleOrDefault();
            if (i == null)
            {
                throw new Exception($"missing OrgMember at oid={OrgId}, pid={PeopleId}");
            }

            OrgMember = i.mm;
            TransactionSummary = i.ts;
            this.CopyPropertiesFrom(OrgMember);
            Name = i.Name;

            IsMissionTrip = i.IsMissionTrip ?? false;
            TripFundingPagesEnable = i.TripFundingPagesEnable;
            TripFundingPagesPublic = i.TripFundingPagesPublic;
            AmtFee = i.ts?.IndPaid + i.ts?.IndDue;
            AmtDonation = i.ts?.IndAmt - AmtFee;
            AmtCoupon = i.ts?.TotCoupon;
            AmtPaid = OrgMember.AmountPaidTransactions(CurrentDatabase);
            AmtDue = OrgMember.AmountDueTransactions(CurrentDatabase);

            OrgName = i.OrganizationName;
            Organization = i.Organization;
            OrgMemMemTags = i.OrgMemMemTags.ToList();
            Setting = CurrentDatabase.CreateRegistrationSettings(OrgId ?? 0);

            PopulatePassport();
        }

        private void PopulatePassport()
        {
            var RecReg = CurrentDatabase.RecRegs.FirstOrDefault(r => r.PeopleId == PeopleId);
            if (RecReg != null && OrgSettingsHasPassport())
            {
                PassportNumber = Util.Decrypt(RecReg.PassportNumber);
                PassportExpires = DateTime.ParseExact(Util.Decrypt(RecReg.PassportExpires), "MM/dd/yyyy", System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        private bool OrgSettingsHasPassport()
        {
            if (Setting?.AskItems != null)            
                return Setting.AskItems.Any(a => a.Type == "AskPassport");            

            return false;
        }

        public string Group { get; set; }
        public string GroupName
        {
            get
            {
                switch (Group)
                {
                    case GroupSelectCode.Member:
                        return "Member";
                    case GroupSelectCode.Pending:
                        return "Pending";
                    case GroupSelectCode.Prospect:
                        return "Prospect";
                    case GroupSelectCode.Inactive:
                        return "Inactive";
                    case GroupSelectCode.Previous:
                        return "Previous";
                }
                return null;
            }
        }

        private bool dopopulate = false;
        public int? OrgId
        {
            get { return orgId; }
            set
            {
                if (orgId != value)
                {
                    dopopulate = true;
                }

                orgId = value;
                Populate();
            }
        }

        [SkipFieldOnCopyProperties]
        public int? PeopleId
        {
            get { return peopleId; }
            set
            {
                if (peopleId != value)
                {
                    dopopulate = true;
                }

                peopleId = value;
                Populate();
            }
        }

        public string Name { get; set; }
        public string OrgName { get; set; }

        public string AttendStr { get; set; }

        public Settings Setting { get; set; }

        [TrackChanges]
        public CodeInfo MemberType { get; set; }

        [TrackChanges]
        public DateTime? InactiveDate { get; set; }

        [TrackChanges]
        //        [RegularExpression(@"\d{1,2}/\d{1,2}/(\d\d){1,2}( \d{1,2}:\d\d [AP]M){0,1}")]
        [DisplayName("Enrollment Date")]
        public DateTime? EnrollmentDate { get; set; }

        [TrackChanges]
        public bool Pending { get; set; }

        [TrackChanges]
        public string RegisterEmail { get; set; }

        [TrackChanges]
        public string Request { get; set; }

        [TrackChanges]
        public int? Grade { get; set; }

        [TrackChanges]
        public int? TranId { get; set; }

        [TrackChanges]
        public int? Tickets { get; set; }

        [TrackChanges, StringLength(50)]
        public string ShirtSize { get; set; }

        [TrackChanges]
        [DisplayName("Extra Member Info")]
        public string UserData { get; set; }

        [TrackChanges]
        public DateTime? DropDate { get; set; }


        //        [DisplayName("Total Amount")]
        //        public decimal? Amount { get; set; }

        [DisplayName("Fee")]
        public decimal? AmtFee { get; set; }

        [DisplayName("Paid")]
        public decimal? AmtPaid { get; set; }

        [DisplayName("Due")]
        public decimal? AmtDue { get; set; }

        [DisplayName("Donation")]
        public decimal? AmtDonation { get; set; }

        [DisplayName("Coupon")]
        public decimal? AmtCoupon { get; set; }

        private string transactionsLink;
        public string TransactionsLink
        {
            get
            {
                if (transactionsLink.HasValue())
                {
                    return transactionsLink;
                }

                if (OrgMember == null)
                {
                    OrgMember = CurrentDatabase.OrganizationMembers.Single(mm => mm.OrganizationId == OrgId && mm.PeopleId == PeopleId);
                }

                if (!IsMissionTrip)
                {
                    return transactionsLink = OrgMember.TranId.HasValue ? $"/Transactions/{OrgMember.TranId}" : null;
                }

                if (OrgMember.IsInGroup("Goer") && OrgMember.IsInGroup("Sender"))
                {
                    return transactionsLink = OrgMember.TranId.HasValue ? $"/Transactions/{OrgMember.TranId}?goerid={OrgMember.PeopleId}&senderid={OrgMember.PeopleId}" : null;
                }

                if (OrgMember.IsInGroup("Goer"))
                {
                    return transactionsLink = OrgMember.TranId.HasValue ? $"/Transactions/{OrgMember.TranId}?goerid={OrgMember.PeopleId}"
                        : null;
                }

                if (OrgMember.IsInGroup("Sender"))
                {
                    return transactionsLink = $"/Transactions/{0}?senderid={OrgMember.PeopleId}";
                }

                return transactionsLink = OrgMember.TranId.HasValue ? $"/Transactions/{OrgMember.TranId}" : null;
            }
        }

        public void UpdateModel()
        {
            if (OrgMember == null)
            {
                OrgMember = CurrentDatabase.OrganizationMembers.Single(mm => mm.OrganizationId == OrgId && mm.PeopleId == PeopleId);
            }

            CheckForAutoDemotion();
            CheckForAutoPromotion();

            var changes = this.CopyPropertiesTo(OrgMember);


            CurrentDatabase.SubmitChanges();
            foreach (var g in changes)
            {
                DbUtil.LogActivity($"OrgMem {GroupName} change {g.Field}", OrgId, PeopleId);
            }

            Populate();
        }

        private IEnumerable<string> _leaderTypeIds;
        private IEnumerable<string> LeaderTypeIds
        {
            get
            {
                if (_leaderTypeIds == null)
                {
                    // AttendanceTypeId of 10 is "Leader"
                    _leaderTypeIds = CurrentDatabase.MemberTypes
                        .Where(x => x.AttendanceTypeId == 10)
                        .Select(x => x.Id.ToString());
                }
                return _leaderTypeIds;
            }
        }

        private string payLink;
        public string PayLink
        {
            get
            {
                if (payLink.HasValue())
                {
                    return payLink;
                }

                return payLink = GetPayLink(OrgId, PeopleId);
            }
        }
        public static string GetPayLink(int? oid, int? pid, CMSDataContext db = null)
        {
            if (!pid.HasValue)
            {
                return null;
            }

            db = db ?? DbUtil.Db;

            var om = db.OrganizationMembers.SingleOrDefault(mm => mm.OrganizationId == oid && mm.PeopleId == pid);
            return om?.PayLink2(db);
        }
        private string supportLink;

        public string SupportLink
        {
            get
            {
                if (supportLink.HasValue())
                {
                    return supportLink;
                }

                if (OrgMember == null)
                {
                    OrgMember = CurrentDatabase.OrganizationMembers.Single(mm => mm.OrganizationId == OrgId && mm.PeopleId == PeopleId);
                }

                if (IsMissionTrip && TripFundingPagesEnable && TripFundingPagesPublic)
                {
                    return supportLink = CurrentDatabase.ServerLink($"/OnlineReg/{OrgId}/Giving/{PeopleId}");
                }
                if (IsMissionTrip && OrgMember.MemberTypeId == MemberTypeCode.Member)
                {
                    return supportLink = CurrentDatabase.ServerLink($"/OnlineReg/{OrgId}?goerid={PeopleId}");
                }

                return null;
            }
        }

        [Display(Description = @"
Checking the Remove From Enrollment History box will erase all enrollment history (but not attendance) of this person in this organization.
**There is no undo.**")]
        public bool RemoveFromEnrollmentHistory { get; set; }

        public void Drop()
        {
            if (OrgMember == null)
            {
                OrgMember = CurrentDatabase.OrganizationMembers.Single(mm => mm.OrganizationId == OrgId && mm.PeopleId == PeopleId);
            }

            if (DropDate.HasValue)
            {
                OrgMember.Drop(CurrentDatabase, CurrentImageDatabase, DropDate.Value);
            }
            else
            {
                OrgMember.Drop(CurrentDatabase, CurrentImageDatabase);
            }

            CurrentDatabase.SubmitChanges();
            DbUtil.LogActivity("OrgMem Drop", OrgId, PeopleId);
            if (RemoveFromEnrollmentHistory)
            {
                CurrentDatabase.RemoveFromEnrollmentHistory(OrgId.Value, PeopleId.Value);
                CurrentDatabase.SubmitChanges();
                DbUtil.LogActivity("OrgMem RemoveEnrollmentHistory", OrgId, PeopleId);
            }
        }
        public string SmallGroupChanged(int sgtagid, bool ck)
        {
            if (OrgMember == null)
            {
                OrgMember = CurrentDatabase.OrganizationMembers.Single(mm => mm.OrganizationId == OrgId && mm.PeopleId == PeopleId);
            }

            if (OrgMember == null)
            {
                return "error";
            }

            if (ck)
            {
                var name = (from mt in CurrentDatabase.MemberTags
                            where mt.Id == sgtagid
                            where mt.OrgId == OrgId
                            select mt.Name).Single();
                OrgMember.OrgMemMemTags.Add(new OrgMemMemTag { MemberTagId = sgtagid });
                DbUtil.LogActivity("OrgMem AddSubGroup " + name, OrgId, PeopleId);
            }
            else
            {
                var i = (from mt in OrgMember.OrgMemMemTags
                         where mt.MemberTagId == sgtagid
                         select new
                         {
                             mt,
                             name = mt.MemberTag.Name
                         }).SingleOrDefault();
                if (i == null)
                {
                    return "not found";
                }

                CurrentDatabase.OrgMemMemTags.DeleteOnSubmit(i.mt);
                DbUtil.LogActivity("OrgMem RemoveSubGroup " + i.name, OrgId, PeopleId);
            }
            CurrentDatabase.SubmitChanges();
            return "ok";
        }

        public IEnumerable<OrgMemberQuestion> RegQuestions()
        {
            var q = CurrentDatabase.OrgMemberQuestions(OrgId, PeopleId);
            return q;
        }

        public IEnumerable<OrgMemberDocuments> MemberDocuments()
        {
            return CurrentDatabase.OrgMemberDocuments.Where(o => o.PeopleId == PeopleId && o.OrganizationId == OrgId);
        }

        public void AddQuestions()
        {
            if (OrgId == null)
            {
                return;
            }

            var r = OnlineRegPersonModel0.CreateFromSettings(CurrentDatabase, OrgId.Value);
            OrgMember.OnlineRegData = r.WriteXml();
            CurrentDatabase.SubmitChanges();
        }

        public void UpdateQuestion(int n, string type, string answer)
        {
            var q = CurrentDatabase.OrgMemberQuestions(OrgId, PeopleId).ToList();
            var rq = q.SingleOrDefault(vv => vv.Row == n);
            if (rq == null)
            {
                throw new Exception("question not found");
            }

            var question = rq.Question;

            var r = new OnlineRegPersonModel0(OrgMember.OnlineRegData);
            switch (type)
            {
                case "question":
                    r.ExtraQuestion[rq.SetX ?? 0][question] = answer;
                    break;
                case "text":
                    r.Text[rq.SetX ?? 0][question] = answer;
                    break;
            }
            OrgMember.OnlineRegData = r.WriteXml();
            CurrentDatabase.SubmitChanges();
        }
        public void DeleteQuestion(int n, string type)
        {
            var q = CurrentDatabase.OrgMemberQuestions(OrgId, PeopleId).ToList();
            var rq = q.SingleOrDefault(vv => vv.Row == n);
            if (rq == null)
            {
                throw new Exception("question not found");
            }

            var question = rq.Question;

            var r = new OnlineRegPersonModel0(OrgMember.OnlineRegData);
            switch (type)
            {
                case "question":
                    r.ExtraQuestion[rq.SetX ?? 0].Remove(question);
                    break;
                case "text":
                    r.Text[rq.SetX ?? 0].Remove(question);
                    break;
            }
            OrgMember.OnlineRegData = r.WriteXml();
            CurrentDatabase.SubmitChanges();
        }

        private void CheckForAutoPromotion()
        {
            var autoLeaderOrgPromotionSetting = CurrentDatabase.Setting(AutoOrgLeaderPromotion);
            if (!autoLeaderOrgPromotionSetting)
            {
                return;
            }

            var isPromotingToLeader = !LeaderTypeIds.Contains(OrgMember.MemberTypeId.ToString()) &&
                                      LeaderTypeIds.Contains(MemberType.Value);

            var autoPromoteCustom = GetAutoPromoteCustomSetting();
            if (autoPromoteCustom != null && autoPromoteCustom.NewMemberType != MemberType.ToString())
            {
                autoPromoteCustom = null;
            }

            var users = CurrentDatabase.Users.Where(us => us.PeopleId == PeopleId);
            if (!users.Any())
            {
                return;
            }

            foreach (var user in users)
            {
                if (!user.Roles.Any() && isPromotingToLeader)
                {
                    user.AddRoles(CurrentDatabase,
                        !user.InRole(AccessRole)
                            ? new[] { AccessRole, OrgLeadersOnlyRole }
                            : new[] { OrgLeadersOnlyRole });
                }
                if (autoPromoteCustom != null)
                {
                    user.AddRoles(CurrentDatabase, autoPromoteCustom.NewRole);
                }
            }
        }

        private void CheckForAutoDemotion()
        {
            var autoLeaderOrgPromotionSetting = CurrentDatabase.Setting(AutoOrgLeaderPromotion);
            if (!autoLeaderOrgPromotionSetting)
            {
                return;
            }

            var autoPromoteCustom = GetAutoPromoteCustomSetting();

            var isPromotingFromLeader = LeaderTypeIds.Contains(OrgMember.MemberTypeId.ToString()) &&
                                        !LeaderTypeIds.Contains(MemberType.Value);

            var users = CurrentDatabase.Users.Where(us => us.PeopleId == PeopleId);
            if (!users.Any())
            {
                return;
            }

            foreach (var user in users)
            {
                if (autoPromoteCustom != null && user.Roles.Contains(autoPromoteCustom.NewRole))
                {
                    if (user.Roles.Count() == 3 && user.InRole(OrgLeadersOnlyRole) && user.InRole(AccessRole) && user.InRole(autoPromoteCustom.NewRole) &&
                        !CurrentDatabase.OrganizationMembers.Any(x => x.MemberType.Id == (OrgMember.Organization.LeaderMemberTypeId > 0 ? OrgMember.Organization.LeaderMemberTypeId : DefaultLeaderMemberTypeId)
                            && x.PeopleId == PeopleId && x.OrganizationId != Organization.OrganizationId))
                    {
                        user.RemoveRoles(CurrentDatabase, autoPromoteCustom.NewRole);
                    }
                }

                if (isPromotingFromLeader)
                {
                    if (user.Roles.Count() == 2 && user.InRole(OrgLeadersOnlyRole) && user.InRole(AccessRole) &&
                        !CurrentDatabase.OrganizationMembers.Any(
                            x => x.MemberType.Id ==
                                (OrgMember.Organization.LeaderMemberTypeId > 0
                                    ? OrgMember.Organization.LeaderMemberTypeId
                                    : DefaultLeaderMemberTypeId)
                                && x.PeopleId == PeopleId && x.OrganizationId != Organization.OrganizationId))
                    {
                        // Resets their roles back to a "MyData" user
                        user.SetRoles(CurrentDatabase, new string[] { });
                    }
                }
            }
        }
        public string AddNewSmallGroup(int pid)
        {
            var o = CurrentDatabase.LoadOrganizationById(OrgId);
            var mt = new MemberTag { Name = NewGroup };
            o.MemberTags.Add(mt);
            CurrentDatabase.SubmitChanges();
            DbUtil.LogActivity("OrgMem AddNewSubGroup " + NewGroup, OrgId);
            NewGroup = null;
            return SmallGroupChanged(mt.Id, true);
        }

        private AutoPromoteCustomSettings GetAutoPromoteCustomSetting()
        {
            var settingContent = CurrentDatabase.Setting(AutoOrgLeaderPromoteCustom, "");
            if (string.IsNullOrWhiteSpace(settingContent))
            {
                return null;
            }

            var values = settingContent.Split(new[] { "__" }, StringSplitOptions.RemoveEmptyEntries);
            if (values.Length != 2)
            {
                return null;
            }

            return new AutoPromoteCustomSettings
            {
                NewMemberType = values[0],
                NewRole = values[1]
            };
        }

        private class AutoPromoteCustomSettings
        {
            public string NewMemberType { get; set; }
            public string NewRole { get; set; }
        }
    }
}
