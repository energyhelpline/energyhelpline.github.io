---
layout: post
title: Migrating to Docker - Part 2 (Migrating Services)
author: Ronan Moriarty
excerpt: With my RabbitMQ and Sql Server instances now running in Docker, it was time to dockerize my .NET Core services and website.
---

## Background

Previously, I migrated the infrastructural parts of my CQSplit sample application to Docker.

## Part 4 - Move .NET Core Services to Docker

All the infrastructural components are in Docker now. That alone gives me a lot of benefit - other developers don't need to install RabbitMQ or Sql Server anymore. If I get the whole stack into Docker though, we'll simplify application startup further - new developers won't need to know about all the different bits before they start, and environments should become more manageable and get rebuilt more consistently.

So the next thing we need to consider is how different components will communicate between docker containers.

### Configuration for Communication Between Containers

Up to now, the containers haven't had to talk to each other - the communication has been from the .NET Core services and website on the host to RabbitMQ and Sql Server containers.

But now that we're moving the .NET services to containers, they'll need appSettings.json files that refer to RabbitMQ and Sql Server by the