namespace Cohesion2
{
  public class CustomerStatus
  {
    public static CustomerStatus Standard { get; }
    public static CustomerStatus CardHolder { get; }
    public static CustomerStatus Gold { get; }

    static CustomerStatus()
    {
      Standard = new CustomerStatus(0.05m);
      CardHolder = new CustomerStatus(0.1m);
      Gold = new CustomerStatus(0.25m);
    }

    private readonly decimal _discount;

    private CustomerStatus(decimal discount)
    {
      _discount = discount;
    }

    public Money GetDiscount(Money total)
    {
        return total.Percentage(_discount);
    }
  }

  // m = 6
  // f =1

  // 1 -((1+1)/6*1)
  // 1 -(2/6) = 0.66
}