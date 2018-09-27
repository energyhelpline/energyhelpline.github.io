---
layout: post
title: "Docker Migration Example - Part 2: .NET Core Services"
author: Ronan Moriarty
excerpt: With my RabbitMQ and Sql Server instances for my sample application now running in Docker, it was time to move my .NET Core services and website into Docker too.
---

## Background

In the [first post in this series]({% post_url 2018-09-21-migrating-to-docker-part-1-infrastructure %}), I introduced my [CQSplit sample application](https://github.com/ronanmoriarty/CQSplit), containing the following parts that I wanted to move into Docker:
* a RabbitMQ server
* 2 SQL Server instances
* 2 .NET Core services
* an ASP.NET Core site

I showed how the addition of a single end to end test into my test suite could give me the frequent feedback I needed to do the migration one component at a time, with confidence. Then I migrated RabbitMQ and the SQL Server instances into Docker - that alone gave me a lot of benefit - other developers didn't need to install RabbitMQ or SQL Server anymore to work on my project.

In this post, I'll continue with the migration - this time migrating the .NET Core services and the ASP. NET Core website. This will make it easier again to run the application - by the end of this next phase, the whole application can be built and run without even having to install .NET Core SDK (simplifying build agent setup). It should also make deployment easier too. In a real application, this would open the door to leveraging Docker's load balancing features, swarming etc.

## Part 4 - Moving the .NET Core Services to Docker

So the next thing I needed to consider was how different components could communicate *between* docker containers. Up to now, the containers haven't had to talk to each other - the communication has been from the .NET Core services and website on the host to RabbitMQ and SQL Server containers.

### Transforming appSettings.json.template files into appSettings.json files

Now that we're moving the .NET Core services themselves into containers, they'll need appSettings.json files that refer to RabbitMQ and SQL Server by the Docker container names. An example will make this clearer - let's look at the command service first. The command service has an appSettings.json.template file with the following:

```
"connectionString": "Server=$writeModelSqlServerAddress;Database=Cafe.Waiter.WriteModel;User Id=CommandService;Password=$commandServicePassword;",
```

The appSettings.json.template files with these placeholder values are under source control. A PowerShell script then takes the .template files and generates corresponding appSettings.json files, with resolved addresses, passwords etc. These generated appSettings.json files, with their mixture of sensitive data and data that changes depending on the context, never get stored in source control. This PowerShell script would take the appSettings.json.template containing the json fragment above and generate an appSettings.json file containing something like:

```
"connectionString": "Server=localhost,1400;Database=Cafe.Waiter.WriteModel;User Id=CommandService;Password=abc123;",
```

"$writeModelSqlServerAddress" in the template has been replaced with "localhost,1400". That's OK when the command service is running on the host - port 1400 on the host is forwarded to port 1433 on my write-model SQL Server in the docker-compose file. And a password has been inserted too. Now that we're moving the command service into Docker though, we'll want it to look more like this:

```
"connectionString": "Server=cqsplit_waiter-write-db-server_1;Database=Cafe.Waiter.WriteModel;User Id=CommandService;Password=abc123;",
```

The password still resolves in the same way, but the database server address is now listed as cqsplit_waiter-write-db-server_1 - this represents the name of the write model db server container *as it appears to other Docker containers.*

Rather than changing how the placeholder $writeModelSqlServerAddress resolves, from localhost,1400 to cqsplit_waiter-write-db-server_1, **I'm going to start generating two appSettings.json files from the commad service's appSettings.json.template file - the original generated appSettings.json file can continue to be used by the command service when running on the host (useful for debugging purposes), and a new appSettings.docker.json file that will get generated to be included in the command service when running inside a container.**

I changed my PowerShell .\SetUp.ps1 script as follows:
```
function GetKeyValuePairsToUseInsideContainers()
{
    $keyValuePairs = GetPasswordKeyValuePairs
    $keyValuePairs.Add("`$rabbitMqServerAddress", "$($repositoryName)_rabbitmq_1")
    $keyValuePairs.Add("`$writeModelSqlServerAddress", "$($repositoryName)_waiter-write-db-server_1")
    $keyValuePairs.Add("`$readModelSqlServerAddress", "$($repositoryName)_waiter-read-db-server_1")
    $keyValuePairs.Add("`$waiterWebsiteUrl", "http://$($repositoryName)_cafe-waiter-web_1")
    return $keyValuePairs
}

$dockerKeyValuePairs = GetKeyValuePairsToUseInsideContainers
$keyValuePairsToUseFromOutsideContainers = GetKeyValuePairsToUseOutsideContainers
$jsonTemplateFiles = GetAppSettingsTemplateFiles
SwapPlaceholdersToCreateNewJsonFiles $jsonTemplateFiles appSettings.docker.json $dockerKeyValuePairs
SwapPlaceholdersToCreateNewJsonFiles $jsonTemplateFiles appSettings.json $keyValuePairsToUseFromOutsideContainers
```

With the appSettings.json files and appSettings.docker.json files all created, let's see how they get included in the Docker image build.

### The Command Service Dockerfile

I created the following Dockerfile for the command service, explained below:

```
#escape=`
FROM microsoft/dotnet:2.1-sdk AS builder
WORKDIR /app
COPY .\Nuget.config .\Nuget.config
COPY .\.nuget.local .\.nuget.local
COPY .\Cafe\Cafe.Waiter.Contracts\Cafe.Waiter.Contracts.csproj .\Cafe\Cafe.Waiter.Contracts\
RUN dotnet restore .\Cafe\Cafe.Waiter.Contracts
COPY .\Cafe\Cafe.Waiter.Events\Cafe.Waiter.Events.csproj .\Cafe\Cafe.Waiter.Events\
RUN dotnet restore .\Cafe\Cafe.Waiter.Events
COPY .\Cafe\Cafe.Waiter.Domain\Cafe.Waiter.Domain.csproj .\Cafe\Cafe.Waiter.Domain\
RUN dotnet restore .\Cafe\Cafe.Waiter.Domain
COPY .\Cafe\Cafe.DAL.Sql\Cafe.DAL.Sql.csproj .\Cafe\Cafe.DAL.Sql\
RUN dotnet restore .\Cafe\Cafe.DAL.Sql
COPY .\Cafe\Cafe.Waiter.Command.Service\Cafe.Waiter.Command.Service.csproj .\Cafe\Cafe.Waiter.Command.Service\
RUN dotnet restore .\Cafe\Cafe.Waiter.Command.Service
COPY .\Cafe\Cafe.Waiter.Commands\Cafe.Waiter.Commands.csproj .\Cafe\Cafe.Waiter.Commands\
RUN dotnet restore .\Cafe\Cafe.Waiter.Commands
COPY .\Cafe\Cafe.Waiter.Domain.Tests\Cafe.Waiter.Domain.Tests.csproj .\Cafe\Cafe.Waiter.Domain.Tests\
RUN dotnet restore .\Cafe\Cafe.Waiter.Domain.Tests
COPY .\Cafe\Cafe.Waiter.Contracts .\Cafe\Cafe.Waiter.Contracts
COPY .\Cafe\Cafe.Waiter.Events .\Cafe\Cafe.Waiter.Events
COPY .\Cafe\Cafe.Waiter.Domain .\Cafe\Cafe.Waiter.Domain
COPY .\Cafe\Cafe.DAL.Sql .\Cafe\Cafe.DAL.Sql
COPY .\Cafe\Cafe.Waiter.Command.Service .\Cafe\Cafe.Waiter.Command.Service
COPY .\Cafe\Cafe.Waiter.Command.Service\appSettings.docker.json .\Cafe\Cafe.Waiter.Command.Service\appSettings.json
COPY .\Cafe\Cafe.Waiter.Commands .\Cafe\Cafe.Waiter.Commands
COPY .\Cafe\Cafe.Waiter.Domain.Tests .\Cafe\Cafe.Waiter.Domain.Tests
RUN dotnet test .\Cafe\Cafe.Waiter.Domain.Tests\Cafe.Waiter.Domain.Tests.csproj
RUN dotnet publish .\Cafe\Cafe.Waiter.Command.Service

FROM microsoft/dotnet:2.1-runtime
WORKDIR /app
COPY --from=builder ["app\\Cafe\\Cafe.Waiter.Command.Service\\bin\\Debug\\netcoreapp2.1\\publish", ".\\Cafe.Waiter.Command.Service"]
EXPOSE 1433
CMD dotnet .\Cafe.Waiter.Command.Service\Cafe.Waiter.Command.Service.dll
```

You can see there are two sections to this Dockerfile, each starting with their own FROM statement. The top section focuses on building the DLLs. The bottom part is about creating a more lightweight runtime environment that doesn't contain all the build tools. Let's focus on the build environment for now. First, we use the Microsoft's dotnet 2.1 SDK image, to give us access to the dotnet tooling:

```
FROM microsoft/dotnet:2.1-sdk AS builder
```

We label this build environment "builder"- we'll reference that later when we want to copy the command service dlls into the final slimmer run-time image.

Then I copy in my Nuget.config file, which lists my .\\.nuget.local folder as a local nuget package source, for access to any of my own nuget packages that I might not be ready to publish yet. Then I copy that nuget package folder into the Docker build environment too:

```
COPY .\Nuget.config .\Nuget.config
COPY .\.nuget.local .\.nuget.local
```

#### Better Build Performance using Image Caching

After that, I copied the relevant *.csproj files first, followed by a dotnet restore:

```
COPY .\Cafe\Cafe.Waiter.Contracts\Cafe.Waiter.Contracts.csproj .\Cafe\Cafe.Waiter.Contracts\
RUN dotnet restore .\Cafe\Cafe.Waiter.Contracts
```

The corresponding folder only gets copied over in a later statement:

```
COPY .\Cafe\Cafe.Waiter.Contracts .\Cafe\Cafe.Waiter.Contracts
```

We copy the *.csproj files first for performance reasons - as the Dockerfile get processed, it creates layers of images, with each layer built on top of the last - there's one layer per Dockerfile command. These layers, or intermediate images, get cached for reuse when rebuilding an image, as long as there aren't any changes to the *.csproj files between rebuilds. **By copying the *.csproj files first, followed by the dotnet restore command, and only then copying the rest of the project contents, rebuilding a Docker image normally becomes a lot quicker - in the case where a rebuild is required because of changes to some .cs files, and the corresponding .csproj file hasn't changed, the intermediate image, created after the dotnet restore step on the previous build, will be reused, meaning that we don't have to wait for nuget packages to be re-fetched.**

Even though the code for my event-projecting service and command service are both directly under .\src\Cafe\, I copy the individual project folders needed for each service in each Dockerfile - this is for similar build performance reasons - if there was a step to copy the whole .\src\Cafe\ folder, then that COPY statement would break the cache frequently for both services, even though the changes might have only occurred in a project folder relating to one service.

#### Copying the appSettings.json file

So with all nuget dependencies restored, and all the .csproj and .cs files in place, there's only one more file to put in place. We need to copy the appSettings.docker.json file, that we generated earlier, into this Docker build environment. We'll rename it in the copy process to appSettings.json, as that's what the command service expects to find:

```
COPY .\Cafe\Cafe.Waiter.Command.Service\appSettings.docker.json .\Cafe\Cafe.Waiter.Command.Service\appSettings.json
```

#### Running Tests during Image Build

Then I copied over the test folder:

```
COPY .\Cafe\Cafe.Waiter.Domain.Tests\Cafe.Waiter.Domain.Tests.csproj .\Cafe\Cafe.Waiter.Domain.Tests\
RUN dotnet restore .\Cafe\Cafe.Waiter.Domain.Tests
...
COPY .\Cafe\Cafe.Waiter.Domain.Tests .\Cafe\Cafe.Waiter.Domain.Tests
RUN dotnet test .\Cafe\Cafe.Waiter.Domain.Tests\Cafe.Waiter.Domain.Tests.csproj
```

Here, in the last statement, I'm running the tests as part of the Docker image building process. We can do that because this particular test project doesn't interact with any other components - they're isolated from all infrastructure. *If any of the domain tests fail, the Docker image building process for the command service will fail, and the final Docker image for the command service will not be built until the tests are fixed.*

#### Publishing the Command Service

Finally, I published the command service artifacts (by default to app\\Cafe\\Cafe.Waiter.Command.Service\\bin\\Debug\\netcoreapp2.1\\publish):

```
RUN dotnet publish .\Cafe\Cafe.Waiter.Command.Service
```

#### Using Different Base Images for Build and Runtime Environments

At this point, the command service dlls are all built, and they're in the publish folder of the build environment. We don't have any more need for the build tools and the more heavy-weight base .NET Core SDK image. The final service image that will be used at run-time should be based on the more lightweight dotnet:2.1-runtime image. By keeping the container footprint small, we'll have better opportunities for scaling later. So I took advantage of Docker's multi-stage build feature, introducing the second FROM statement in the Dockerfile:

```
FROM microsoft/dotnet:2.1-runtime
WORKDIR /app
COPY --from=builder ["app\\Cafe\\Cafe.Waiter.Command.Service\\bin\\Debug\\netcoreapp2.1\\publish", ".\\Cafe.Waiter.Command.Service"]
CMD dotnet .\Cafe.Waiter.Command.Service\Cafe.Waiter.Command.Service.dll
```

Here I copied the contents of the publish folder from the final build environment image:

```
COPY --from=builder ["app\\Cafe\\Cafe.Waiter.Command.Service\\bin\\Debug\\netcoreapp2.1\\publish", ".\\Cafe.Waiter.Command.Service"]
```

Then I indicated that at container run-time, i.e. when a container instance is *actually started* from this run-time image, we want it to run the command service:

```
CMD dotnet .\Cafe.Waiter.Command.Service\Cafe.Waiter.Command.Service.dll
```

### Adding the .NET Core service to the docker-compose file

Because I needed access to the nuget.config and .nuget.local package folder in my Dockerfile, I set the context to the .\src folder instead of the .\src\Cafe\ folder. The Dockerfile path, as well as every relative path in the Dockerfile, is specified relative to this context.

```
cafe-waiter-command-service:
  build:
    context: .\src\
    dockerfile: .\Cafe\Cafe.Waiter.Command.Service\Dockerfile
  depends_on:
    - "rabbitmq"
    - "waiter-write-db-server"
```

You can see that we specify that the command service depends on the write model SQL Server and RabbitMQ - this ensures that those containers are started before the command service container.

### Connection Race Conditions

So at this point, I ran my tests again, but *this time, I didn't get the green light - my end-to-end test was failing.* Checking my running containers:

```
docker container ls --all
```

I could see that the command service had stopped unexpectedly, while all the other containers were still running. Looking at the logs:

```
docker logs [command service container id]
```

I could see that there was an exception connecting to the RabbitMQ server. But the RabbitMQ server was running - I was able to log into the RabbitMQ management console from the browser on my host. It took me a while to figure out what was going on - **Docker will start your containers in the order specified by the dependencies in your docker-compose file, but once a container is started, Docker assumes the service on that container is ready to use, and so it just starts the next containers in the dependency graph immediately.** In my case, the command service was trying to establish a RabbitMQ connection on start-up, but the RabbitMQ service within the container was still starting up even though the container it was in was running fine. And so the command service threw an exception, and failed - it had no way to receive commands to process. I never said this sample application was production-ready just yet! :) **So the solution here was for the command service to retry connection attempts to RabbitMQ until some retry limit has been reached.** It would wait a few seconds between failed attempts, and then try again. And it would give up finally if it reached some retry limit.

I had similar issues with SQL connections - the SQL Server wasn't always ready to receive connections, so I built a retry mechanism into that too - I made it so that connection strings were verified through connection attempts before being returned for wider use.

After putting these retry mechanisms in for SQL Server and RabbitMQ, I ran all my tests again. Finally everything passed again! It wasn't quite the baby step I would normally aim for, but it did highlight a genuine issue that needed resolving through the connection retry mechanism. With all my tests now passing again, including my end-to-end test, I moved onto migrating the event projecting service. That was very similar to the migration process for the command service, so I won't include it here. I had to include the connection retry mechanisms in the event-projecting service too.

So the last part of the sample application to migrate was the ASP.NET Core website.

### Part 5 - Migrating the ASP.NET Core Website

Even though it's a website as opposed to a console application, the Dockerfile for the Cafe.Waiter.Website followed exactly the same pattern outlined in the Dockerfile above for the command service and event projecting service, and so again, I won't include it here - it's all available to see in the [CQSplit repo](https://github.com/ronanmoriarty/CQSplit).

Again, I did have to introduce the same connection retry mechanism here as the Cafe.Waiter.Website connects to RabbitMQ to send commands, and it connects to the read model database server, as created by the event-projecting service.

### Next Time

So at this point, I've got my whole sample application running in Docker - the RabbitMQ server, the SQL Servers, my .NET Core services and the website. I've got tests in place that prove it all works. This was a *big* milestone for me - I could run the whole application with one [Cake](https://cakebuild.net/) task that used the docker-compose file. I could also run all the tests with one other Cake task. Job done, right? Well, not quite! The tests themselves were all still running on the host, and this meant that I couldn't just run my tests on any build agent yet - in the current state of the sample application, those build agents would still need to have the right build tools installed, e.g. .NET Core SDK. I'll deal with that next time!