namespace Cohesion2
{
  public class DiscountCalculator
  {
    private Customer _customer;
    private Money _total;

    public DiscountCalculator(Money total, Customer customer)
    {
      _customer = customer;
      _total = total;
    }

    public string FormattedTotal
    {
      get { return _customer.FormattedTotal(_total); }
    }
  }

  // m = 2
  // f =2

  // 1 -((2+2)/2*2)
  // 1 -(4/4) = 0

}