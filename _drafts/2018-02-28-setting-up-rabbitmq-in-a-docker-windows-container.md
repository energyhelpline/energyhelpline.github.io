---
layout: post
title:  "Setting up RabbitMQ in a Docker Windows Container"
author: Ronan Moriarty
comments: true
excerpt: Recently I've been using my 20% time to get up to speed on Docker - specifically Docker for Windows. I've used another of my other 20% projects, a project implementing the CQRS pattern, as a testing ground to get familiar with the concepts within the context of trying to solve a particular problem I've been having.
tags:
 - docker
 - rabbitmq
---

### Background

Recently I've been using my weekly 20% time to get up to speed on Docker - specifically Docker for Windows. I have [another 20% project](http://github.com/ronanmoriarty/cqrs-nu-tutorial) that has a dependency on RabbitMQ, so I thought it would be nice to be able to leverage Docker to make setup as easy as possible. I ran into a few issues along the way, so I thought I'd share my findings to try to help others avoid the pitfalls that caught me out.

### RabbitMQ on Docker Hub

**Unfortunately, at the moment, the official RabbitMQ containers are all Linux-based**, so using a windows container for RabbitMQ wasn't going to be as simple as just pulling a suitable image from Docker Hub. There were a few other people on Docker Hub who had created Windows containers for RabbitMQ, but I didn't want to rely on something that wasn't being officially supported by Pivotal, and also part of this for me was just about learning Docker and how to build my own custom images, so I decided I'd see if I could create my own RabbitMQ Docker image for windows containers.

I'm new to Docker, so after trying out parts 1-3 of [Docker's getting-started tutorial](https://docs.docker.com/get-started/), I decided I had just about enough knowledge to try Docker out my own project.

### First Attempt - Using Chocolatey

I find [Chocolatey](https://chocolatey.org/) very easy to install most tools on my workstation normally, so I felt it might be quite simple to lean on that when building a new Docker image. To start with, I needed Chocolatey itself installed in the Docker image though. As I expected to be able to use Chocolatey for various Docker images later, it seemed to make sense to create a base Docker image with Chocolatey installed, and then that image could be used as the starting point for other Docker files. 

#### Creating the Base Chocolatey Image

So I created a Docker file similar to the one that follows:

```docker
FROM microsoft/windowsservercore
SHELL [ "powershell", "-command"]
RUN Invoke-Expression(Invoke-WebRequest 'https://chocolatey.org/install.ps1' -UseBasicParsing | Select-Object -Expand Content)
```

I published it to Docker Hub as ronanmoriarty/chocolatey:v1, and then set that as my base image for my RabbitMQ Docker file instead of microsoft/windowsservercore, as follows:

```docker
FROM ronanmoriarty/chocolatey:v1
RUN choco install rabbitmq -y
```

Then running

```powershell
docker build -t rabbitmq .
```

installed Erlang first and then RabbitMQ, and it built the RabbitMQ image with no errors. So then I decided to run a container (interactively) using this new image:

```powershell
docker run -it rabbitmq
```

I hadn't appended the RabbitMQ path on to the PATH environment variable in the Docker file yet, so on the container command line, I just navigated to the default RabbitMQ installation folder, then into the sbin subdirectory, and I ran:

```powershell
rabbitmqctl status
```

**And this was where I started getting issues with the Chocolatey approach. This gave the following error:**

```
Hostname mismatch: node "rabbit@be3fe5c4e551" believes its host is different. Please ensure that hostnames resolve the same way locally and on "rabbit@be3fe5c4e551"
```

It seemed the RabbitMQ node name wasn't as expected. This was when I realised a little bit more about how the Docker image build process works. There's the base image layer in the FROM command, and then each build step afterwards in the Docker file is ran in a separate image, layered on top. At each step along the way, these intermediate images have different computer names. So RabbitMQ was getting installed in an image with one computer name, and then when we ran a container, the container has a completely different computer name. So **the RabbitMQ configuration files in the container contained entries based on the computer name of the intermediate image when building the image.**

### Attempt 2 - Install RabbitMQ without using Chocolatey

So instead of downloading *and* installing RabbitMQ at build stage, I could just download the installer and unzip etc. at image build time, but *delay the execution of any of the downloaded installation batch files until the container starts*. But you don't have that level of control with the RabbitMQ chocolatey package - when it runs, it just downloads the installer, and installs RabbitMQ as a service there and then. So I decided I couldn't use Chocolatey to install RabbitMQ.

Looking at the [steps to manually install RabbitMQ on Windows](https://www.rabbitmq.com/install-windows-manual.html), I changed my Dockerfile to download the Erlang installer first, then the RabbitMQ zip file. I could have used Chocolatey for installing Erlang, but as it was just the one package that I could install with Chocolatey now, I didn't see much value in using Chocolatey any more. So my Docker file evolved as follows:

```docker
FROM microsoft/windowsservercore
ENV ERLANG_HOME="c:\\erlang"
SHELL [ "powershell", "-command"]
RUN Invoke-WebRequest -Uri "http://erlang.org/download/otp_win64_20.2.exe" -OutFile "c:\\erlang_install.exe" ; \
        Start-Process -Wait -FilePath "c:\\erlang_install.exe" -ArgumentList /S, /D=$env:ERLANG_HOME ; \
        Remove-Item -Force -Path "C:\\erlang_install.exe" ;
ARG RABBITMQ_VERSION=3.6.15
ENV RABBITMQ_VERSION=${RABBITMQ_VERSION}
RUN Invoke-WebRequest -Uri "https://www.rabbitmq.com/releases/rabbitmq-server/v$env:RABBITMQ_VERSION/rabbitmq-server-windows-$env:RABBITMQ_VERSION.zip" -OutFile "c:\\rabbitmq.zip"; \
        Expand-Archive -Path "c:\\rabbitmq.zip" -DestinationPath "c:\\" ; \
        Remove-Item -Force -Path "c:\\rabbitmq.zip" ;
ENV RABBITMQ_SERVER=C:\\rabbitmq_server-${RABBITMQ_VERSION}
RUN $path = [Environment]::GetEnvironmentVariable('Path', 'Machine'); \
    [Environment]::SetEnvironmentVariable('Path', $path + ';C:\rabbitmq_server-' + $env:RABBITMQ_VERSION + '\sbin', 'Machine')
RUN rabbitmq-plugins enable rabbitmq_management --offline
COPY rabbitmq.config C:\\Users\\ContainerAdministrator\\AppData\\Roaming\\RabbitMQ\\rabbitmq.config
ENTRYPOINT rabbitmq-server
EXPOSE 5672 15672
```

These steps were largely driven by the manual-install steps link above. 

#### Updating the PATH to Include RabbitMQ

I set an environment variable so RabbitMQ knows where to find Erlang. I added the RABBITMQ_SERVER environment variable pointing at the base RabbitMQ directory, and appended the base RabbitMQ directory to the PATH environment variable at the machine level so that I could easily run RabbitMQ commands.

#### Enabling the Management Portal

Then I enabled the rabbitmq_management plugin so that I could use the RabbitMQ portal. The --offline flag ensures that it doesn't try to communicate with the RabbitMQ process just yet - remember we're not actually starting the RabbitMQ application until the container starts - we're still in the build stage here.

#### Configuring the Portal to Allow Remote Access by Guest Account

When I originally tried to access the RabbitMQ portal from my host, I found that the guest user was not available when logging on remotely. So as a temporary measure just to get some quick feedback, I created a config file as follows:

```
[{rabbit, [{loopback_users, []}]}].
```

and placed it alongside my Docker file so that I could just copy it over as another build step in my Docker file. The longer-term fix here is to add new users rather than easing security around the guest user.

The config file format above can apparently be specified more easily as a .conf file using the much simpler sysctl format, but I got various errors trying to get that to work, and didn't consider it a big issue, so I left it in the format above.

#### Installing the RabbitMQ Service

Then, I set the command to run when the container starts, ie. the rabbitmq_server installation batch file, and listed the ports that need to be exposed.

### Accessing the RabbitMQ Portal from the Host

With the Docker file building the image as intended now, I could run a container based on that image now:

```
docker run  --rm --name rabbitmqtest -p 15672:15672 -p 5672:5672 -t rabbitmq
```

Once the console in the container confirmed that the container was up and running, in a separate console window I ran the following:
```
docker inspect  rabbitmqtest

```

The output of this gave me the IP address of the container that we can then use in a browser on the host:
http://[container-IP-address]:15672

This showed the RabbitMQ portal. Logging in as "guest" with password "guest" loaded the dashboard.


To test this out properly, running my [CQRSTutorial project](http://github.com/ronanmoriarty/cqrs-nu-tutorial) from my host, I (temporarily) updated its configuration to point at this RabbitMQ server on my container, and took an action that would send a command to Rabbit. The portal shows that the appropriate exchange got created ok and the command message is received:

![RabbitMQ Management Portal]({{ "/images/cafe.waiter.command.service-exchange.png" | absolute_url }})

### Conclusion

I've learnt quite a lot from this exercise. I've found Docker command line for building images / publishing images, running containers etc. to be very intuitive so far. However I've found the Dockerfile syntax to be a lot more complex - there was a lot of trial and error around escaping backslashes and getting powershell variables / environment variables to evaluate instead of being treated as literal strings - **the key thing I found when working with Docker files is to have a very quick feedback loop so that you can try lots of different things in a very short space of time.** To help with this, I've found when creating Docker files, try to structure your Dockerfile so that the longer and more stable processes happen as early as possible in the Dockerfile - **every time you change a command in a Dockerfile, each build step after that is rebuilt - this means that Docker can't use previously cached intermediate images, and your feedback loop increases significantly**, and it takes a lot longer to recover from your mistakes.

Obviously it would have been better if I didn't have to write the Docker file at all - that's the price I've had to pay by limiting myself to windows containers for now - there's various RabbitMQ images on Docker Hub, but they're all linux-based at the moment. Hopefully they'll fix that soon.
