namespace Cohesion2
{
public class DiscountCalculator
{
  private string _customerName;
  private CustomerStatus _customerStatus;
  private Money _total;

  public DiscountCalculator
    (string customerName,
     CustomerStatus customerStatus,
     Money total)
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
      var discountedTotal = _total.Subtract(discount);

      return $"Total for {_customerName} is {discountedTotal}" +
              $"with discount {discount}";
    }
  }

  private Money GetDiscount()
  {
    switch (_customerStatus)
    {
      case CustomerStatus.Standard:
        return _total.Percentage(0.05m);
      case CustomerStatus.CardHolder:
        return _total.Percentage(0.1m);
      case CustomerStatus.Gold:
        return _total.Percentage(0.25m);
      default:
        return _total.Percentage(0m);
    }
  }
}

  // m = 5
  // f =3

  // 1 -((2+2+3)/5*3)
  // 1 -(7/15) = 0.533

  //         private string _customerName; //mf = 2
  // private CustomerStatus _customerStatus; //mf = 2
  // private Money _total; //mf = 3

}