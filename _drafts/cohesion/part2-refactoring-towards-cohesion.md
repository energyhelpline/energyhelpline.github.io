---
layout: post
title: Cohesion - Part 2 Refactoring Towards Cohesion
author: Richard Nagle
date: 2016-08-08
tags: cohesion,oo,refactoring
---

In the second part of this series about code cohesion we'll look at how we can refactor an existing class
to be more cohesive and look at the benefits of doing so.

[Previously](/part1-what-is-cohesion) we were examining a class `DiscountCalculator` and determined that it 
had Lack of Cohesion of Methods (LCOM) score of 0.53. We want this score to be as close to zero as possible
so we need to refactor this class to improve it's cohesiveness. As cohesion is basically a function of keeping 
methods and the field they operate on together, when refactoring we have two main strategies - extracting methods,
and extracting fields.

### Extracting Methods ### 

Let's quickly just review the `DiscountCalculator` class.

{% highlight c# %}
public class DiscountCalculator
{
  private string _customerName;
  private CustomerStatus _customerStatus;
  private decimal _total;

  public DiscountCalculator(
    string customerName,
    CustomerStatus customerStatus,
    decimal total)
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

      return
        $"Total for {_customerName} "+
        $"is {FormatValue(discountedTotal)}" +
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
{% endhighlight %} 

We can see that there are three fields and five methods. Doing a quick count we can quickly see which methods
are least cohesive.

|                   | **Fields**      | 
| **Method**        | **Referenced**  |
|:------------------|----------------:|
| constructor       | 3               |
| FormattedTotal    | 2               |
| GetDiscount()     | 2               |
| FormatTaxAmount() | 0               | 
| FormatValue()     | 0               |

It's fairly obvious that the `FormatTaxAmount()` and `FormatValue()` methods are our first target as they use no 
fields at all. In fact, there's a pretty big warning sign right there in the code; both these methods are marked
as `static` meaning they belong to the class and not to instances of the class, therefore they are unlikely to 
access instance fields. Also, look at `FormatTaxAmount()`, this method really has nothing to do with calculating 
discounts and is not called by any of the other methods; it's probably only really here so that it can use the 
formatting provided by the `FormatValue()` method. 

So, what to do?

Well, the most straightforward refactoring we can make is extract those methods into another class and reference
them from the `DiscountCalculator`; like so:

{% highlight c# %}
public static class MoneyFormatter
{ 
  public static string FormatTaxAmount(decimal tax)
  {
    return $"Total tax is {FormatValue(tax)}";
  }

  public static string FormatValue(decimal value)
  {
    return value.ToString("C2");
  }
}
{% endhighlight %}

{% highlight c# %}
public class DiscountCalculator
{
  private string _customerName;
  private CustomerStatus _customerStatus;
  private decimal _total;

  public DiscountCalculator
    (string customerName, 
    CustomerStatus customerStatus, 
    decimal total)
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

      return $"Total for {_customerName} is " +
             $"{MoneyFormatter.FormatValue(discountedTotal)}" +
              "with discount " +
             $"{MoneyFormatter.FormatValue(discount)}";
    }
  }

  private decimal GetDiscount()
  {
     decimal discountPercentage;

     switch (_customerStatus)
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

    return _total*discountPercentage;
}
{% endhighlight %}

And then we can re-calculate our LCOM. We removed two methods, so M is now 3; the other variables remain the
same:

> 1 - (sum(MF) / M * F)
>
> 1 - ((2 + 2 + 3) / 3 * 3)
>
> 1 - (7 / 9)
>
> 1 - 0.22
> 
> 0.22

Much better, we're much closer to zero. But hang on a second, not so fast, look at the `MoneyFormatter` class
we just created, it's entirely static so I'd guess not very cohesive. Let's do a quick LCOM on that class

> M = 2, F = 0, SUM(MF) = 0
>
> 1 - (0 / 2 * 0)
>
> 1
 
So as expected the `MoneyFormatter` class is completely uncohesive with the worst possible LCOM score of 1.
Even worse if we calculate the average cohesion of these two classes together we get a score of 0.61 which
is even worse than our original score of 0.53.

Let us see if we can improve `MoneyFormatter`. If we look at the two methods they both operate on a single
decimal so maybe we change this to an instance field and improve the cohesion.

{% highlight c# %}

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
}

{% endhighlight %}

However, when I try to integrate the `Money` class into `DiscountCalculator` I find that there are some issues around
calculating the discount. Therefore I need to add two new methods to `Money`:

{% highlight c# %}
  public Money Subtract(Money other)
  {
    return new Money(_value - other._value);
  }

  public Money Percentage(decimal percent)
  {
    return new Money(_value * percent);
  }
{% endhighlight %}

`DiscountCalculator` now looks like this:

{% highlight c# %}
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
{% endhighlight %}

Now our `Money` class is fully cohesive, scoring 0 for LCOM. Our average cohesion is 0.11; this is pretty good and may be good
enough in many scenarios - but in this case I think we can do better.

### Extracting fields ###


### Conclusion ### 
* talk about SRP
* something about coupling?
* is using LCOM important?  
* talk about data-clumps (decimals in MoneyFormatter) 