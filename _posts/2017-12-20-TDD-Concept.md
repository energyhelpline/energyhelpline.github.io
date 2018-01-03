---
layout: post
title: TDD Concept
author: Ranu Miah
excerpt: The conceptual process of TDD ...
---

## Core Principles

The fundamental of TDD is to Write a failing test (Red), then make the test pass asap (Green), and finally Refactor (if possible).  I will share my experience and go over more detail of the 3 core principles.

### STEP 1 --- write a failing test

Write a test that describes the functionality you wish to build. I found that the best way to do this is to write the **_assertion first_**. Then think how you will solve it in your mind and just code the blueprint of the solution. Create anything you need for the code to compile but don't implementing any logic yet i.e. if a method is missing then create an empty method or if a class is missing then create and empty class. Ensure the test compiles and is in a failing state.

A test should only be testing one *"concept"*.  This mean a test can have multiple assertions as long as they are logically testing the same abstract concept. Testing a work flow of a function i.e. ensuring a computer can execute lines of code which has no conditional logic is a bad test. However, if it was testing a conceptual logic then it is ok. For example, if the order of those statement matters or the number of calls made, to a particular method is important. A test should speak to us more clearly, as if it were an **_assertion of truth_**, not a **_sequence of operations_**. The assertion in the test must clearly express **the fact** i.e. what is the purpose of this test.

### STEP 2 --- Make the test pass ASAP

The naturally thing to do as a developer is *"make it work!"*. Some does it with elegant design while others does it with ugly code.  What matters is that you should be able to achieve the *"green state"* within seconds and not in minutes. Therefore if necessary hard code the answer and wait for a triangulation (writing a test to make the previous test fail so that you can refactor the code). This short feedback loop is imperative because at this point you need to make a choice to either go forward with next step or go backward with a new test.

### STEP 3 --- Refactor!

If you can see how to make the code better then do it now.  Otherwise carrying on writing more tests. It takes lots of hardship and practices to refactor with great success.  It is at this stage where TDD truly shines.  The previous steps assist\allow you to improve code. Why? Because if you accidently change the behaviour then your test will fails. Once the tests are in place and passing you can start to clean up the code to ensure best practise is applied.  This is the stage that is actually incredible hard to achieve.  It is not always obvious how to make the code clean.  There are many techniques that is described in the [Clean Code](https://www.amazon.co.uk/Clean-Code-Handbook-Software-Craftsmanship/dp/0132350882) & [Refactor Books](https://www.amazon.co.uk/Refactoring-Improving-Design-Existing-Technology/dp/0201485672/ref=sr_1_1?s=books&ie=UTF8&qid=1514375481&sr=1-1&keywords=refactoring) that will assist with refactoring.

### My Experience

At the start of my TDD journey one of my favourite things I used to do is MOCK everything in a test and Assert that all MOCK was used.  Well I learnt that mocking everything means you are actually not doing any test. One of the hardest things that I have found practicing TDD is to write a test that describe what I'm thinking. Over time I've learnt that it usually meant that the test I need is to big to write within the TDD cycle. Therefore I started to write smaller tests, which ultimately leads me to working out how to write the big test that I initially thought of. Another thing I've come to accept is to experiment with test.  I've faced many time when I have no idea how to move forward. So I write a test to prove an assumption, If that test fails I simply discard and come up with another assumption until I find the right one. Important thing to note here, is never worry about discarding test.

Working on legacy system I found writing a test to be a daunting process and have skipped TDD practises many times.  However, I've learnt that I need to deal with it in a smaller problems. The first thing that I should do is simply get a test, which only executes the method I'm interested in.  If its a Query function then assert a non-default value or if it is a Command function then ensure the state is changed from it's default state.  Still not worrying about the actually feature I'm interested in for the test yet.  Once I've got a working test then I can build on it and work towards to what I really wish to test.

Bare something in mind: while TDD encourages you to take baby steps, it is not necessary if you are able to jump a few steps.  However it is a must that you can take the baby step as when you do jump and fail, you will simply resort back to baby step and carrying on, without really delaying the development time. This is really achieved by the first two steps. Make sure you keep a list of TODO's and implement them at the correct steps. For example, if you see a code smell, then clean it up during the refactor phase. Or if you have a design decision then ensure that you have this written down into Test and let that drive your code. If you're stuck, stop and take a break and start again. Never be afraid to start over again in TDD

Hopefully some of my experience help you on your journey to mastering TDD.