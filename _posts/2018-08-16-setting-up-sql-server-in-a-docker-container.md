---
layout: post
title:  Setting up SQL Server in a Docker Windows Container
author: Ronan Moriarty
comments: true
excerpt: This post discuses how we created Sql Server Docker Images that wil create databases if they don't exist (ideal for testing scenarios), or attach databases if they do exist (as they would in Staging / Production environments). This post also demonstrates a simple migration mechanism used in the Docker images to migrate out-of-date schemas to the latest schema.
tags:
 - docker
 - rabbitmq
---

## Background - Implementing CQRS Pattern

I've got a long running [20% project at energyhelpline](https://github.com/ronanmoriarty/cqsplit), that started from a tutorial to implement the CQRS pattern. This pattern involves splitting data into two distinct models - a read model and a write model - in my case hosting each model in its own database.I decided to use SQL Server to store these read and write models. Originally I had these databases set up on a local SQL Express server, but over time I decided that I would move this, as well as all the other parts of the solution, into Docker. For a little bit more flexibility, I decided to use 2 separate SQL Server instances with a single database in each.

## Scenarios to Support

There was a few scenarios to consider :
*  when the database does not exist yet in a given environment
* when we want a database that can be disposed and recreated in a reproducible state for development /  testing purposes
* when we have an existing database with data that is not disposable, eg. in Production
* when we want to roll out new schema or data changes to any environment

After doing a bit of research, I decided to follow the general approach outlined by [Elton Stoneman](https://blog.sixeyed.com/windows-weekly-dockerfile-16-sql-server/). So I created a Dockerfile that mostly delegated to a PowerShell script on container startup. This PowerShell script would:
* create a database if none existed
* attach to an existing database if one could be found (making use of docker volumes)
* update the new or existing database to have the latest schema changes

**Whether the data persisted beyond the lifetime of the container depended on whether the database files were in a docker volume (ie. a folder that appears local within the container, but is actually attached to a folder on the Docker host computer) or not.** So in a Production environment, while the Sql Server database server instance was running within a Docker container, the database .mdf and .ldf files containing the actual data remained on the host.

## Migrating Database Schema

Elton used .dacpac files for the migration, but I took a slightly different approach. For simplicity, I just created a series of re-runnable SQL scripts, where each script would check a particular part of the schema and then make the necessary changes if they hadn't already been applied. My migration process would then simply run all the SQL scripts every time, whether the schema was up to date or not. My scripts were numbered so that they would run in the correct order every time.This approach probably wouldn't scale very well, but it works well for my current situation where I don't have too many scripts. This could be refined later quite easily to record the current migration in the database, similar to Entity Framework Migrations, so that only the newest scripts are applied.

## The Dockerfile

The docker file for my write model database server looked as follows:
```
#escape=`
FROM microsoft/mssql-server-windows-express
WORKDIR /app
ENV sa_password=_
ENV commandServicePassword=_
COPY .\WriteModel\Scripts .\scripts
COPY .\PowerShell .\powershell
EXPOSE 1433
CMD $parameters = @(\"commandServicePassword=$env:commandServicePassword\"); .\powershell\setup.ps1 -sa_password $env:sa_password -Database "Cafe.Waiter.WriteModel" -DatabaseFolder "C:\app\databases" -DatabaseScriptFolder "C:\app\scripts" -Parameters $env:parameters
```

### The Base Image

I've used a base SQL Server image so that I don't need to concern myself with the actual installation of SQL Server. I can just focus my energies on creating / attaching databases and updating schemas and data.

### Password Management using Environment Variables
I use SQL Server authentication as I'd heard about a few issues trying to get Windows Integrated authentication working over Docker. As you can see, no passwords are stored in the script - I'm storing passwords in environment variables. Environment variables can be resolved in a few different ways with Docker - in the Dockerfile above, defaults are set in the two ENV statements, but overriding values are passed in from a .env file on the host. The .env file itself is created using Setup.ps1 script in my repository root - this script prompts an administrator to choose passwords which are then written to the .env file, which is in the .gitignore file - *so no passwords are ever stored in source control.*

### SQL Scripts

As mentioned earlier, the SQL scripts are written to be re-runnable. This Dockerfile is ran with the context set to .\src\Cafe\Docker\SqlServer, so looking at the first COPY statement in the Dockerfile above, the SQL scripts are being copied from .\src\Cafe\Docker\SqlServer\WriteModel\Scripts on my host, and being copied to C:\app\scripts\ within the container (C:\app\ being the working directory set early in the dockerfile).

### PowerShell Scripts

Similarly to how the SQL script locations are resolved, the second COPY statement copies PowerShell scripts from .\src\Cafe\Docker\SqlServer\PowerShell\ on the host to C:\app\powershell\ in the container.

### Container Startup

The CMD statement sets up an array containing the command service password ultimately retrieved from the .env file in my repository root. This array holds all the SQLCMD parameters passed to every SQL script. In this case only the one script setting up the command service login needs the password, but it was simpler for now to just pass the same SQLCMD arguments to each script instead of figuring it which SQL scripts needed what - SQL scripts that don't need the SQLCMD arguments supplied just ignore them.

### Issues with Single Quotes

I struggled trying to put single quotes in the Dockerfile around the environment variable values as required by PowerShell - Docker kept removing them, making it invalid PowerShell syntax - I got around this by including the single quotes within the actual environment variable values in the .env file. This bit is definitely a bit hacky - I'm open to suggestions for a cleaner way to get around this particular problem! :)

## The PowerShell Setup Scripts

The .\powershell\setup.ps1 script ran from the CMD statement above looks as follows:

```
[CmdletBinding()]
Param(
[Parameter(Mandatory=$true)]
[string]$sa_password,
[Parameter(Mandatory=$true)]
[string]$Database,
[Parameter(Mandatory=$true)]
[string]$DatabaseFolder,
[Parameter(Mandatory=$true)]
[string]$DatabaseScriptFolder,
[string[]]$parameters
)

& $PSScriptRoot\setupDatabase.ps1 $Database $DatabaseFolder $DatabaseScriptFolder $parameters
& C:\start.ps1 -sa_password $sa_password -ACCEPT_EULA Y
```

My setupDatabase.ps1 script in the same folder follows the general outline mentioned earlier to create a database if need be, or attach to an existing database, and run the migration scripts. Here we're passing in:
* the database name to search for in the database server. This also doubles as the name of the .mdf and .ldf files.
* the location to check for .mdf and .ldf files
* the location of the SQL scripts, and
* the SQLCMD parameters

The migration SQL scripts run using Invoke-SqlCmd, with the parameters array passed through as SqlCmd arguments.

With the databases set up, the start.ps1 included from Microsoft's base SQL Server docker image is ran to set the SA password (ultimately taken from the .env file in my repository root) and accept the licensing agreement.

### The Database Setup Script

The script below implements the general pattern outlined in Elton Stoneman's article:
* Look for database files with a given name in a particular folder
    - this folder can map to a directory on your host as a docker volume, allowing data to be persisted beyond the lifetime of the container, and reattached next time.
* If no database files are found, create a new database using the same file paths searched for above. If the folder is a docker volume, the files will be on your host available for reuse. Otherwise they'll be discarded when the container shuts down.
* Whether it's a new or existing database, run the migration to ensure the schema is up to date.

Here's the setup-database script:

```
[CmdletBinding()]
Param(
    [Parameter(Mandatory=$true)]
    [string]$dbName,
    [Parameter(Mandatory=$true)]
    [string]$dbFolder,
    [Parameter(Mandatory=$true)]
    [string]$dbScriptsFolder,
    [string[]] $parameters
)

. "$PSScriptRoot\attachDatabase.ps1"

function CreateNewDatabase () {
    if(!(Test-Path $dbFolder))
    {
        mkdir $dbFolder
    }

    try
    {
        Write-Host "Creating new database $dbName..."
        $server = GetLocalSqlServer
        $database = New-Object -TypeName Microsoft.SqlServer.Management.Smo.Database -ArgumentList $server, $dbName
        $database.DatabaseOptions.AutoShrink = $true
        $primaryFileGroup = New-Object -TypeName Microsoft.SqlServer.Management.Smo.FileGroup -ArgumentList $database, 'PRIMARY'
        $database.FileGroups.Add($primaryFileGroup)
        $mdfFilePath = GetMdfFilePath $dbFolder $dbName
        $dataFile = New-Object -TypeName Microsoft.SqlServer.Management.Smo.DataFile -ArgumentList $primaryFileGroup, "$($dbName)_Data", $mdfFilePath
        $primaryFileGroup.Files.Add($dataFile)
        $database.Create()
        Get-ChildItem $dbFolder
    }
    catch [Exception]
    {
        Write-Host $_.Exception|format-list -force
    }
}

function DatabaseFilesExist ()
{
    $mdfFilePath = GetMdfFilePath $dbFolder $dbName
    $ldfFilePath = GetLdfFilePath $dbFolder $dbName
    $dbFilesExist = (Test-Path ($mdfFilePath)) -and (Test-Path ($ldfFilePath))
    if($dbFilesExist)
    {
        Write-Host "Files $mdfFilePath and $ldfFilePath found for $dbName database."
    }
    else
    {
        Write-Host "No files found for $dbName database."
    }

    return $dbFilesExist
}

function DatabaseExists()
{
    return ((GetLocalSqlServer).Databases[$dbName]) -ne $null
}

function EnsureDatabaseExists()
{
    if(-Not (DatabaseExists))
    {
        Write-Host "$dbName database not found."
        if(DatabaseFilesExist)
        {
            AttachExistingDatabase $dbFolder $dbName
        }
        else
        {
            CreateNewDatabase
        }
    }
    else
    {
        Write-Host "$dbName already attached."
    }
}

function ApplyDatabaseMigrations()
{
    Write-Host "Applying scripts from $dbScriptsFolder..."
    Get-ChildItem $dbScriptsFolder | Sort-Object | ForEach-Object `
    {
        Write-Host "Applying $($_.FullName)..."
        Invoke-SqlCmd -InputFile $_.FullName -ServerInstance "." -Database $dbName -Variable $parameters
    }
    Write-Host "Finished applying scripts for $dbName"
}

EnsureDatabaseExists
ApplyDatabaseMigrations
```

### The Attach-Database Script

The above script references a separate script to attach existing databases. The attach-database script uses the Microsoft.SqlServer.Smo library to manipulate database server and database objects:

```
[reflection.assembly]::LoadWithPartialName("Microsoft.SqlServer.Smo")
function GetMdfFilePath($databaseFolder, $databaseName)
{
    return "$databaseFolder\$databaseName.mdf"
}

function GetLdfFilePath($databaseFolder, $databaseName)
{
    return "$databaseFolder\$($databaseName)_log.ldf"
}

function GetLocalSqlServer()
{
    return new-object Microsoft.SqlServer.Management.Smo.Server -ArgumentList "."
}

function AttachExistingDatabase ($databaseFolder, $databaseName) {
    Write-Host "Attaching database $databaseName..."
    $dataFiles = New-Object System.Collections.Specialized.StringCollection
    $dataFiles.Add((GetMdfFilePath $databaseFolder $databaseName))
    $dataFiles.Add((GetLdfFilePath $databaseFolder $databaseName))
    $server = GetLocalSqlServer
    $server.AttachDatabase($databaseName, $dataFiles)
    Write-Host $server.Databases
}
```

## Conclusion

The Dockerfile and related PowerShell scripts above build a flexible SQL Server image that can use disposable databases for maximum repeatability in dev / test environments, while also attaching to existing databases for Staging or Production. In all cases, databases are migrated to the latest schema using a simple mechanism involving re-runnable scripts.

## Next Steps

There's only a small amount in the above Dockerfile that tailors it to my particular database - the environment variables, and the corresponding parameters array in the CMD statement. I'll look to refactor this shortly so that the above file can be uploaded to Docker Hub for general use.

In follow up posts, I'll look at how I migrated my .NET Core services to Docker.
