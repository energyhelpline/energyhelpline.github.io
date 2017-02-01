Duplication and Abstractions
===

A lot of developers know that they shouldn't duplicate code.  It makes logical sense; you don't want to have to maintain the same logic in multiple places because if it needs to change you have to hunt through the code base trying to find it, you'll likely have to write the same test multiple times, and you may find the duplicated logic starts to deviate over time leading to different behaviours which may be bugs.

However I believe that developers (I suppose I'm really talking about past me here!) can focus on removing duplication by any means necessary, which, whilst solving the problem of the code being in more than one place, doesn't always produce a pleasing design full on reusable abstractions.

*TODO: I need to read to rest of Don't Repeat Yourself and Orthogonality sections of The Pragmatic Programmer again to make sure I'm right about this, but...*

In fact, Andy Hunt and Dave Thomas who introduced the concept in [The Pragmatic Programmer][], which is an excellent book and I encourage those who haven't read it to do so, spend a long time explaining different types of duplication and the ways they can arise, but they don't offer any advice on how you should get rid of the duplication.

What I want to draw attention to is the link between abstractions and duplication that Steve Smith talks about.  Steve Smith, in his entry to the book [97 Things Every Programmer Should Know][], says:

> The developer who learns to recognize duplication, and understands how to eliminate it through appropriate practice and proper abstraction, can produce much cleaner code than one who continuously infects the application with unnecessary repetition.

Removing abstraction through appropriate practice and proper abstraction.  It's this link between duplication and abstraction which I think holds the key to improving our designs.  One way our designs can improve is by replacing duplication with appropriate abstractions.

The hardest thing about this advice is there is no process you can follow to create the perfect abstraction, it really is were the art and creativity come in to computer programming and design.  The best guidance we have available are hueristics that describe what a good abstraction might look like, and others that indicate where we might be missing an abstraction.  Repeated code is an example of the latter.  It is a signal that we might be missing an abstraction.  If we find an abstraction that enables us to remove the duplication, we can replace the duplication with that abstraction.  However, if we naively remove the duplication, we may couple concepts together and reduce the reusability of our code.

Removing duplication the right way
---

Joe B Rainsberger, otherwise known as JBrains, has an interesting video about finding abstractions in the episdoe [A Long Look Down the Road (paywall)][], part of his [The World's Best Intro to TDD][].  In the video he thinks out loud so you can really understand the thought process he goes through which I have made an effort to document below.

The class that is the focus of the video, `ConsoleDisplay`, is so small that you might think that there is nothing to refactor in it, or that it doesn't need refactoring, or at least is not warrented given his progress with the project in the series.  You may be right about the latter, as Joe himself conceeds, but it is an interesting exercise none-the-less.

`ConsoleDisplay` is a simple class that formats a number of different messages that a till/checkout might require.  The state we start with is as such:

```java
public class ConsoleDisplay {
    public static String formatPrice(Price price) {
        return String.format("$%,.2f", price.dollarValue());
    }

    public void displayProductNotFoundMessage(String barcodeNotFound) {
        System.out.println(
            String.format("Product not found for %s", barcodeNotFound));
    }

    public void displayEmptyBarcodeMessage() {
        System.out.println("Scanning error: empty barcode");
    }

    public void displayPrice(Price price) {
        System.out.println(formatPrice(price));
    }
}
```

Joe starts to look at similarities and differenes between the methods.  There are three calls to `System.out.println` but they are all passed differing arguments.  The call in `displayEmptyBarcodeMessage` passes a string literal, the call in `displayProductNotFoundMessage` passes the output of `String.format`, whilst the call in `displayPrice` passes the output of `formatPrice` which itself returns the output of `String.format`.  He notices that if he inlines the `formatPrice` method (checking first that it isn't used any where else as it's public) he can make the three 'display' methods more similar.

```java
public class ConsoleDisplay {
    public void displayProductNotFoundMessage(String barcodeNotFound) {
        System.out.println(
            String.format("Product not found for %s", barcodeNotFound));
    }

    public void displayEmptyBarcodeMessage() {
        System.out.println("Scanning error: empty barcode");
    }

    public void displayPrice(Price price) {
        System.out.println(
            String.format("$%,.2f", price.dollarValue()));
    }
}
```

Now it is easier to see that two of the methods send the output of `String.format` to `System.out.println` and one sends a string to `System.out.println`.  If fact, Joe notices it is possible to make all three look more similar by wrapping the string passed to `System.out.println` in `displayEmptyBarcodeMessage` in `String.Format` as passing a string with no arguments returns the original string.

```java
public class ConsoleDisplay {
    public void displayProductNotFoundMessage(String barcodeNotFound) {
        System.out.println(
            String.format("Product not found for %s", barcodeNotFound));
    }

    public void displayEmptyBarcodeMessage() {
        System.out.println(
            String.Format("Scanning error: empty barcode"));
    }

    public void displayPrice(Price price) {
        System.out.println(
            String.format("$%,.2f", price.dollarValue()));
    }
}
```

Now all three methods look much more similar.  Each call `System.out.println` passing the output of `String.format`.

Highlighting the differences and similarities in your code can help you feel your way towards an abstraction.  An abstraction is the synthesis of a set of behaviours to their common core so being able to make the structure of these methods similar means we might be on the way to exposing an abstraction.  Duplication of structure is another, perhaps slightly stronger, signal that you've got an abstraction trying to get out.

Mechanically, you could say that creating abstractions is the process of separating that which is similar from that which is different.

Joe then notices that he can make the differences stand out more if he changes the formatting of the code within the methods, by placing all that is similar within the methods on one line, and all that is different on the next.

```java
public class ConsoleDisplay {
    public void displayProductNotFoundMessage(String barcodeNotFound) {
        System.out.println(String.format(
            "Product not found for %s", barcodeNotFound));
    }

    public void displayEmptyBarcodeMessage() {
        System.out.println(String.Format(
            "Scanning error: empty barcode"));
    }

    public void displayPrice(Price price) {
        System.out.println(String.format(
            "$%,.2f", price.dollarValue()));
    }
}
```

Now the similarity `System.out.printlin(String.format` is all on one line in each method and the difference, the format of the string and the args, are all on the following line.

It is interesting to hear Joe talk through what he's thinking at this point in the refactoring.  In a voice over added as he himself watched his own video, he notices how ideas various names come and go, but some stick around as the concepts coalesce in this mind.  He has begun to use words such as format, render, and template.  The names he talks about aren't apparent in the code yet but they give a clue to the abstractions that may be hiding there.

I'm going to deviate a little from Joe's video here, but we'll end up in the same place.  

I feel there are few bits of duplication left in the class.  Perhaps the most obvious to me is the duplication of `System.out.println` so let's extract a method.

```java
public class ConsoleDisplay {
    public void displayProductNotFoundMessage(String barcodeNotFound) {
        render(String.format(
            "Product not found for %s", barcodeNotFound));
    }

    public void displayEmptyBarcodeMessage() {
        render(String.Format(
            "Scanning error: empty barcode"));
    }

    public void displayPrice(Price price) {
        render(String.format(
            "$%,.2f", price.dollarValue()));
    }

    private void render(string text) {
        System.out.println(text);
    }
}
```

The next is `String.format` so let's extract another method for that.

```java
public class ConsoleDisplay {
    public void displayProductNotFoundMessage(String barcodeNotFound) {
        render(format("Product not found for %s", barcodeNotFound));
    }

    public void displayEmptyBarcodeMessage() {
        render(format("Scanning error: empty barcode"));
    }

    public void displayPrice(Price price) {
        render(format("$%,.2f", price.dollarValue()));
    }

    private void render(string text) {
        System.out.println(text);
    }

    private void format(string template, Object... args) {
        return String.format(template, args);
    }
}
```

The `render` and `format` methods we've extracted are actually quite generic and you could imagine that they may be useful elsewhere in the system.  If they were found to be useful they could be moved to separate classes the interfaces of which could get injected in to the `ConsoleDisplay` as dependencies, like so:

```java
public class ConsoleDisplay {
    private final Formatter formatter;
    private final Renderer renderer;

    public ConsoleDisplay(Formatter formatter, Renderer renderer) {
        this.formatter = formatter;
        this.renderer = renderer;
    }

    public void displayProductNotFoundMessage(String barcodeNotFound) {
        renderer.render(formatter.format("Product not found for %s", barcodeNotFound));
    }

    public void displayEmptyBarcodeMessage() {
        renderer.render(formatter.format("Scanning error: empty barcode"));
    }

    public void displayPrice(Price price) {
        renderer.render(formatter.format("$%,.2f", price.dollarValue()));
    }
}

public class ConsoleRenderer : Renderer {
    private void render(string text) {
        System.out.println(text);
    }
}

public class StringFormatter : Formatter {
    private void format(string template, Object... args) {
        return String.format(template, args);
    }
}
```

At this point we've created two new abstractions, the `Renderer` and the `Formatter` and they, along with the `ConsoleDisplay` can be tested independently.  In fact, having to test the formatting and the rendering together was a signal at the beginning that we missed, we can see in hindsight.

This design lets us easily add a new method for templating if required and would allow us to swap in a new method of rendering, such as rendering to an LCD screen (this is for a till/checkout remember!), if required.

This is, I believe, what people mean when they talk about removing duplication.  The duplication has been removed by adding two new abstractions.

At this point we can see that the name of the class `ConsoleDisplay` no longer makes sense as it no longer has a reference to `System.out.println`.  What it does now is tie together a renderer and a formatter.  Perhaps it's name should be more domain oriented and it could be called a `TillDisplay` or similar.

Removing duplication the wrong way
---

The wrong way to think about duplication, I believe, is to remove the duplication naively without necessarily thinking about the abstractions that you are creating, and without being sensitive to whether what you extract belongs together.

Let's take the code all the way back to after Joe inlined the `formatPrice` method, and wrapped the scanning error message in `String.format`.  The third code block from the 'Removing duplication the right way' section above.

```java
public class ConsoleDisplay {
    public void displayProductNotFoundMessage(String barcodeNotFound) {
        System.out.println(
            String.format("Product not found for %s", barcodeNotFound));
    }

    public void displayEmptyBarcodeMessage() {
        System.out.println(
            String.Format("Scanning error: empty barcode"));
    }

    public void displayPrice(Price price) {
        System.out.println(
            String.format("$%,.2f", price.dollarValue()));
    }
}
```

One thing we could have done is to extract the `System.out.println` and the `String.format` together.  They are used together in each case we have seen so far, so perhaps they belong together?  If we did so we might end up with something like the following:

```java
public class ConsoleDisplay {
    public void displayProductNotFoundMessage(String barcodeNotFound) {
        formatAndPrint("Product not found for %s", barcodeNotFound));
    }

    public void displayEmptyBarcodeMessage() {
        formatAndPrint("Scanning error: empty barcode");
    }

    public void displayPrice(Price price) {
        formatAndPrint("$%,.2f", price.dollarValue());
    }

    public void formatAndPrint(String template, Object... args) {
        System.out.println(String.format(template, args));
    }
}
```

`formatAndPrint` could, again, be moved to an injectable class of its own, which ConsoleDisplay could reference by interface.

```java
public class ConsoleDisplay {
    private final RendererFormatter rendererFormatter;

    public ConsoleDisplay(RendererFormatter rendererFormatter) {
        this.rendererFormatter = rendererFormatter;
    }

    public void displayProductNotFoundMessage(String barcodeNotFound) {
        rendererFormatter.formatAndPrint("Product not found for %s", barcodeNotFound));
    }

    public void displayEmptyBarcodeMessage() {
        rendererFormatter.formatAndPrint("Scanning error: empty barcode");
    }

    public void displayPrice(Price price) {
        rendererFormatter.formatAndPrint("$%,.2f", price.dollarValue());
    }
}

public class ConsoleRendererStringFormatter : RendererFormatter {
    public void formatAndPrint(String template, Object... args) {
        System.out.println(String.format(template, args));
    }
}
```

This refactoring removes the same amount of duplication as the first.  You might even think this to be better, as there are fewer classes created.  Unfortunately it hasn't really solved any of our other problems.  The rendering and the formatting cannot be tested independently and it is harder to swap either one out because one is tied to the other.  In other words, we have coupled rendering together with formatting.  The name of the new interface `RendererFormatter` and the new class that implements the interface `ConsoleRendererStringFormatter` are both compound names which describe doing more than one thing, signalling perhaps that this abstraction is in violation of [The Single Responsibility Principle][].

Conclusion
---

This may not be the greatest example.  Perhaps the second refactoring is better than the first given what we know about the system right now, i.e., not very much.  We don't yet know that rendering and formatting will ever need to be changed independently because we've never received that requirement.  Perhaps the second refactoring would be a good first step, until such time as we do need to format and render independently when we could refactor further.  Whether or not this was a good or a bad descision will only become apparent in due course and will depend on the changes to the system that are required.

However, I still think the examples give a good insight in to the sensitivity to conceptually different things within the code that is needed by a developer to enable abstractions to emerge.

This kind of sensitivity to the concepts in the code, and the level of abstraction they are is also critical when extracting methods in the same way as it is when extracting classes.  Methods are also an abstraction.  Even if the extracted methods don't get moved to classes of their own, it is still important that the methods themselves are coherant.

In the real world I have noticed the kind of coupling introduced in the name of eliviating duplication in my own code when I've been moving shared behaviour up to a base class.  More than once I've noticed I've moved code shared between two controllers to a base controller class but the code I moved shared very little in common conceptually and yet I've coupled them together inside my new abstraction, the base controller class.  A better way of removing the duplication might have been to create seperate classes injected in to the controllers as dependencies.

To do
---

* It looks as though Code Complete might have something to say on the matter.  I've never read it, and we've got a copy!
* Perhaps I need to research what an abstraction is.

[A Long Look Down the Road (paywall)]: http://online-training.jbrains.ca/courses/wbitdd-01/lectures/140743
[The World's Best Intro to TDD]: http://www.jbrains.ca/training/the-worlds-best-introduction-to-test-driven-development/
[97 Things Every Programmer Should Know]: http://programmer.97things.oreilly.com/wiki/index.php/Contributions_Appearing_in_the_Book