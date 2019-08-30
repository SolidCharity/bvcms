﻿using CmsData;
using SharedTestFixtures;
using Shouldly;
using System.Collections.Generic;
using System.Linq;
using UtilityExtensions;
using Xunit;
using Shouldly;
using SharedTestFixtures;

namespace CMSWebTests.Areas.Setup.Controllers
{
    [Collection(Collections.Database)]
    public class DivisionControllerTests
    {
        [Theory]
        [InlineData("1")]
        [InlineData("yes")]
        public void Should_Change_No_Zero_field(string value)
        {
            var requestManager = FakeRequestManager.Create();
            var db = requestManager.CurrentDatabase;
            var controller = new CmsWeb.Areas.Setup.Controllers.DivisionController(requestManager);
            var routeDataValues = new Dictionary<string, string> { { "controller", "Division" } };
            controller.ControllerContext = ControllerTestUtils.FakeControllerContext(controller, routeDataValues);

            Program prog = new Program()
            {
                Name = "MockProgram",
                RptGroup = null,
                StartHoursOffset = null,
                EndHoursOffset = null
            };
            db.Programs.InsertOnSubmit(prog);
            db.SubmitChanges();

            Division div = new Division()
            {
                Name = "MockDivision",
                ProgId = prog.Id,
                SortOrder = null,
                EmailMessage = null,
                EmailSubject = null,
                Instructions = null,
                Terms = null,
                ReportLine = null,
                NoDisplayZero = false
            };
            db.Divisions.InsertOnSubmit(div);
            db.SubmitChanges();

            controller.Edit("z" + div.Id, value);

            bool? result = db.Divisions.Where(x => x.Id == div.Id).Select(y => y.NoDisplayZero).First();
            result.ShouldBe(true);

            db.ExecuteCommand("DELETE FROM [ProgDiv] WHERE [ProgId] = {0} AND [DivId] = {1}", prog.Id, div.Id);
            db.ExecuteCommand("DELETE FROM [Division] WHERE [Id] = {0}", div.Id);
            db.ExecuteCommand("DELETE FROM [Program] WHERE [Id] = {0}", prog.Id);
        }
    }
}
