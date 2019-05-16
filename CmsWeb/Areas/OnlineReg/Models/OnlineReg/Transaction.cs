using CmsData;
using System;
using System.Linq;

namespace CmsWeb.Areas.OnlineReg.Models
{
    public partial class OnlineRegModel
    {
        public static string GetTransactionGateway(PaymentProcessTypes processType = PaymentProcessTypes.OnlineRegistration)
        {
            return (from e in DbUtil.Db.PaymentProcess
                    join d in DbUtil.Db.GatewayAccount on e.GatewayAccountId equals
                    d.GatewayAccountId into gj
                    from sub in gj.DefaultIfEmpty()
                    where e.ProcessId == (int)processType
                    select new
                    {
                        sub.GatewayAccountName
                    }).FirstOrDefault().GatewayAccountName;
        }

        private decimal? payAmt;
        public decimal PayAmount()
        {
            if (payAmt.HasValue)
            {
                return payAmt.Value;
            }

            decimal max = 0;
            decimal amt = List.Sum(p => p.AmountToPay());
            if (List.Count > 0)
            {
                max = List.Max(p => p.org != null ? p.setting.MaximumFee ?? 0 : 0);
            }

            if (max == 0)
            {
                return CachePayAmount(amt);
            }

            var totalother = List.Sum(p => p.TotalOther());
            if (List.Any(p => p.setting.ApplyMaxToOtherFees) && amt > max)
            {
                amt = max;
            }
            else if ((amt - totalother) > max)
            {
                amt = max + totalother;
            }

            return CachePayAmount(amt);
        }

        private decimal CachePayAmount(decimal amt)
        {
            payAmt = amt;
            if (org != null) // Check Family Deposit
            {
                var famdeposit = org.GetExtraValue("FamilyDeposit");
                if (famdeposit != null && famdeposit.IntValue.HasValue)
                {
                    var total = TotalAmount();
                    var famdep = Convert.ToDecimal(famdeposit.IntValue);
                    payAmt = Math.Min(total, famdep);
                }
            }
            return payAmt.Value;
        }


        private decimal? totAmt;
        public decimal TotalAmount()
        {
            if (totAmt.HasValue)
            {
                return totAmt.Value;
            }

            var amt = List.Sum(p => p.TotalAmount());
            var max = List.Max(p => p.org != null ? p.setting.MaximumFee ?? 0 : 0);
            if (max == 0)
            {
                return CacheTotalAmount(amt);
            }

            var totalother = List.Sum(p => p.TotalOther());
            if (List.Any(p => p.setting.ApplyMaxToOtherFees) && amt > max)
            {
                amt = max;
            }
            else if ((amt - totalother) > max)
            {
                amt = max + totalother;
            }

            return CacheTotalAmount(amt);
        }
        private decimal CacheTotalAmount(decimal amt)
        {
            totAmt = amt;
            return amt;
        }
    }
}
