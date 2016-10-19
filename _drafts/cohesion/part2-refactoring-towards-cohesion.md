---
layout: post
title: Cohesion - Part 2 Refactoring Towards Cohesion
author: Richard Nagle
date: 2016-10-19
tags: cohesion,oo,refactoring
---

In the second part of this series about code cohesion we'll look at how we can refactor an existing class
to be more cohesive and look at the benefits of doing so.

[Previously](/cohesion-part-1) we were examining a class, `DiscountCalculator`, and we determined it to have
a Lack of Cohesion of Methods (LCOM) score of 0.53. We want this score to be as close to zero as possible
so need to refactor this class to improve its cohesiveness. High cohesion is achieved by keeping methods and
the fields they operate upon together in the same class; therefore to improve cohesiveness we have two main
refactoring strategies - extracting methods and extracting fields.

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

It's fairly obvious that the `FormatTaxAmount()` and `FormatValue()` methods are the least cohesive as they use no
fields at all. In fact, there's a pretty big warning sign right there in the code; both these methods are marked
as `static` meaning they belong to the class and not to instances of the class, therefore they are unlikely to 
access instance fields. Additionally, look at `FormatTaxAmount()`, this method really has nothing to do with calculating
discounts and is not called by any of the other methods; it's probably only here so that it can use the formatting
provided by the `FormatValue()` method.

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
 
Yep, the `MoneyFormatter` class is completely uncohesive with the worst possible LCOM score of 1.
Even worse if we calculate the average cohesion of these two classes together we get a score of 0.61 which
is even worse than our original score of 0.53.

Let us see if we can improve `MoneyFormatter`. If we look at the two methods they both operate on a single
decimal so maybe we change this to an instance field and improve the cohesion. I am also going to change the
name of the class to `Money` to describe its new responsibility.

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

The `DiscountCalculator` now looks like this:

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
The second technique we'll look at to improve cohesion is to extract fields into another class. When we do this we want to move two
or more fields and replace them with a single field. Look at your class and try to identify related fields that should be grouped together -
such groups of fields are often referred to as [Data Clumps](http://martinfowler.com/bliki/DataClump.html).

If we examine the fields in `DiscountCalculator` we have `_total`, `_customerStatus` and `customerName`. It seems fairly obvious that
`_customerStatus` and `_customerName` belong together, probably in some sort of `Customer` class. Let's do that: 

{% highlight c# %}
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
{% endhighlight %}

And integrate it into `DiscountCalculator`

{% highlight c# %}
public class DiscountCalculator
{
  private Customer _customer;
  private Money _total;

  public DiscountCalculator (Money total, Customer customer)
  {
    _customer = customer;
    _total = total;
  }

  public string FormattedTotal
  {
    get
    {
      var discount = GetDiscount();
      var discountedTotal = _total.Subtract(discount);

      return $"Total for {_customer.Name} is {discountedTotal}" +
             $"with discount {discount}";
    }
  }

  private Money GetDiscount()
  {
    switch (_customer.Status)
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

And yay, we've finally managed to get an LCOM score of zero for `DiscountCalculator` - we have two fields and they are both
used in every method. The LCOM for `Customer` is 0.34 and, disappointingly, the average LCOM remains at 0.11. To fix this we 
are going to have to move some of the behaviour into the `Customer` class. But to start with we'll look at `CustomerStatus`
which is currently defined as an `enum`:

{% highlight c# %}
public enum CustomerStatus
{
  Standard, CardHolder, Gold
}
{% endhighlight %}

The first move is convert this into a real class and move the responsibility for calculating the discount into it:

{% highlight c# %}
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
{% endhighlight %}

And update the `GetDiscount()` method in `DiscountCalculator`:

{% highlight c# %}
private Money GetDiscount()
{
  return _customer.Status.GetDiscount(_total);
}
{% endhighlight %}

I don't like the [Demeter](https://en.wikipedia.org/wiki/Law_of_Demeter) violation in that method so I add a passthru method
on `Customer`:

{% highlight c# %}
public Money GetDiscount(Money total)
{
  return _status.GetDiscount(total);
}
{% endhighlight %}

And change `GetDiscount()` in `DiscountCalculator` to use it:

{% highlight c# %}
private Money GetDiscount()
{
  return _customer.GetDiscount(_total);
}
{% endhighlight %}

These changes so far have not improved our LCOM score, they were just necessary to get the code is a better state for my final refactoring.
Looking at the `FormattedTotal` property in `DiscountCalculator`  I can see that this behaviour actually belongs to the `Customer` class

{% highlight c# %}    
public string FormattedTotal
{
  get
  {
    var discount = GetDiscount();
    var discountedTotal = _total.Subtract(discount);

    return $"Total for {_customer.Name} is {discountedTotal}" +
           $"with discount {discount}";
  }
}
{% endhighlight %}

So I move it to the `Customer` class:

{% highlight c# %}
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
{% endhighlight %}

This now gives our `Customer` class an LCOM score of zero, so we are done.

### Conclusion ### 

Let's take a final look at our `DiscountCalculator` class:

{% highlight c# %}
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
{% endhighlight %}

We've pretty much ended up with a class that does nothing and I'd probably look to remove it and get clients to interact
directly with the `Customer` class instead.

So, what have we achieved? By concentrating on improving the cohesion in our original `DiscountCalculator` class we have 
spawned additional classes for `Money`, `Customer` and `CustomerStatus` to replace the original confused class. In doing so 
we have the improved the design by putting the correct responsibilities into highly-focused classes. In other words, we have
fulfilled the Single Responsibility Principle.

One of the most common criticism of the Single Responsibility Principle is that it is not clear how big the single responsibility
should be. As an extreme example, I could write my entire application in a single class and it would still only have a single
responsibility, that being "running my application". But by concentrating on the cohesion metrics we can correctly size classes
to manage just their data and apply the Single Responsibility Principle as it was meant to be.

Another design improvement we've made is to remove the reliance on simple-types; we achieved this by replacing them with classes. So if
you look at the original `DiscountCalculator` it used `decimal` for the values, `string` for the customer and an `enum` for the customer
status. In the completed version these have been replaced with `Money`, `Customer` and `CustomerStatus` classes.

In the final part of this series I will look how we should apply cohesion at the architectural level.