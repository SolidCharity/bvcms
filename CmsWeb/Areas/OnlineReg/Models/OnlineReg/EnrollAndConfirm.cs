using CmsData;
using CmsData.API;
using CmsData.Registration;
using CmsData.View;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UtilityExtensions;
using CmsWeb.Code;
using HtmlAgilityPack;

namespace CmsWeb.Areas.OnlineReg.Models
{
    public partial class OnlineRegModel
    {
        private readonly Regex donationtext = new Regex(@"\{donation(?<text>.*)donation\}", RegexOptions.Singleline | RegexOptions.Multiline);
        private Settings _masterSettings;
        private List<Person> _notifyIds;
        private string _subject;
        private List<MailAddress> listMailAddress;
        public bool UsedAdminsForNotify;

        public bool EnrollAndConfirm()
        {
            ExpireRegisterTag();
            AddPeopleToTransaction();

            if (masterorgid.HasValue)
            {
                return EnrollAndConfirmMultipleOrgs();
            }

            if (SupportMissionTrip)
                List[0].isMissionTripSupporter = true;

            if (SupportMissionTrip && TotalAmount() > 0)
            {
                List[0].Enroll(Transaction, List[0].GetPayLink());
                return DoMissionTripSupporter();
            }
            var message = DoEnrollments();

            if (IsMissionTripGoerWithPayment())
            {
                DoMissionTripGoer();
            }
            else if (Transaction.Donate > 0)
            {
                message = DoDonationModifyMessage(message);
            }
            else
            {
                message = donationtext.Replace(message, "");
            }

            SendAllConfirmations(message);

            return true;
        }

        private bool EnrollAndConfirmMultipleOrgs()
        {
            foreach (var p in List)
            {
                p.Enroll(Transaction, p.GetPayLink());
                p.DoGroupToJoin();
                p.CheckNotifyDiffEmails();

                if (p.IsCreateAccount())
                {
                    p.CreateAccount();
                }

                CurrentDatabase.SubmitChanges();
            }
            foreach (var p in List)
            {
                SendSingleConfirmationForOrg(p);
            }

            return true;
        }

        private bool IsMissionTripGoerWithPayment()
        {
            return org != null
                   && Transaction.Amt > 0
                   && org.IsMissionTrip == true
                   && SupportMissionTrip == false;
        }

        private void SendAllConfirmations(string message)
        {
            CurrentDatabase.SetCurrentOrgId(org.OrganizationId);
            var subject = GetSubject();
            var amtpaid = Transaction.Amt ?? 0;

            var firstPerson = List[0].person;
            if (user != null)
            {
                firstPerson = user;
            }

            var notifyIds = GetNotifyIds();
            if (subject != "DO NOT SEND")
            {
                CurrentDatabase.Email(notifyIds[0].FromEmail, firstPerson, listMailAddress, subject, message, false);
                Log("SentConfirmations");
            }

            CurrentDatabase.SubmitChanges();
            // notify the staff
            foreach (var p in List)
            {
                var messageNotice = UsedAdminsForNotify
                    ? @"<span style='color:red'>THERE ARE NO NOTIFY IDS ON THIS REGISTRATION!!</span><br/>
<a href='https://docs.touchpointsoftware.com/OnlineRegistration/MessagesSettings.html'>see documentation</a><br/><br/>"
                    : "";

                var detailSection = GetDetailsSection();

                if(ValidateEmailRecipientRegistrant(p.person.Name, detailSection))
                {
                    CurrentDatabase.Email(Util.PickFirst(p.person.FromEmail, notifyIds[0].FromEmail), notifyIds, Header,
                        $@"{messageNotice}{p.person.Name} has registered for {Header}<br/><hr>{detailSection}");
                }
                else
                {
                    CurrentDatabase.LogActivity($"Person ({p.person.Name}) is different from the registrant in the email body. " +
                        $"The email was not sent.");
                }
            }
        }

        public static bool ValidateEmailRecipientRegistrant(string name, string detailSection)
        {
            //some users use to include <br> tags in his emails but this tag is not recognized by xdocument.
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(detailSection);
            IEnumerable<string> childList = from el in htmlDoc.DocumentNode.Descendants("registrant")
                                            select el.InnerText;

            return childList.Any(p => p == name);
        }

        private TransactionSummary transactionSummary;
        public TransactionSummary TransactionSummary()
        {
            if (transactionSummary == null)
            {
                transactionSummary = CurrentDatabase.ViewTransactionSummaries.SingleOrDefault(tt => tt.RegId == Transaction.OriginalId && tt.PeopleId == List[0].PeopleId);
            }

            return transactionSummary;
        }

        private void SendSingleConfirmationForOrg(OnlineRegPersonModel p)
        {
            var ts = TransactionSummary();
            CurrentDatabase.SetCurrentOrgId(p.orgid);
            var emailSubject = GetSubject(p);
            var message = p.GetMessage();
            var details = "";
            if (message.Contains("{details}"))
            {
                details = p.PrepareSummaryText(CurrentDatabase);
                message = message.Replace("{details}", details);
            }
            var notifyIds = CurrentDatabase.StaffPeopleForOrg(p.org.OrganizationId);
            var notify = notifyIds[0];

            var location = p.org.Location;
            if (!location.HasValue())
            {
                location = masterorg.Location;
            }

            message = APIOrganization.MessageReplacements(CurrentDatabase, p.person,
                masterorg.OrganizationName, p.org.OrganizationId, p.org.OrganizationName, location, message);

            if (Transaction.Donate > 0 && p == List[donor ?? 0])
            {
                message = DoDonationModifyMessage(message);
            }
            else
            {
                message = donationtext.Replace(message, "");
            }

            // send confirmations
            if (emailSubject != "DO NOT SEND")
            {
                CurrentDatabase.Email(notify.FromEmail, p.person, Util.EmailAddressListFromString(p.fromemail),
                    emailSubject, message, false);
                Log("SentConfirmation");
            }
            // notify the staff
            CurrentDatabase.Email(Util.PickFirst(p.person.FromEmail, notify.FromEmail),
                notifyIds, Header,
                $@"{p.person.Name} has registered for {Header}<br/>
Feepaid for this registrant: {p.AmountToPay():C}<br/>
Others in this registration session: {p.GetOthersInTransaction(Transaction)}<br/>
Total Fee paid for this registration session: {ts?.TotPaid:C}<br/>
<pre>{details}</pre>");
        }

        private Settings GetMasterOrgSettings()
        {
            if (_masterSettings != null)
            {
                return _masterSettings;
            }

            if (masterorgid == null)
            {
                return null;
            }

            if (settings == null)
            {
                return null;
            }

            if (!settings.ContainsKey(masterorgid.Value))
            {
                return null;
            }

            ParseSettings();
            return _masterSettings = settings[masterorgid.Value];
        }

        private string DoDonationModifyMessage(string message)
        {
            var p = List[donor ?? 0];
            Transaction.Fund = p.setting.DonationFund();
            var desc = $"{p.person.Name}; {p.person.PrimaryAddress}; {p.person.PrimaryCity}, {p.person.PrimaryState} {p.person.PrimaryZip}";
            if (!Transaction.TransactionId.StartsWith("Coupon") && Transaction.Donate.HasValue)
            {
                p.person.PostUnattendedContribution(CurrentDatabase, Transaction.Donate.Value, p.setting.DonationFundId, desc,
                    tranid: Transaction.Id);
                Log("ExtraDonation");
            }
            var subject = GetSubject(p);
            var ma = donationtext.Match(message);
            if (ma.Success)
            {
                var v = ma.Groups["text"].Value;
                message = donationtext.Replace(message, v);
            }
            message = message.Replace("{donation}", Transaction.Donate.ToString2("N2"));
            // send donation confirmations
            var notifyIds = GetNotifyIds(p);
            var notice = $"${Transaction.Donate:N2} donation received from {Transaction.FullName(Transaction)} on behalf of {p.person.Name}";
            CurrentDatabase.Email(notifyIds[0].FromEmail, notifyIds, subject + "-donation", notice);
            return message;
        }

        private void DoMissionTripGoer()
        {
            var p = List[0];
            Transaction.Fund = p.setting.DonationFund();
            if (!p.orgid.HasValue || !p.PeopleId.HasValue)
            {
                throw new Exception(
                    $"DoMissionTripGoer missing org or person: orgid={p.orgid ?? 0} or peopleid={p.PeopleId ?? 0}");
            }

            CurrentDatabase.GoerSenderAmounts.InsertOnSubmit(
                new GoerSenderAmount
                {
                    Amount = Transaction.Amt,
                    GoerId = p.PeopleId,
                    Created = DateTime.Now,
                    OrgId = p.orgid.Value,
                    SupporterId = p.PeopleId.Value
                });
            if (Transaction.TransactionId.StartsWith("Coupon") || !Transaction.Amt.HasValue)
            {
                return;
            }

            p.person.PostUnattendedContribution(CurrentDatabase,
                Transaction.Amt.Value, p.setting.DonationFundId,
                $"MissionTrip: org={p.orgid}; goer={p.PeopleId}", tranid: Transaction.Id);
            Log("GoerPayment");
            //Transaction.Description = "Mission Trip Giving";
        }

        private List<Person> GetNotifyIds(OnlineRegPersonModel p)
        {
            if (_notifyIds != null)
            {
                return _notifyIds;
            }

            return _notifyIds = CurrentDatabase.StaffPeopleForOrg(p.org.OrganizationId, out UsedAdminsForNotify);
        }
        private List<Person> GetNotifyIds()
        {
            if (_notifyIds != null)
            {
                return _notifyIds;
            }

            return _notifyIds = CurrentDatabase.StaffPeopleForOrg(org.OrganizationId, out UsedAdminsForNotify);
        }

        private bool DoMissionTripSupporter()
        {
            var notifyIds = GetNotifyIds();
            var p = List[0];
            Transaction.Fund = p.setting.DonationFund();
            var goerid = p.Parent.GoerId > 0
                ? p.Parent.GoerId
                : p.MissionTripGoerId;
            var forgoer = "";
            var forgeneral = "";
            if (p.MissionTripSupportGoer > 0 && p.orgid.HasValue && p.PeopleId.HasValue)
            {
                var gsa = new GoerSenderAmount
                {
                    Amount = p.MissionTripSupportGoer ?? 0,
                    Created = DateTime.Now,
                    OrgId = p.orgid.Value,
                    SupporterId = p.PeopleId.Value,
                    NoNoticeToGoer = p.MissionTripNoNoticeToGoer
                };
                if (goerid > 0)
                {
                    gsa.GoerId = goerid;
                }

                CurrentDatabase.GoerSenderAmounts.InsertOnSubmit(gsa);
                if (p.Parent.GoerSupporterId.HasValue)
                {
                    var gs = CurrentDatabase.GoerSupporters.Single(gg => gg.Id == p.Parent.GoerSupporterId);
                    if (!gs.SupporterId.HasValue)
                    {
                        gs.SupporterId = p.PeopleId;
                    }
                }
                if (!Transaction.TransactionId.StartsWith("Coupon"))
                {
                    p.person.PostUnattendedContribution(CurrentDatabase,
                        p.MissionTripSupportGoer ?? 0, p.setting.DonationFundId,
                        $"SupportMissionTrip: org={p.orgid}; goer={goerid}", tranid: Transaction.Id);
                    Log("GoerSupport");
                    // send notices
                    if (goerid > 0 && !p.MissionTripNoNoticeToGoer)
                    {
                        var goer = CurrentDatabase.LoadPersonById(goerid.Value);
                        forgoer = $", {p.MissionTripSupportGoer:c} for {goer.Name}";
                        CurrentDatabase.Email(notifyIds[0].FromEmail, goer, org.OrganizationName + "-donation",
                            $"{p.MissionTripSupportGoer ?? 0:C} donation received from {Transaction.FullName(Transaction)}{forgoer}");
                    }
                }
            }
            if (p.MissionTripSupportGeneral > 0 && p.orgid.HasValue && p.PeopleId.HasValue)
            {
                CurrentDatabase.GoerSenderAmounts.InsertOnSubmit(
                    new GoerSenderAmount
                    {
                        Amount = p.MissionTripSupportGeneral ?? 0,
                        Created = DateTime.Now,
                        OrgId = p.orgid.Value,
                        SupporterId = p.PeopleId.Value
                    });
                forgeneral = $", ({p.MissionTripSupportGeneral ?? 0:c}) for trip";
                if (!Transaction.TransactionId.StartsWith("Coupon"))
                {
                    p.person.PostUnattendedContribution(CurrentDatabase,
                        p.MissionTripSupportGeneral ?? 0, p.setting.DonationFundId,
                        "SupportMissionTrip: org=" + p.orgid, tranid: Transaction.Id);
                    Log("TripSupport");
                }
            }
            var notifyids = CurrentDatabase.NotifyIds(org.GiftNotifyIds);
            CurrentDatabase.Email(notifyIds[0].FromEmail, notifyids, org.OrganizationName + "-donation",
                $"${Transaction.Amt:N2} donation received from {Transaction.FullName(Transaction)}{forgoer}{forgeneral}");

            var orgsettings = settings[org.OrganizationId];
            var senderSubject = orgsettings.SenderSubject ?? "NO SUBJECT SET";
            var senderBody = orgsettings.SenderBody ?? "NO SENDEREMAIL MESSAGE HAS BEEN SET";
            senderBody = APIOrganization.MessageReplacements(CurrentDatabase, p.person,
                org.DivisionName, org.OrganizationId, org.OrganizationName, org.Location, senderBody);
            senderBody = senderBody.Replace("{phone}", org.PhoneNumber.FmtFone7());
            senderBody = senderBody.Replace("{paid}", Transaction.Amt.ToString2("c"));

            //Transaction.Description = "Mission Trip Giving";
            CurrentDatabase.Email(notifyids[0].FromEmail, p.person, listMailAddress, senderSubject, senderBody, false);
            CurrentDatabase.SubmitChanges();
            return true;
        }

        private string defaultSubject => $"Confirmation for {Header}";

        private string GetSubject(OnlineRegPersonModel p)
        {
            var os = GetMasterOrgSettings();
            return Util.PickFirst(p.setting?.Subject, os?.Subject, defaultSubject);
        }

        private string GetSubject()
        {
            if (_subject.HasValue())
            {
                return _subject;
            }

            var orgsettings = settings[org.OrganizationId];
            _subject = Util.PickFirst(orgsettings.Subject, defaultSubject);
            if (Header.HasValue())
            {
                return _subject = _subject.Replace("{org}", Header);
            }

            return _subject;
        }

        private string DoEnrollments()
        {
            foreach (var p in List)
            {
                p.Enroll(Transaction, p.GetPayLink());
                p.DoGroupToJoin();
                p.CheckNotifyDiffEmails();

                if (p.IsCreateAccount())
                {
                    p.CreateAccount();
                }

                CurrentDatabase.SubmitChanges();
            }
            var message = List[0].GetMessage();
            if (!message.Contains("{details}"))
            {
                return message;
            }

            var details = GetDetailsSection();
            message = message.Replace("{details}", details);
            return message;
        }

        public string GetDetailsSection()
        {
            var details = new StringBuilder();
            if (Transaction?.Amt > 0)
            {
                details.Append(List[0].SummaryTransaction());
            }

            foreach (var p in List)
            {
                details.Append(p.PrepareSummaryText(CurrentDatabase));
            }

            return details.ToString();
        }


        private void AddPeopleToTransaction()
        {
            listMailAddress = GetEmailList();
            var participants = GetParticipants(listMailAddress);
            var transactionPeople = new List<TransactionPerson>();
            foreach (var p in List)
            {
                if (p.PeopleId == null)
                {
                    continue;
                }

                if (transactionPeople.Any(pp => pp.PeopleId == p.PeopleId))
                {
                    continue;
                }

                var tp = new TransactionPerson
                {
                    PeopleId = p.PeopleId.Value,
                    Amt = p.TotalAmount(),
                    OrgId = p.orgid ?? Orgid
                };
                tp.Donor = Transaction.Donate > 0 && p == List[donor ?? 0];
                transactionPeople.Add(tp);
            }

            if (SupportMissionTrip && GoerId == _list[0].PeopleId)
            {
                // reload transaction because it is not in this context
                var om = CurrentDatabase.OrganizationMembers.SingleOrDefault(mm => mm.PeopleId == GoerId && mm.OrganizationId == Orgid);
                if (om != null && om.TranId.HasValue)
                {
                    Transaction.OriginalId = om.TranId;
                }
            }
            else
            {
                Transaction.OriginalTrans.TransactionPeople.AddRange(transactionPeople);
            }
            Transaction.Emails = listMailAddress.EmailAddressListToString();
            Transaction.Participants = participants;
            Transaction.TransactionDate = Util.Now;
        }

        private string GetParticipants(List<MailAddress> elist)
        {
            var participants = new StringBuilder();
            for (var i = 0; i < List.Count; i++)
            {
                var p = List[i];
                if (p.IsNew)
                {
                    Person uperson = null;
                    if (i > 0)
                    {
                        if (List[i].AddressLineOne.HasValue() && List[i].AddressLineOne == List[i - 1].AddressLineOne)
                        {
                            uperson = List[i - 1].person; // add to previous family
                        }
                    }
                    p.AddPerson(uperson, p.org.EntryPointId ?? 0);
                }
                Util.AddGoodAddress(elist, p.fromemail);
                participants.Append(p);
            }
            return participants.ToString();
        }

        private void ExpireRegisterTag()
        {
            if (registertag.HasValue() && registerLinkType != "masterlink")
            {
                var guid = registertag.ToGuid();
                var ot = CurrentDatabase.OneTimeLinks.SingleOrDefault(oo => oo.Id == guid.Value);
                ot.Used = true;
            }
        }

        private List<MailAddress> GetEmailList()
        {
            var elist = new List<MailAddress>();
            if (UserPeopleId.HasValue)
            {
                if (user.SendEmailAddress1 ?? true)
                {
                    Util.AddGoodAddress(elist, user.FromEmail);
                }

                if (user.SendEmailAddress2 ?? false)
                {
                    Util.AddGoodAddress(elist, user.FromEmail2);
                }
            }
            return elist;
        }

        public void UseCoupon(string TransactionID, decimal AmtPaid)
        {
            var matchcoupon = @"Coupon\((?<coupon>[^)]*)\)";
            if (Regex.IsMatch(TransactionID, matchcoupon, RegexOptions.IgnoreCase))
            {
                var match = Regex.Match(TransactionID, matchcoupon, RegexOptions.IgnoreCase);
                var coup = match.Groups["coupon"];
                var coupon = "";
                if (coup != null)
                {
                    coupon = coup.Value.Replace(" ", "");
                }

                if (coupon != "Admin")
                {
                    var c = CurrentDatabase.Coupons.SingleOrDefault(cp => cp.Id == coupon);
                    if (c == null)
                    {
                        return;
                    }

                    c.RegAmount = AmtPaid;
                    c.Used = DateTime.Now;
                    c.PeopleId = List[0].PeopleId;
                    Log("CouponUsed");
                }
                else
                {
                    Log("AdminCouponUsed");
                }
            }
        }

        public void ConfirmReregister()
        {
            var p = List[0];
            var message = CurrentDatabase.ContentHtml("ReregisterLinkEmail", @"Hi {name},
<p>Here is your <a href=""{url}"">MANAGE REGISTRATION</a> link to manage {orgname}. This link will work only once. Creating an account will allow you to do this again without having to email the link.</p>");
            message = message.Replace("{orgname}", Header).Replace("{org}", Header);

            var Staff = CurrentDatabase.StaffPeopleForOrg(Orgid.Value);

            p.SendOneTimeLink(Staff.First().FromEmail,
                CurrentDatabase.ServerLink("/OnlineReg/RegisterLink/"), "Manage Your Registration for " + Header, message);
            Log("SendReRegisterLink");
        }

        public ConfirmEnum ConfirmManageSubscriptions()
        {
            var p = List[0];
            if (p.IsNew)
            {
                p.AddPerson(null, GetEntryPoint());
            }

            if (p.CreatingAccount)
            {
                p.CreateAccount();
            }

            var c = CurrentDatabase.Content("OneTimeConfirmation");
            if (c == null)
            {
                c = new Content();
                c.Name = "OneTimeConfirmation";
                c.Title = "Manage Your Subscriptions";
                c.Body = @"Hi {name},
<p>Here is your <a href=""{url}"">link</a> to manage your subscriptions. (note: it will only work once for security reasons)</p>
";
                CurrentDatabase.Contents.InsertOnSubmit(c);
                CurrentDatabase.SubmitChanges();
            }

            var Staff = CurrentDatabase.StaffPeopleForOrg(masterorgid.Value);

            p.SendOneTimeLink(
                Staff.First().FromEmail,
                CurrentDatabase.ServerLink("/OnlineReg/ManageSubscriptions/"), c.Title, c.Body);
            Log("SendOneTimeLinkManageSub");
            return ConfirmEnum.ConfirmAccount;
        }

        private ConfirmEnum ConfirmPickSlots()
        {
            var p = List[0];
            if (p.IsNew)
            {
                p.AddPerson(null, GetEntryPoint());
            }

            if (p.CreatingAccount)
            {
                p.CreateAccount();
            }

            var c = DbUtil.Content(CurrentDatabase, "OneTimeConfirmationVolunteer");
            if (c == null)
            {
                c = new Content();
                c.Name = "OneTimeConfirmationVolunteer";
                c.Title = "Manage Your Volunteer Commitments";
                c.Body = @"Hi {name},
<p>Here is your <a href=""{url}"">link</a> to manage your volunteer commitments. (note: it will only work once for security reasons)</p>";
                CurrentDatabase.Contents.InsertOnSubmit(c);
                CurrentDatabase.SubmitChanges();
            }

            List<Person> Staff = null;
            Staff = CurrentDatabase.StaffPeopleForOrg(Orgid.Value);

            p.SendOneTimeLink(
                Staff.First().FromEmail,
                CurrentDatabase.ServerLink("/OnlineReg/ManageVolunteer/"), c.Title, c.Body);
            Log("SendOneTimeLinkManageVol");
            URL = null;
            return ConfirmEnum.ConfirmAccount;
        }

        internal ConfirmEnum SendLinkForPledge()
        {
            var p = List[0];
            if (p.IsNew)
            {
                p.AddPerson(null, p.org.EntryPointId ?? 0);
            }

            if (p.CreatingAccount)
            {
                p.CreateAccount();
            }

            var c = DbUtil.Content(CurrentDatabase, "OneTimeConfirmationPledge");
            if (c == null)
            {
                c = new Content();
                c.Name = "OneTimeConfirmationPledge";
                c.Title = "Manage your pledge";
                c.Body = @"Hi {name},
<p>Here is your <a href=""{url}"">link</a> to manage your pledge. (note: it will only work once for security reasons)</p> ";
                CurrentDatabase.Contents.InsertOnSubmit(c);
                CurrentDatabase.SubmitChanges();
            }

            p.SendOneTimeLink(
                CurrentDatabase.StaffPeopleForOrg(Orgid.Value).First().FromEmail,
                CurrentDatabase.ServerLink("/OnlineReg/ManagePledge/"), c.Title, c.Body);
            Log("SendOneTimeLinkManagePledge");
            return ConfirmEnum.ConfirmAccount;
        }

        internal ConfirmEnum SendLinkToManageGiving()
        {
            var p = List[0];
            if (p.IsNew)
            {
                p.AddPerson(null, p.org.EntryPointId ?? 0);
            }

            if (p.CreatingAccount)
            {
                p.CreateAccount();
            }

            var c = DbUtil.Content(CurrentDatabase, "OneTimeManageGiving");
            if (c == null)
            {
                c = new Content();
                c.Name = "OneTimeManageGiving";
                c.Title = "Manage your recurring giving";
                c.Body = @"Hi {name},
<p>Here is your <a href=""{url}"">link</a> to manage your recurring giving. (note: it will only work once for security reasons)</p> ";
                CurrentDatabase.Contents.InsertOnSubmit(c);
                CurrentDatabase.SubmitChanges();
            }

            var parameters = new List<string>
            {
                $"{(!string.IsNullOrWhiteSpace(Campus) ? $"campus={Campus}" : string.Empty)}",
                $"{(!string.IsNullOrWhiteSpace(DefaultFunds) ? $"funds={DefaultFunds}" : string.Empty)}"
            };

            var appendQueryString = string.Join("&", parameters.Where(i => !string.IsNullOrEmpty(i)));

            p.SendOneTimeLink(
                CurrentDatabase.StaffPeopleForOrg(Orgid.Value).First().FromEmail,
                CurrentDatabase.ServerLink($"/OnlineReg/ManageGiving/"), c.Title, c.Body, appendQueryString);
            Log("SendOneTimeLinkManageGiving");
            return ConfirmEnum.ConfirmAccount;
        }

        public int GetEntryPoint()
        {
            if (org != null && org.EntryPointId != null)
            {
                return org.EntryPointId.Value;
            }

            if (masterorg != null && masterorg.EntryPointId != null)
            {
                return masterorg.EntryPointId.Value;
            }

            return 0;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendFormat("orgid: {0}<br/>\n", Orgid);
            sb.AppendFormat("masterorgid: {0}<br/>\n", masterorgid);
            sb.AppendFormat("userid: {0}<br/>\n", UserPeopleId);
            foreach (var li in List)
            {
                sb.AppendFormat("--------------------------------\nList: {0}<br/>\n", li.Index);
                sb.Append(li);
            }
            return sb.ToString();
        }
    }
}
