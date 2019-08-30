﻿using CmsData;
using CmsData.Codes;
using CmsDataTests.Support;
using SharedTestFixtures;
using Shouldly;
using System;
using System.Linq;
using UtilityExtensions;
using Xunit;

namespace CmsDataTests
{
    [Collection(Collections.Database)]
    public class CMSDataContextFunctionTests : FinanceTestBase
    {
        //TODO: clean up contributions in the database
        [Fact]
        public void GetTotalContributionsDonorTest()
        {
            var fromDate = new DateTime(2019, 1, 1);
            var toDate = new DateTime(2019, 7, 31);
            using (var db = CMSDataContext.Create(Util.Host))
            {
                var TotalAmmountContributions = db.Contributions.Where(x => x.ContributionTypeId == 1).Sum(x => x.ContributionAmount) ?? 0;
                var TotalPledgeAmountContributions = db.Contributions.Where(x => x.ContributionTypeId == 8).Sum(x => x.ContributionAmount) ?? 0;

                var bundleHeader = CreateBundle(db);
                var FirstContribution = CreateContribution(db, bundleHeader, fromDate, 120, peopleId: 1);
                var SecondContribution = CreateContribution(db, bundleHeader, fromDate, 500, peopleId: 1, contributionType: ContributionTypeCode.Pledge);

                var results = db.GetTotalContributionsDonor(fromDate, toDate, null, null, true, null, null, true).ToList();
                var actual = results.First();

                actual.Amount.ShouldBe(TotalAmmountContributions + 120);
                actual.PledgeAmount.ShouldBe(TotalPledgeAmountContributions + 500);

                db.ExecuteCommand("DELETE FROM [BundleDetail] WHERE [BundleHeaderId] = {0} AND [ContributionId] = {1}", bundleHeader.BundleHeaderId, FirstContribution.ContributionId);
                db.ExecuteCommand("DELETE FROM [BundleDetail] WHERE [BundleHeaderId] = {0} AND [ContributionId] = {1}", bundleHeader.BundleHeaderId, SecondContribution.ContributionId);

                db.ExecuteCommand("DELETE FROM [Contribution] WHERE [ContributionId] = {0}", FirstContribution.ContributionId);
                db.ExecuteCommand("DELETE FROM [Contribution] WHERE [ContributionId] = {0}", SecondContribution.ContributionId);
            }
        }
    }
}
