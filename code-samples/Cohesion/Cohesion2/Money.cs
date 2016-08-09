namespace Cohesion2
{
public class Money
{
  private decimal _value;

  public Money(decimal value)
  {
    _value = value;
  }

  public string ToTaxString()
  {
    return $"Total tax is {_value}";
  }

  public override string ToString()
  {
    return _value.ToString("C2");
  }

  public Money Subtract(Money other)
  {
    return new Money(_value - other._value);
  }

  public Money Percentage(decimal percent)
  {
    return new Money(_value * percent);
  }
}
}
