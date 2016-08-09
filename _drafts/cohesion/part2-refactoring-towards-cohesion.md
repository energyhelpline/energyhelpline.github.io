---
layout: post
title: Cohesion - Part 2 Refactoring Towards Cohesion
author: Richard Nagle
date: 2016-08-08
tags: cohesion,oo,refactoring
---

In the second part of this series about code cohesion we'll look at how we can refactor an existing class
to be more cohesive and look at the benefits of doing so.

[Previously](/part1-what-is-cohesion) we were examining a class `DiscountCalculator` [Previously](/part1-what-is-cohesion) we were examining a class `DiscountCalculator` and determined that it 
had Lack of Cohesion of Methods (LCOM) score of 0.53. We want this score to be as close to zero as possible
so we need to refactor this class to improve it's cohesiveness. As cohesion is basically a function of keeping 
methods and the field they operate on together, when refactoring we have two main strategies - extracting methods,
and extracting fields.

### Extracting Methods ### 
### Extracting fields ###


### Conclusion ### 
* talk about SRP
* something about coupling?
* is using LCOM important?  