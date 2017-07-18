---
layout: post
title: Optimising NuGet Update
author: Richard Nagle
excerpt: NuGet update can be agonisingly slow. I recently optimised our build process and found some ways to improve the speed.
---

![Nuget logo'](/images/nuget.png "Nuget logo")

At Energy Helpline we publish our shared .net packages to our in-house NuGet server so that they may be consumed by our applications and websites. We have automated our build process so that when we publish an update to a NuGet package; any downstream application using that package is updated to the latest version, rebuilt, tested and deployed. However we have found over-time as the number of packages has increased the build-time for 'update packages to latest version' has been getting slower and slower.

This week I set about trying to optimise the build time for our NuGet package updates. The build-time for our main energy-switching API was over 14 minutes just for the NuGet package update part.

In pseudo-code the NuGet package update process is something like this:

```
installedPackages = Get-list-of-installed-packages-by-parsing-
                       packages.config-files

availablePackages = Get-list-of-packages-available-on-our-
                       nuget-server

targetPackages = Intersection-of-installedPackages-
                    and-availablePackages

foreach targetPackage in targetPackages
{
    nuget update my.sln -Id targetPackage -Source nuget-url
}
```
After several false starts, I found the main culprit to be `nuget update` which appears to be agonisingly slow. Given this tardiness, it doesn't make sense to
make several calls to `nuget update` within a loop; once for each package that we want to update. If only there was some way we could remove that `foreach` loop and call `nuget update` just once.

It turns out there is. The `-Id` argument to `nuget update` is used to specify which package we want to update. Instead of specifying `-Id` once per package in a loop I discovered that it can be specified multiple times; in other words instead of

```
nuget update my.sln -Id pkg1
nuget update my.sln -Id pkg2
nuget update my.sln -Id pkg3
```

We can do

```
nuget update my.sln -Id pkg1 -Id pkg2 -Id pkg3
```

This speeds thing up significantly - that 14 minute build-time shrunk down to just 90 seconds. Then by updating the `nuget.exe` commmand-line from 3.5 to 4.1 we managed to get this down to 40 seconds - an improvement of 2100%.

The full code for our powershell script to do this can be found [here](https://gist.github.com/richardnagle/2ffa63c3a9a711aa97c929192d8cbcef)