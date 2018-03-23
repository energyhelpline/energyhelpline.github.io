---
layout: post
title:  "Refactoring by Baby Steps"
author: Ronan Moriarty
comments: true
excerpt: Seeing a tangled mess of code transformed into a cleaner design can be very rewarding. However if you also manage this transformation through a serious of quick, easy, safe steps, all the while keeping your solution building and tests passing, you can take on a refactoring of any size with confidence.
tags:
 - refactoring
 - clean-code
---

## Why do Baby Steps?

We pair-program daily at energyhelpline. This practice really helps us to hone our skills as we see how other developers approach problems and techniques and tools they use.

One problem-solving approach that I was aware of, and did to some degree, but didn't fully appreciate until I saw how others do it, is refactoring by baby steps.  Seeing a tangled mess of code transformed into a cleaner design can be very rewarding. However if you also manage this transformation through a serious of quick, easy, safe steps, all the while keeping your solution building and tests passing, you can take on a refactoring of any size with confidence.

**The trick to tackling a refactoring of any size is to identify the next baby step that moves you towards that cleaner design.**

## Example - Introducing a New Dependency

I try to keep my method signatures brief, with as few arguments as possible, but whenever I see multiple responsibilities in a class, SOLID's Single Responsibility Principle advises us to split them out into separate classes. This often means the original class will gain a new constructor dependency. We typically use dependency injection to hook all our dependencies together in production, but **unlike our production code, our tests don't tend to use dependency injection, so if you have a class like that below referenced in many tests, you could find yourself in a similar situation**. So let's assume we've got the following existing signature :

```c#
public class ClassA
    {
        public ClassA()
        {
        }

        public void DoSomething()
        {
            // method doing too much, with some code that could be reused in other contexts if it wasn't all inlined here.
        }
    }
```

and we want to change it to :

```c#
public class ClassA
    {
        private readonly ClassB _classB;

        public ClassA(ClassB classB)
        {
            _classB = classB;
        }

        public void DoSomething()
        {
            // still doing most of what we did before inline here
            // ...

            // but we've pulled out at least one responsibility to a new class.
            _classB.DoOneThingWell();

            // ...
        }
    }

    public class ClassB
    {
        public void DoOneThingWell()
        {
            // contains some code that used to live in ClassA.
        }
    }
```

Let's also assume this ClassA constructor is already called in a *lot* of places (that could be a code smell in its own right, but I'll consider that a problem for another time!).

So what are the options for how we go about making the transformation above? I'll consider two different approaches.

### Option 1 - The Big Bang

Previously, I probably would have done the following, all in one commit. I'd use ReSharper to make the job a bit easier and safer, but the general steps would look like this:
 - create the new ClassB with the one public method, DoOneThingWell()
 - copy the functionality from the ClassA.DoSomething() to the ClassB.DoOneThingWell()
 - add parameters to DoOneThingWell() as needed to pass any data necessary
 - add a new constructor dependency in ClassA to get access to an instance of ClassB
 - remove code from ClassA that is now in ClassB
 - invoke ClassB.DoOneThingWell() at the point where the code used to live in ClassA.
 - build the solution - this would highlight all the breaking constructor calls to ClassA that weren't passing the new ClassB dependency.
 - fix all the build errors in your solution by passing in the new ClassB dependency

ReSharper will definitely make doing these steps easier than if you had to do it manually, but ReSharper refactorings will still be limited to one solution. So when you think you're done, and check-in and push your changes, your CI might highlight that this dll is being used in various other solutions that you weren't even aware of! That's a whole load of other places to fix! That's potentially a lot of disruption caused and you might wish you had left well-enough alone, and end up reverting your changes before you get lynched by the rest of the team!

*So what can we do to make this a little bit easier? What's the smallest thing that we can do to use this new dependency?*

### Option 2 - Baby Steps

#### First Baby Step - Instantiate the New Dependency Within the Existing Class

This next approach uses a series of baby steps, to make our life a little easier than the Big Bang approach above. By taking the following approach, we don't have to consider all the places where ClassA is instantiated just yet - we can deal with that problem later. So let's do the following as a first step:

```c#
public class ClassA
    {
        public void DoSomething()
        {
            // still doing most of what we did before inline here
            // ...

            // ClassB is now instantiated inline instead of passing in through ClassA's constructor.
            new ClassB().DoOneThingWell();

            // ...
        }
    }

    public class ClassB
    {
        public void DoOneThingWell()
        {
            // contains some code that used to live in ClassA.
        }
    }
```

Instantiating ClassB directly above doesn't look very pretty! But it has allowed us to split our responsibilities out into separate classes, and a step like this can typically be done in a couple of minutes, so it was a good first step.

If we have tests around ClassA, they should all continue to pass after this change above. We haven't changed the constructor for ClassA. With that working, we can commit, and switch our focus to the next problem - injecting the new dependency.

#### Second Baby Step - Optional Dependency Injection

Now we'll take the next step to introduce ClassB as a constructor dependency in ClassA. *Remember, we're assuming that there's a lot of places in our code base that instantiate ClassA.* So it would be nice if we can continue to support that existing constructor signature, i.e. the blank constructor, for the time being.

So, to continue to support the existing way of instantiating ClassA, while introducing a new way to instantiate ClassA taking this new dependency, let's have two constructors, chaining the blank one into the new one:

```c#
    public class ClassA
    {
        private readonly ClassB _classB;

        public ClassA()
            : this(new ClassB())
        {
            // this is the constructor that all the existing code will use initially.
        }

        public ClassA(ClassB classB)
        {
            // Initially, only the constructor above will call into this. There won't be any direct calls to this constructor from outside of this class.
            _classB = classB;
        }

        public void DoSomething()
        {
            // still doing most of what we did before inline here
            // ...

            // ClassB is now instantiated inline instead of passing in through ClassA's constructor.
            _classB.DoOneThingWell();

            // ...
        }
    }

    public class ClassB
    {
        public void DoOneThingWell()
        {
        }
    }
```

Again, this step will take no more than a couple of minutes, and all your existing calls to instantiate ClassA will continue to work as expected. So let's check that in, and move on. Next, we can switch our focus to all the different code that instantiates ClassA.

#### Third Baby Step - Inject the New Dependency

So, let's say we have 100 different places that are doing something similar to the following:

```c#
    var classA = new ClassA();
```

We can take each of those calls, *in isolation*, and change it to:

```c#
    var classA = new ClassA(new ClassB());
```

We now have a choice - we could choose to work through all calls in one go. Or, we can change a few, check in, and come back to it at a later stage, if there's something more urgent that we need to switch our attention to. **We can check in at any time** The old way of instantiating ClassA, with no constructor arguments, continues to be supported, but we have a new way that we can migrate to, in our own time. No need to abandon the refactoring at any point because it became a bigger piece of work than we expected.

#### Fourth Baby Step - Remove the Old Blank Constructor

Whenever we're certain we've removed all the old calls to ClassA's blank constructor, we can remove the blank constructor, leaving us with:

```c#
public class ClassA
    {
        private readonly ClassB _classB;

        public ClassA(ClassB classB)
        {
            _classB = classB;
        }

        public void DoSomething()
        {
            // still doing most of what we did before inline here
            // ...

            // ClassB is now instantiated inline instead of passing in through ClassA's constructor.
            _classB.DoOneThingWell();

            // ...
        }
    }

    public class ClassB
    {
        public void DoOneThingWell()
        {
        }
    }
```

## Conclusion

The approach outlined above can be generalised as follows:
 - Create a new cleaner way of doing something, but continue to support the old way for now.
 - Switch each invocation of the old way to use the new way instead.
 - When nothing is using the old way anymore, delete the old way.

Once you start using baby steps, it's difficult to consider ever going back to the Big Bang approach. I always look for the baby step now when I'm refactoring, because it makes refactoring so much easier than before. Each baby step is so trivial, but this means they can each be done very quickly - these baby steps quickly add up, giving you an easy way to take on refactorings of any size.