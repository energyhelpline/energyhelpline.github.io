namespace Cohesion2
{
  public class Customer
  {
    private string _name;
    private CustomerStatus _status;

    public Customer(string name, CustomerStatus status)
    {
      _name = name;
      _status = status;
    }

    public string FormattedTotal(Money total)
    {
      var discount = _status.GetDiscount(total);
      var discountedTotal = total.Subtract(discount);

      return $"Total for {_name} is {discountedTotal}" +
              $"with discount {discount}";
    }
  }


  // m = 2
  // f = 2

  // 1 -((2+2)/2*2)
  // 1 -(4/4) = 0

}
