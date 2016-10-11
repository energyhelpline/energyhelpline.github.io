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

    public string Name
    {
      get { return _name; }
    }

    public CustomerStatus Status
    {
      get { return _status; }
    }
  }


  // m = 3
  // f = 2

  // 1 -((2+1+1)/3*2)
  // 1 -(4/6) = 0.33

}
