using CmsWeb.Areas.OnlineReg.Models;
using CmsWeb.Lifecycle;
using CmsWeb.Services.RogueIpService;
using System.Web.Mvc;

namespace CmsWeb.Areas.Finance.Controllers
{
    [Authorize(Roles = "Admin,Finance")]
    [RouteArea("Finance", AreaPrefix = "Vault"), Route("{action}/{id?}")]
    public class VaultController : CmsController
    {
        private readonly IRogueIpService _rogueIpService;

        public VaultController(IRequestManager requestManager, IRogueIpService rogueIpService) : base(requestManager)
        {
            _rogueIpService = rogueIpService;
        }

        [HttpPost]
        public ActionResult DeleteVaultData(int id)
        {
            var manageGiving = new ManageGivingModel(CurrentDatabase, _rogueIpService);
            manageGiving.CancelManagedGiving(id);

            return Content("ok");
        }
    }
}
