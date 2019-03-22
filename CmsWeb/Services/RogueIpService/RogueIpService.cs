using CmsWeb.Lifecycle;
using Dapper;
using System.Configuration;
using System.Text;
using UtilityExtensions;

namespace CmsWeb.Services.RogueIpService
{
    public interface IRogueIpService
    {
        bool LogRogueUser(string why, string from);
    }

    public class RogueIpService : IRogueIpService
    {
        private readonly IRequestManager _requestManager;

        public RogueIpService(IRequestManager requestManager)
        {
            _requestManager = requestManager;
        }

        public bool LogRogueUser(string why, string from)
        {
            var insertRogueIp = ConfigurationManager.AppSettings["InsertRogueIp"];
            if (insertRogueIp.HasValue())
            {
                _requestManager.CurrentDatabase.Connection.Execute(insertRogueIp, new { ip = _requestManager.CurrentHttpContext.Request.UserHostAddress, db = Util.Host });
            }

            var form = Encoding.Default.GetString(_requestManager.CurrentHttpContext.Request.BinaryRead(_requestManager.CurrentHttpContext.Request.TotalBytes));
            var sendto = Util.PickFirst(ConfigurationManager.AppSettings["CardTesterEmail"], Util.AdminMail);
            _requestManager.CurrentDatabase.SendEmail(Util.FirstAddress(sendto), $"CardTester on {Util.Host}", $"why={why} from={from} ip={_requestManager.CurrentHttpContext.Request.UserHostAddress}<br>{form.HtmlEncode()}", Util.EmailAddressListFromString(sendto));

            return true;
        }
    }
}
