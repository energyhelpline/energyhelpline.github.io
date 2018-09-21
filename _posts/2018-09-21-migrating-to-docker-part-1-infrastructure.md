---
layout: post
title: "Migrating to Docker - Part 1: Infrastructure"
author: Ronan Moriarty
excerpt: In the first post in this series, I'll be looking at the overall strategy for migrating different components of a solution to Docker one component at a time, while maintaining confidence that all components still interact correctly. This first post will focus on migrating infrastructural components to Docker - in my case, Sql Server and RabbitMQ.
---

## Background

My [CQSplit project](https://github.com/ronanmoriarty/CQSplit) contains the codebase for my [CQSplit nuget packages](https://www.nuget.org/packages?q=CQSplit) intended as a starting point for implementing [CQRS](https://martinfowler.com/bliki/CQRS.html) The same repo contains a sample application that demonstrates how to use those packages. The sample application includes:
* a RabbitMQ server
* 2 SQL Server instances
* 2 .NET Core services
* an ASP.NET Core website.

Having a few different components like this creates a problem though - setting up a correctly configured RabbitMQ service and SQL Server instances with the right database schemas is prone to error, and so it's a barrier to set up. It means other developers and build agents need various dependencies available to build and run tests and to run the application. I could have created scripts to install these various dependencies, but *who wants to clutter up their development environment with a bunch of dependencies they might not need on any other project?* This is one of Docker's big selling points (for me, at least) - environments can be created when they're needed, with all dependencies included, and dropped when they're no longer required.

**So my main aim then was to minimise the number of installs needed on a developer environment beyond Visual Studio / VS Code to just Docker.**. I wanted developers to be able to run the application, and run all the tests (including integration tests and acceptance tests) without having to install anything beyond Docker.

By the end of this refactoring work, running the application, or running tests would be as simple as opening a PowerShell window and running the appropriate [Cake](https://cakebuild.net/) custom task:

* .\build.ps1 -Target Run-Sample-Application
* .\build.ps1 -Target Run-CQSplit-Tests
* .\build.ps1 -Target Run-Sample-Application-Tests

These commands would do some or all of the following, as needed:
* pull my RabbitMQ image from Docker Hub
* build Sql Server Docker images (tweaked from Microsoft base images to aid migrating Sql schemas)
* build Docker images for my 2 .NET Core services and ASP.NET Core website
* build Docker images for running integration tests and acceptance tests
* start containers based on all the images above, attaching existing databases to the database servers in the Sql Server containers, migrating schemas if out-of-date, or create databases if they don't exist
* tear down containers afterwards

The application and tests could then both be built and run without installing SQL Server, RabbitMQ or even the .NET Core SDK.

**So all of the above works very nicely now using the above Cake tasks, but how did I get to this point, from a situation where I wasn't using Docker at all?**

## Baby Steps to Manage Risks

As I'm relatively new to Docker, I didn't want to have to migrate one RabbitMQ service, two Sql Server instances, the related databases, two .NET services and a website to Docker in one giant refactoring, only to potentially discover at the very end that there was a fundamental flaws in my understanding of Docker - that would be a lot of wasted effort! **So instead of doing one big risky refactoring, I wanted to start in a good state, initially with nothing running in Docker, and then move in small steps towards everything being in Docker, knowing that at every step along the way the whole application was still in a good state.** Bit by bit, I'd get more and more pieces of the solution running in Docker, until eventually all the following were running in Docker (migrated in the following order):
* RabbitMQ server
* Sql server for write model
* Sql server for read model
* Waiter command service (.NET Core) (using RabbitMQ and write model db)
* Waiter event projecting service (.NET Core) (using RabbitMQ and read model db)
* Waiter website (ASP.NET Core) (using RabbitMQ and read model db)

So, if there's a lot of baby steps to migrate to Docker, we need a quick way to check frequently if the application is still in good working order, i.e. after each baby step. So first of all, I needed a good end-to-end test.

## Step 1 - Add an End-to-End Test

So in the process of working towards moving everything to Docker, **I wanted at least one end-to-end test that would exercise all components of the solution, whether those parts were currently running in containers or not.** For the earlier stages, the end-to-end test itself would continue to run on the host, but over the course of the refactoring work, everything that the test relied on would get migrated to be hosted in containers. Once I had all the infrastructure and services running in Docker, I could switch my focus to getting the tests themselves running in their own Docker containers. So, before migrating anything to Docker, I wrote an end-to-end test for the Sample Application (a restaurant line of business application) to check that a waiter could open a new tab after seating some guests at a table :

![End to End Test](https://www.websequencediagrams.com/cgi-bin/cdraw?lz=U2VsZW5pdW0gVGVzdC0-V2Vic2l0ZTogQ3JlYXRlIFRhYgoADQctPlJhYmJpdE1ROiBPcGVuVGFiQ29tbWFuZAoAEQgtPgALByBTZXJ2aWNlABkRABEPAB8SVGFiT3BlbmVkIEV2ZW50AB8SABIFIFN0b3JlIChXcml0ZSBNb2RlbCkAGSIAgTcJAFUQAIE2CgBYBlByb2plY3RpbmcAgH8ZABEYLT5SZWFkAIENBjpVcGRhdGUgdG8gcmVmbGVjdCAAgU8QAIJwDwCDAQ06V2FpdCA1IHNlY29uZHMAHBAAgyMIUmVmcmVzaCBUYWJzIHZpZXcAgyYLAIEBCkdldCBsYXRlc3QgdGFiADQZQ2hlY2sgbmV3IHRhYiBleGlzdHMK&s=napkin)


### Running the End-to-End Test Before Starting Docker Migration

My end-to-end test TabAcceptanceTest looked like this:

```
private const int TableNumber = 345;
private const string Waiter = "TabAcceptanceTest";
private IEnumerable<ExternalProcess> _externalProcesses;

[SetUp]
public void SetUp()
{
    OpenTabs.DeleteTabsFor(Waiter);
    _externalProcesses = Start.AllWaiterServices();
}

[Test]
public void Created_tab_is_displayed_on_open_tabs_view()
{
    using (var browserSession = CafeWaiterWebsite
        .CreateTab
        .WithTableNumber(TableNumber)
        .WithWaiter(Waiter)
        .AndSubmit())
    {
        AllowTimeForMessagesToBeConsumed();
        browserSession.RefreshPage();
        Assert.That(browserSession.OpenTabs
            .ContainsSingleTab
                .WithWaiter(Waiter)
                .WithTableNumber(TableNumber)
        );
    }
}

[TearDown]
public void TearDown()
{
    _externalProcesses.ForEach(externalProcess => externalProcess.Stop());
}

private void AllowTimeForMessagesToBeConsumed()
{
    Thread.Sleep(5000);
}
```

As you can probably guess, under the hood I'm using Selenium to test all the components through the waiter's web interface.

Before moving anything to Docker, Start.AllWaiterServices() in the SetUp() started my website and my two .NET Core services in the various bin\Debug\netcoreapp2.0\ folders. These were ran using Systems.Diagnostic.Process (in a wrapper class called ExternalProcess) to start them as separate processes, capturing any output from them for the test output. 3 instances of the ExternalProcess class were created to represent my 2 .NET Core services and my website.

Also, initially the test would only pass if I had RabbitMQ and Sql Server running locally as they weren't hosted in Docker yet. I'd remove these from my host machine later. Bit by bit, the Start.AllWaiterServices() method would dwindle to do less and less, with the test implicitly relying on Docker containers more.

## Step 2 - Migrating RabbitMQ to Docker

With my end-to-end test working, I [developed a RabbitMQ Docker image]({% post_url 2018-02-28-setting-up-rabbitmq-in-a-docker-windows-container %}). I decided I'd make it public and upload it to [Docker Hub](https://hub.docker.com/r/ronanmoriarty/rabbitmq-windowsservercore/). Then I ran :

```
docker run -p 5672:5672 -p 15672:15672  ronanmoriarty/rabbitmq-windowsservercore .
```

**With the RabbitMQ container running now, I had to reconfigure all the components that my end-to-end test relied on to use the RabbitMQ service in my Docker container instead of the RabbitMQ server on my host (which I would be able to remove shortly).**

### Temporary Workaround for Port Forwarding Issues

At the time I was doing these baby steps, I ran into a bit of a stumbling block (which I've only recently resolved) - I couldn't get Docker's port-forwarding to work - this was an issue with Windows containers at one point. Port forwarding would have allowed me to publish ports from the container to the "localhost" loopback address on my host. Without port forwarding, I was only able to access a container from the host using the container's IP address. The problem with this is that a container's IP address changes every time it restarts. **So, for all components to play well together, the components running directly on my host (i.e. those not yet migrated to Docker) needed to be configured to communicate with container-hosted components using whatever the current IP addresses of those docker containers were.** So, I wrote a PowerShell script to get the current IP addresses of the Docker containers, and create appSettings.json files using these values.

### Determining IP Addresses of Currently-Running Containers

I created appSettings.json.template files to store in source control, that looked like regular appSettings.json files, but included placeholder variables starting with '$':
```
{
    ...
    "rabbitmq": {
        "uri": "rabbitmq://$rabbitMqServerAddress",
    ...
    },
    ...
}
```

**This template file in source control would be used to create an appSettings.json file dynamically - as the appSettings.json would change whenever container IP addresses changed, the appSettings.json file was not stored in source control.**

Then, after starting the RabbitMQ container, but before I ran the tests, I ran a PowerShell script .\Update-Settings-To-Use-Docker-Containers.ps1 that would find a running container based on my ronanmoriarty/rabbitmq-windowsservercore Docker image, and find the current IP address of that container:

```
{% raw %}
function GetContainerRunningWithImageName($imageName){
    Write-Host "Finding container based on image named '$imageName'..."
    return docker container list --filter ancestor=$imageName --format "{{.ID}}"
}

function GetIpAddress($containerId){
    return docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' $containerId
}

function GetRabbitMqAddress(){
    $rabbitMqContainerId = GetContainerRunningWithImageName "ronanmoriarty/rabbitmq-windowsservercore"
    $rabbitMqServerIpAddress = GetIpAddress $rabbitMqContainerId
    return $rabbitMqServerIpAddress
}
{% endraw %}
```

**Note that I only needed to do the above IP address resolving above as I couldn't get port-forwarding working initially, and so couldn't put**
```
"uri": "rabbitmq://localhost:35672"
```
**in any host component appSettings.json file (where 35672 would have been the host port mapped to the RabbitMQ container port 5672).**

**The general approach outlined below, though, of combining appSettings.json.templates files and key-value pairs to generate appSettings.json files, was also useful for keeping passwords out of source control.**

### Creating Configuration Files from Template Files and Key-Value-Pairs

Using the IP address for the RabbitMQ container determined above, a list of placeholder key-value-pairs would be generated, with one of them being something like "$rabbitMqServerAddress=172.15.13.76", and this list of key-value-pairs would be used to create new appSettings.json files based on the appSettings.json.template files:

```
function GetKeyValuePairs()
{
    $keyValuePairs = @{}
    $rabbitMqServerAddress = GetRabbitMqAddress
    $keyValuePairs.Add("`$rabbitMqServerAddress", $rabbitMqServerAddress)
    return $keyValuePairs
}

function SwapPlaceholdersToCreateNewJsonFiles([string[]] $paths, [string] $targetName, [hashtable] $keyValuePairs)
{
    if($paths.Length -eq 0)
    {
        Write-Output "No template files supplied."
        return
    }

    $paths | ForEach-Object {
        $sourcePath = $_
        $sourceName = [System.IO.Path]::GetFileName($sourcePath)
        Write-Output "Replacing placeholders in '$sourcePath' to create new file called '$targetName' in same directory."
        $targetJsonPath = $sourcePath.Replace($sourceName, $targetName)
        if(Test-Path $targetJsonPath)
        {
            Remove-Item $targetJsonPath
        }

        (GetJsonTemplateFileWithPlaceholdersReplaced $sourcePath $keyValuePairs) | Set-Content $targetJsonPath
        Write-Output "Created $targetJsonPath"
        Write-Output (Get-Content $targetJsonPath)
    }
}

function GetJsonTemplateFileWithPlaceholdersReplaced([string] $filePath, [hashtable] $keyValuePairs)
{
    $temp = (Get-Content $filePath)

    $keyValuePairs.Keys | ForEach-Object {
        $value = $keyValuePairs[$_]
        $temp = $temp.Replace($_, $value)
    }

    return $temp
}

....

$keyValuePairs = GetKeyValuePairs

$paths = (Get-ChildItem -Path .\ -Filter appSettings.json.template -Recurse) | Select-Object -ExpandProperty FullName

SwapPlaceholdersToCreateNewJsonFiles $paths appSettings.json $keyValuePairs
```

This .\Update-Settings-To-Use-Docker-Containers.ps1 script would call the function to get the IP address of the RabbitMQ container, and create an appSettings.json file based on the appSettings.json.template file above, but with the placeholder for the RabbitMQ server address swapped out with the current RabbitMQ container IP address, e.g. something like:
```
{
    ...
    "rabbitmq": {
        "uri": "rabbitmq://172.15.13.76",
        ...
    },
    ...
}
```

With the updated appSettings.json file in place, I was ready to run my end to end test. The test passed (after some minor script tweakage!), and so did all my other unit tests and integration tests.

**So I now had my RabbitMQ server running in Docker, with everything else in my solution still running on my host developer machine.**

I switched off my local RabbitMQ to make sure my tests were relying on the Docker instance of RabbitMQ. All was good, so I moved onto dockerising the Sql Server bits.

## Step 3 - Migrating SQL Servers to Docker

### Swapping out the SQL Server Address Placeholders

Continuing from the bottom up, I [created Docker images for SQL Server]({% post_url 2018-08-16-setting-up-sql-server-in-a-docker-container %}). To run the end to end test above using a SQL Server instance running in Docker, and to run other integration tests that also relied on SQL Server, I extended the approach above to work for Sql Server as well as RabbitMQ. The code below was added to my PowerShell scripts:

```
function GetWriteModelSqlServerAddress(){
    $writeModelSqlServerContainerId = GetContainerRunningWithImageName "$($repositoryName)_waiter-write-db-server"
    $writeModelSqlServerIpAddress = GetIpAddress $writeModelSqlServerContainerId
    return $writeModelSqlServerIpAddress
}

function GetReadModelSqlServerAddress(){
    $readModelSqlServerContainerId = GetContainerRunningWithImageName "$($repositoryName)_waiter-read-db-server"
    $readModelSqlServerIpAddress = GetIpAddress $readModelSqlServerContainerId
    return $readModelSqlServerIpAddress
}
```

Then I replaced the localhost SQL Server addresses with $readModelSqlServerAddress and $writeModelSqlServerAddress placeholders in the appSettings.json.template files:
```
{
    ...
    "connectionString": "Server=$writeModelSqlServerAddress;Database=Cafe.Waiter.WriteModel;..."
}
```

Then I updated .\Update-Settings-To-Use-Docker-Containers.ps1 script to include these new key-value-pairs:

```
function GetKeyValuePairs()
{
    $keyValuePairs = @{}
    $rabbitMqServerAddress = GetRabbitMqAddress
    $writeModelSqlServerAddress = GetWriteModelSqlServerAddress
    $readModelSqlServerAddress = GetReadModelSqlServerAddress

    $keyValuePairs.Add("`$rabbitMqServerAddress", $rabbitMqServerAddress)
    $keyValuePairs.Add("`$writeModelSqlServerAddress", $writeModelSqlServerAddress)
    $keyValuePairs.Add("`$readModelSqlServerAddress", $readModelSqlServerAddress)
    return $keyValuePairs
}
```

Starting up the Sql Server containers alongside my RabbitMQ server container, I ran my .\Update-Settings-To-Use-Docker-Containers.ps1 script again to recreate the appSettings.json files from the updated appSettings.json.template files. All the tests, including my end-to-end test and integration tests continued to pass. So now I could consider what to do about passwords in my connection strings.

### Sql Server Authentication

I [blogged previously]({% post_url 2018-08-16-setting-up-sql-server-in-a-docker-container %}) about using a .env file to supply passwords to the Sql Server Dockerfiles so that we could set up users in the databases with Sql Server authentication. Now even though the .env file is documented in Docker, I decided to reuse the .env file to avoid storing passwords in the related connectionstrings in the appSettings.json.template files. I wrote Powershell functions to extract the password from the .env file:

```
function GetEnvFilePath()
{
    return [System.IO.Path]::GetFullPath([System.IO.Path]::Combine((get-item $PSScriptRoot).Parent.Parent.Parent.FullName, '.env'))
}

function GetEnvironmentVariableFromEnvFile($environmentVariableName)
{
    return [regex]::Match((Get-Content $envPath),"$environmentVariableName='([^=]*)'").captures.groups[1].value
}

function GetWaiterWebsitePassword()
{
    return GetEnvironmentVariableFromEnvFile "waiterWebsitePassword"
}

function GetCommandServicePassword()
{
    return GetEnvironmentVariableFromEnvFile "commandServicePassword"
}

function GetEventProjectingServicePassword()
{
    return GetEnvironmentVariableFromEnvFile "eventProjectingServicePassword"
}

$envPath = GetEnvFilePath
```

Then I updated the connectionstring in the appSettings.json.template files to include the placeholder:

```
{
    ...
    "connectionString": "...;User Id=CommandService;Password=$commandServicePassword;"
}
```

I changed my .\Update-Settings-To-Use-Docker-Files.ps1 to make use of a new key value pair:

```
function GetPasswordKeyValuePairs()
{
    $waiterWebsitePassword = GetWaiterWebsitePassword
    $commandServicePassword = GetCommandServicePassword
    $eventProjectingServicePassword = GetEventProjectingServicePassword

    $keyValuePairs = @{}
    $keyValuePairs.Add("`$waiterWebsitePassword", $waiterWebsitePassword)
    $keyValuePairs.Add("`$commandServicePassword", $commandServicePassword)
    $keyValuePairs.Add("`$eventProjectingServicePassword", $eventProjectingServicePassword)
    return $keyValuePairs
}

function GetKeyValuePairs()
{
    $keyValuePairs = GetPasswordKeyValuePairs
    ... # add other key-value-pairs listed earlier in the blog post
    return $keyValuePairs
}
```

I followed the same process for read and write model databases.

With a RabbitMQ and 2 SQL Server images, instead of having to issue various docker-run statements to start different Docker containers for each component, now seemed like the right time to create a docker-compose file:

```
version: '3'
services:
    rabbitmq:
        image: ronanmoriarty/rabbitmq-windowsservercore
    waiter-read-db-server:
        build:
        context: .\src\Cafe\Docker\SqlServer\
        dockerfile: .\ReadModel\Dockerfile
        env_file:
        - .env
    waiter-write-db-server:
        build:
        context: .\src\Cafe\Docker\SqlServer\
        dockerfile: .\WriteModel\Dockerfile
        env_file:
        - .env
```

Running "docker-compose up -d" built and started my 3 containers. Then .\Update-Settings-To-Use-Docker-Files.ps1 updated the appSettings.json files.

**With all tests passing, including my end-to-end test and integration tests, I was able to disable the Sql Server instances hosted locally. So at this point, I had removed the need to have Sql Server or RabbitMQ installed on the host machine - these two dependencies were now being provided through Docker.**

## Next Time...

Having now removed the reliance on having a RabbitMQ server or SQL Server instances running on my host, the main barriers to setting up a Development environment were now gone.

Next time, I'll show how I moved my .NET Core services and ASP.NET Core website into Docker containers of their own, so that my whole application, not just the infrastructure, was running in Docker. All code for this project is available [here](https://github.com/ronanmoriarty/CQSplit)
