---
layout: post
title: Cohesion - Part 1 What is Cohesion
author: Richard Nagle
---

The most [basic description](https://en.wikipedia.org/wiki/Object#Computing) of an object in 
computer science is 

> "a language mechanism for binding data with methods that operate on that data"

From this we can see that the most fundamental OO design heuristic we can apply is to ensure 
that an object contains methods which only operate on the data contained in that object; and 
that the data contained in the object is only operated upon by the methods in that object.

Cohesion is a measure used to determine how closely related an object's methods are to the data
contained within that object. An object is said to exhibit high cohesion when each of its 
methods uses all the data in that object; an object has low cohesion when its methods uses none 
of the data. We should aim towards objects exhibiting high cohesion.

In my mind, cohesion is what really makes OO different to procedural. I'd go as far to say that
unless you are continually striving for high cohesion you're not really doing OO at all. You may be
using an OO language but unless your objects display behavior on contained data then your 
still doing procedural.

### How can we Measure Cohesion

There are various methods for calculating the cohesiveness of a class (up to now I've written 
about objects, but in class-based OO languages like Java and C# cohesion is measured on 
a class rather than an object). The basis of these methods is to calculate the cohesion between
the class's fields and the class's methods; the most cohesive a class can is when all the methods
use the all fields, the least cohesive is when the all the method's use none of the fields.

The most commonly used calculation method is the Lack of Cohesion of Methods (LCOM) which has
the following formula

        1 - (sum(MF)/M*F)

where

        M is the number of methods in the class
        F is the number of fields in the class
        MF is number of methods which access a given field of the class
        
The result will be a value in the range 0 - 1, where 0 has high cohesive and 1 has no cohesion 
at all.

### Example LCOM Calculation

If, like me, you're not a maths gonk this can seems slightly daunting so I'll walk you through an 
example and all should become clear.

{% highlight c# %}
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
{% endhighlight %}

So firstly we'll find the value for M, which the number of methods in the class. In this case M=5 
because in addition to standard instance methods we need to include static methods, constructors 
and property getters/setters (a property with get and set, if we had any, counts as 2). So we have 
5: constructor, FormattedTotal getter, GetDiscount, FormatTaxAmount and FormatValue.

    M = 5

Next, we'll calculate F. F is the number of fields, so F=3: _customerName, _customerStatus, _total.

    F = 3

Then we need to calculate the MF values. To do this we look at each field in turn and count the 
number methods that access that field, this gives us an MF value for each field thus:

    _customerName: MF = 2 (constructor, FormattedTotal)

    _customerStatus: MF = 2 (constructor, GetDiscount)

    _total: MF = 3 (constructor, FormattedTotal, GetDiscount)

Now we can plug these values into the LCOM formula

        1 - (sum(MF)/M*F)

        1 - ((2 + 2 + 3) / 5 * 3)

        1 - (7 / 15)

        1 - 0.47

        0.53

So our DiscountCalculator class has a score of 0.53, or maybe put another way 53% of this
class is uncohesive (remember we want to get as close to zero as possible). That's not 
terrible but I'm sure we can do better. In the next part I'll show how to improve the cohesion
in this class; and how a focus on cohesion can improve the quality of your code.
