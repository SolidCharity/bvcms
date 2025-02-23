﻿using CmsData;
using CMSWebTests;
using IntegrationTests.Support;
using SharedTestFixtures;
using Shouldly;
using Xunit;

namespace IntegrationTests.Areas.Reports.Views.Reports
{
    [Collection(Collections.Webapp)]
    public class ApplicationTests : AccountTestBase
    {
        private int OrgId { get; set; }
        private int SepacialContentId { get; set; }

        [Fact, FeatureTest]
        public void Application_Report_Should_Have_Answers()
        {
            driver.Manage().Window.Maximize();
            var requestManager = FakeRequestManager.Create();

            var Orgconfig = new Organization()
            {
                OrganizationName = "MockName",
                RegistrationTitle = "MockTitle",
                Location = "MockLocation",
                RegistrationTypeId = 1
            };

            var FakeOrg = FakeOrganizationUtils.MakeFakeOrganization(requestManager, Orgconfig);
            OrgId = FakeOrg.org.OrganizationId;

            var NewSpecialContent = SpecialContentUtils.CreateSpecialContent(0, "MembershipApp2017", null);
            SepacialContentId = NewSpecialContent.Id;
            SpecialContentUtils.UpdateSpecialContent(NewSpecialContent.Id, "MembershipApp2017", "MembershipApp2017", GetValidHtmlContent(), false, null, "", null);

            username = RandomString();
            password = RandomString();
            string roleName = "role_" + RandomString();
            var user = CreateUser(username, password, roles: new string[] { "Access", "Edit", "Admin", "Membership" });
            Login();

            /*
                Using WaitForElement() doesn't work in this test
                WaitForElement() only makes the duration of loading spinner longer
                and causes a "reference not set to an instance of an object" error in the Find() functions below
            */

            Open($"{rootUrl}Org/{OrgId}#tab-Registrations-tab");
            WaitForElementToDisappear(loadingUI, 30);

            ScrollTo(css: "#Questions-tab > .ajax");
            Find(css: "#Questions-tab > .ajax").Click();
            WaitForElementToDisappear(loadingUI);

            Find(css: ".col-sm-12 .edit").Click();
            WaitForElementToDisappear(loadingUI);

            Find(css: ".pull-right > .btn-success:nth-child(2)").Click();
            Wait(1);

            Find(css: ".AskText > a").Click();
            WaitForElement("#QuestionList > div.type-AskText");

            Find(css: ".modal-footer > .btn-primary:nth-child(1)").Click();
            var swal = ".sweet-alert.visible";
            WaitForElement(swal);

            Find(xpath: "//button[contains(.,'Yes, Add Questions')]").Click();
            WaitForElementToDisappear(".sweet-overlay");

            Find(css: "#QuestionList > div.type-AskText a.btn.btn-success").Click();
            WaitForElementToDisappear(loadingUI);

            var input = "div.ask-texts > div.well.movable > div.form-group > div.controls > input.form-control:nth-child(1)";
            WaitForElement(input);

            var InputAskItem = Find(css: input);
            InputAskItem.Clear();
            InputAskItem.SendKeys("Vow 1 reads: \"Do you acknowledge yourself to be a sinner in the sight of God, justly deserving his displeasure and without hope except through his sovereign mercy?\"");

            Find(css: ".ask-texts > .well").Click();
            WaitForElementToDisappear(loadingUI);

            ScrollTo(css: "#Questions-tab > .ajax");
            Find(xpath: "(//a[contains(text(),'Save')])[3]").Click();
            WaitForElementToDisappear(loadingUI);

            Open($"{rootUrl}OnlineReg/{OrgId}");

            var InputField = Find(id: "List0.Text0_0");
            InputField.Clear();
            InputField.SendKeys("ThisTextMustAppearInTests");
            Find(id: "otheredit").Click();

            WaitForElement("#submitit", 5);
            Find(id: "submitit").Click();
            Wait(2);

            Open($"{rootUrl}Reports/Application/{OrgId}/{user.PeopleId}/MembershipApp2017");
            WaitForElement("h2", 5);

            PageSource.ShouldContain("ThisTextMustAppearInTests");
        }

        private string GetValidHtmlContent()
        {
            string res = ReportsViewTestsResources.ValidHtmlContent;
            return res;
        }

        public override void Dispose()
        {
            SpecialContentUtils.DeleteSpecialContent(SepacialContentId);
            FakeOrganizationUtils.DeleteOrg(OrgId);
            base.Dispose();
        }
    }
}
