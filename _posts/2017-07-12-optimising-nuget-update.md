---
layout: post
title: Optimising NuGet Update
author: Richard Nagle
excerpt: NuGet update can be agonisingly slow. I recently optimised our build process and found some ways to improve the speed.
---

![Nuget logo'](/images/nuget.png "Nuget logo")

At Energy Helpline we publish our shared .net packages to our in-house NuGet server so that they may be consumed by our applications and websites. We have automated our build process so that when we publish an update to a NuGet package; any downstream application using that package is updated to the latest version, rebuilt, tested and deployed. However we have found over-time as the number of packages has increased the build-time for 'update packages to latest version' has been getting slower, for example it was taking over 14 minutes for our main energy-switching API.

This week I set up trying to optimise this build time. After several false starts, I found the main culprit to be `nuget update` which appears to
be agonisingly slow. In pseudo-code the update process is something like this

```
installedPackages = Get-list-of-installed-packages-by-reading-packages.config-files
availablePackages = Get-list-of-packages-available-on-our-nuget-server

updatedPackages = Intersection-of-installedPackages-and-availablePackages

foreach updatedPackage in updatedPackages
{
    nuget update my.sln -Id updatedPackage -Source url-to-nuget-server
}
```

Having determined that `nuget update` is a slow operation; I think its fairly obvious that the problem is that we call it multiple times within the `foreach` loop. If only there was some way we could move the call outside the loop and call `nuget update` just once. It turns out there is 