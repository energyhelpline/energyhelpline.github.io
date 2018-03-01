---
layout: post
title: Basic Docker Cheat Sheet
author: Ranu Miah
excerpt: Useful basic Docker commands
---

This is intended as a quick reference of Docker commands for a .Net Developer using Windows Container.
For more detail you can visit [Docker Docs](https://docs.docker.com/engine/reference/commandline/cli/#examples)

A simple analogy of a container and image. An image is like a hard drive of a computer, which contains information. A container is like a computer that needs the hard drive, which it will use. Another word a container is basically a running image.

### Docker Client

Some useful command regarding the client

`docker help` -- This will give you all the available commands. It provides a quick description of what each commands does.

`docker <COMMAND> --help` This provide more information or how to utilise a particular command.

`docker version` -- This will show what version of Docker installed on your machine as well as if your running a linux or windows container.

`docker info` -- This has lot more useful information like number of container and its state as well as the number of images.

### Docker Images

`docker images` -- List all the images locally downloaded.

`docker pull <IMAGE NAME>` -- If you do not have the image on your local machine then it will download the image from Docker hub. However it will not start a container.

`docker search <SEARCH TERM>` -- This is equivalent to vising [Docker Hub](https://hub.docker.com/).

### Docker Container

* `docker create <IMAGE NAME>` -- Create a container from an image without starting it.
* `docker start <CONTAINER ID>` -- Start up a container.
* `docker run <CONTAINER ID>` -- Creates and starts a container.
* `docker stop <CONTAINER ID>` -- Stop a container.
* `docker rename <CONTAINER NAME> <NEW NAME>` -- Change the name of the container.
* `docker attach <CONTAINER ID>` -- This will connect to a running container.
* `docker ps` -- This gives you a list of all the running containers.
* `docker ps -a` -- This will list all running and exited containers.

### Docker Run

If you wish to download and run then the following command will be more appropriate. `docker run --rm -it microsoft/dotnet:2.1-sdk dotnet --version`

The arguments are as follows:

* `--rm` -- It will delete the container and not the image when we exit the container.
* `-it` -- create an "interactive" session and attaches the tty input.
* `microsoft/dotnet` -- the name of the image.
* `2.1-sdk` -- This is the tag, which means what version of image to use.
* `dotnet --version` -- The command to execute in the container when it starts.

Other useful parameter you can use with `docker run`:

* `-d` -- Running the container in Detached mode.
* `--name <CONTAINER NAME>` -- This will give a name to the container instead of the default image name if omitted.
* `-v <HOST DIR>:<CONTAINER DIR>` -- Map the Host and Container folder.
*`-p <HOST PORT>:<CONTAINER PORT>` -- This is port forwarding from between Host and Container.

As of writing this blog on windows you can not use localhost:8080 but instead you will need to use the container ip address. This can be found using: `docker inspect <CONTAINER ID>`

Running the following command will change between Linux and Windows containers

    & 'C:\Program Files\Docker\Docker\DockerCli.exe' -SwitchDaemon

### Housekeeping

`docker system prune` -- This will remove all *unused* and *dangling* images, containers and build cache.

`docker rm -f $(docker ps -a -q)` This will delete all Docker containers.

`docker rmi -f $(docker images -q)`This will delete all Docker images.