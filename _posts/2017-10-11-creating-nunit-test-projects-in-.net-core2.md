---
layout: post
title: Creating NUnit Projects in .Net Core 2.0
author: Richard Nagle
excerpt: Shows the steps required to create a new NUnit project when using .Net Core 2.0
---

Since starting to play with .Net Core 2.0 I have, several times, set-up new NUnit test projects. Each time I struggle
to remember the steps to do it; so I'm writing this to remind myself and hopefully someone else will find it useful.

I use the CLI, but that's not a pre-requisite, you can easily carry out the equivalent steps in Visual Studio or you
could create the .csproj file manually (in fact the final .csproj is shown at the end of this post).

Firstly create your test project:
```
dotnet new classlib -f netcoreapp2.0 -n my.test.project -o .\tests
```

The `-f netcoreapp2.0` is important so that you create a project targeting the .Net Core 2.0 framework. If you omit it
then the default is to target .Net Standard 2.0 and your tests will not run. `-n` is the name of the project and 
`-o` is the output directory.

Then add the new test project to your solution:
```
dotnet sln add .\tests\my.test.project.csproj
```

Then change to the folder containing the test project
```
cd .\tests
```

And add the following packages to the test project
```
dotnet add package nunit

dotnet add package nunit3testadapter

dotnet add package microsoft.net.test.sdk
```

Now you're good to go.

Your final project should look like this
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netcoreapp2.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="15.3.0" />
    <PackageReference Include="nunit" Version="3.8.1" />
    <PackageReference Include="nunit3testadapter" Version="3.8.0" />
  </ItemGroup>
</Project>
```