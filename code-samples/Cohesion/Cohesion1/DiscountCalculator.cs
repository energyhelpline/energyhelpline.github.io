namespace Cohesion1
{
    public class DiscountCalculator
    {
        private string _customerName;
        private CustomerStatus _customerStatus;
        private decimal _total;

        public DiscountCalculator(string customerName, CustomerStatus customerStatus, decimal total)
        {
            _customerName = customerName;
            _customerStatus = customerStatus;
            _total = total;
        }

        public string FormattedTotal
        {
            get
            {
                var discount = GetDiscount();
                var discountedTotal = _total - discount;

                return $"Total for {_customerName} is {FormatValue(discountedTotal)}" +
                       $"with discount {FormatValue(discount)}";
            }
        }

        private decimal GetDiscount()
        {
            decimal discountPercentage;

            switch(_customerStatus)
            {
                case CustomerStatus.Standard:
                    discountPercentage = 0.05m;
                    break;
                case CustomerStatus.CardHolder:
                    discountPercentage = 0.1m;
                    break;
                case CustomerStatus.Gold:
                    discountPercentage = 0.25m;
                    break;
                default:
                    discountPercentage = 0m;
                    break;
            }

            return _total * discountPercentage;
        }

        public static string FormatTaxAmount(decimal tax)
        {
            return $"Total tax is {FormatValue(tax)}";
        }

        private static string FormatValue(decimal value)
        {
            return value.ToString("C2");
        }
    }

    // m = 5
    // f =3

    // 1 -((2+2+3)/5*3)
    // 1 -(7/15) = 0.533

    //         private string _customerName; //mf = 2
    // private CustomerStatus _customerStatus; //mf = 2
    // private decimal _total; //mf = 3

}