---
layout: post
title: Jbrains' World's Best Intro to TDD
author: Douglas Waugh
excerpt: I recently completed Jbrains' World's Best Intro To TDD.  I decided to write this blog post to collect my thoughts.
---

![A family wearing Jordans'](/images/family-jordans.jpg "A family wearing Jordans")

[JBrains' World's Best Intro to TDD](http://online-training.jbrains.ca/p/wbitdd-01)

My interest in Test Driven Development (TDD) started five or six years ago and I have been using it as part of my normal development almost continuously since, so I am experienced writing well structured, easy to understand tests, as well as being comfortable using appropriate test doubles.

One thing I've found harder to understand and apply is the subtle link between TDD and object oriented (OO) design beyond the mechanical improvements that making classes testable provides.  I've found this difficult to learn from books because OO design, like many creative processes, tends to be a bit messy and books, because of the necessities of space, require more concise examples.  Videos of live coding with commentary can provide the extra messiness I need!

I'd been aware of Jbrains for some time, perhaps from the [Growing Object Oriented Software Guided by Tests (GOOS) mailing list](https://groups.google.com/forum/#!forum/growing-object-oriented-software) or perhaps from his own [blog posts](http://blog.thecodewhisperer.com/).  I enjoyed Nat Pryce and Steve Freeman's [GOOS](https://www.amazon.co.uk/Growing-Object-Oriented-Software-Guided-Signature/dp/0321503627) and Jbrains had once said it was the book he wished he had written.  When I heard Jbrains had released a [video course teaching TDD](http://online-training.jbrains.ca/p/wbitdd-01) it was something I wanted to try and see if there were any insights I could glean.

Things That Have Stuck With Me
---

I created this list from memory so it should represent the things that have actually stuck with me.  Not all of them are new to me but Jbrains definitely made me think harder, and sometimes differently, about them.

### Moving Details Up and Abstractions Down

Jbrains suggests that as you build a system your tests should become more specific and your application should become more generic.  I'm not sure how I feel about this right now.  I think it could be big, and I'm not sure I've ever thought about it before!  Interestingly today my colleague [Richard Nagle](https://twitter.com/richard_nagle) shared a link to a [post by Uncle Bob](http://blog.cleancoder.com/uncle-bob/2017/03/03/TDD-Harms-Architecture.html) who had noticed a similar phenomenon.

One situation Jbrains where uses the technique is to remove the duplication between the code and the tests when his tests for `SalesController` contain very similar product data as the `SalesController` itself; `SalesController` has a map of barcode to price and its tests have individual barcodes and prices.  Pushing the map up to the tests by passing it in to `SalesController` as a dependency makes the `SalesController` more generic and puts the data that the tests rely on in setup, passing as parameters and in assertions close to each other, making the link between the three much clearer.

Uncle Bob has a similar reason for making his code more generic and his tests more specific and that is to avoid the test logic from duplicating the tests.  The post linked to above has a good example of this.

### Sensitivity to Duplication

Most developers know that duplication in your code base is generally I bad idea and that duplication can be removed by finding suitable abstractions.  Perhaps the strongest signal towards a missing abstraction is the same few lines of code liberally copied and pasted throughout the code base.  However, there are different, more subtle forms of duplication, which Jbrains picks up on.

One is the duplication of the word 'display' in three different method names (`displayProductNotFoundMessage`, `displayEmptyBarcodeMessage`, `displayPrice`) and their containing class name `ConsoleDisplay` (Series 5, Episode 8: A Long Look Down the Road).  This might be a signal that some of the work the method does could be extracted to a shared method with the argument the method receives varying the behaviour.  For more on this subject check out my blog on [Duplication and Abstraction](/2017/02/01/duplication-and-abstraction/).

### Sensitivity to Levels of Abstraction

Together with being sensitive to duplication within the code, being sensitive to different levels of abstraction is important feedback that the code gives you that you can use to improve your design.  If a lower level abstraction has leaked in to a class it is likely to know too much about an implementation detail it doesn't need to know or care about.  Mixing different levels of abstraction within the same class is a bad idea because code at different levels of abstraction are likely to change at different rates and for different reasons.

The example that Jbrains has is within a class primarily concerned with the `Display` of `Messages`, he has a `PostOffice` which has a relevance at a lower, in this case `UDP`, level.  This is an indication that at the very least the naming is wrong, and perhaps there are other problems with the design.

### Lowest Coupling

I've always liked the idea of using the tell-don't-ask principle I first read about in [GOOS](https://www.amazon.co.uk/Growing-Object-Oriented-Software-Guided-Signature/dp/0321503627) when designing interactions between objects.  Jbrains makes it clear why this is such a good idea; a method on an interface not expected to return anything is the weakest form of contract a method can have as, so long as the parameters passed remain the same, the implementing class can change as many times and it likes without affecting those things dependent on the interface.

### When to Bend the Rules

Despite what I've said in Lowest Coupling, I've always struggled with how tell-don't-ask fits in to a normal web application for which a response is required for every request, mandating at least the controllers return something.

Jbrains discusses this problem when attempting to remove the reference to `Display`, which connects to hardware, from `SalesController`, a more pure object from within the Model-View-Controller pattern.  He doesn't like the idea of losing the purity the design had when `SalesController` could just tell `Display` to render a message, but by `SalesController` returning an object that represents the message to be sent back up the call stack he is able to remove the reference to `Display` from the controller.  As with many things in software engineering the decision is a trade-off between two contradictory ideas both with merits and short-comings.

In this case Jbrains decides that, on balance, it's better to remove the reference to the code which deals with hardware from the controller than to retain the tell-don't-ask design.  I can imagine that you might often have to do a bit of fudging when your beautiful snowflake code meets the horrible reality of I/O.

### Collection Test Cases

Jbrains reminded me of the minimum number of test cases required to test a collection: none, one, some, many, and error.  A small point but nice to be reminded.

### Using Object References for Object Equality

Usually when you assert that one object is the same as another you override the equality members to compare all the properties, in the case of a value object, or the ID, in the case of an entity.  However, Jbrains shows that you can delay implementing the equality overrides when you have a reference to the object you are asserting on in the test, i.e., you pass an object in to the object under test, and assert the referenced object is passed to a dependency.

### git Commits

TO begin with I really didn't like the vague commit messages that Jbrains uses such as 'extracted a method' or 'renamed a variable'.  However, over time, I have got used to them and now find myself producing surprisingly similar commit messages!

I was already in favour of very tight commits, with anything not relevant to this particular change being committed separately.  I would even exclude removing an extraneous empty line or removing some unused namespaces as deserving of a different commit.  The less I have in a commit that isn't truly about the change I am committing, the easier it is for me to see what the change was when I come back to it.

Favourite episodes
---

### Series 4, 8: A Long Look Down the Road

An in-depth refactoring of a small class.  I think this episode gives a good insight to the level of sensitivity to duplication, naming, and levels of abstraction required of a developer.  In fact I was so impressed with the content of this episode that I was inspired to write a [blog post](/2017/02/01//duplication-and-abstraction/) based on it.

### Series 5, Episode 6: Before We Move On

Jbrains walks through a number of smells he can see in the code and gives possible solutions to them.  Again it's nice to see what design issues catch his attention, why they catch his attention and what he would like to do about them.

Thrilling Conclusion
---

As you can see, I feel I got a fair amount out of the course.  Certainly enough to cover the US$147 it costs, especially if your employer pays for it (thanks [energyhelpline](https://www.energyhelpline.com/fri/)).  I think most developers would get a fair amount from the course as it covers just about everything from the basics of TDD, writing test lists, and how to use test doubles, up to more advanced topics like creating good abstractions.  Teaching how to create good abstractions is hard (or at least I've found it difficult to learn!) and I think videos where the developer explains their thought process whilst they are discovering a design are one of the best ways to learn how to do it, and Jbrains leaves enough warts in to make it feel as though we are watching somebody go through this process.

One thing I would do the same was to stop the videos each time Jbrains is about to implement a feature and give it a go myself first.  I found this engaged my brain and I got a deeper understanding of what Jbrains was teaching and made things easier to remember.

One thing I probably should have done was to update my code to match Jbrains after I'd seen what he'd done.  Thinking that I knew best, I put test doubles in immediately because it looked like the right thing to do.  It would have been better if I had just followed along with his examples from the beginning so I could feel the pain and reinforce why it is a good idea to use test doubles in certain places, as he shows in the second half of the course.