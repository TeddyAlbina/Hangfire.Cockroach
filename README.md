﻿# Hangfire.Cockroach

[![Build status](https://github.com/TeddyAlbina/Hangfire.Cockroach/actions/workflows/pack.yml/badge.svg)](https://github.com/TeddyAlbina/Hangfire.Cockroach/actions/workflows/pack.yml) [![GitHub release (latest by date)](https://img.shields.io/github/v/release/hangfire-cockroach/Hangfire.Cockroach?label=Release)](https://github.com/TeddyAlbina/Hangfire.Cockroach/releases/latest) [![Nuget](https://img.shields.io/nuget/v/Hangfire.Cockroach?label=NuGet)](https://www.nuget.org/packages/Hangfire.Cockroach)

This is an plugin to the Hangfire to enable CockroachDB as a storage system.
Read about hangfire here: https://github.com/HangfireIO/Hangfire#overview
and here: http://hangfire.io/


### Credits

👌👌👌 This library is based on the amazing work of Frank Hommers and his team at [Hangfire.PostgreSql](https://github.com/hangfire-postgres/).


### Differences with the Postgres provider

The database schema has been modified to fit the distributed nature of CockroachDB, primary keys are not serial but uuid, serialid has been added to certain tables.

🚧🚧 **The notification feature has been disabled**, i will try to provide an api to handle this scenario using [CockroachDB changefeed](https://www.cockroachlabs.com/docs/stable/create-changefeed).


⭕ All apis marked as **Obsolete** has been remove.


## Instructions

### For .NET

Install Hangfire, see https://github.com/HangfireIO/Hangfire#installation

Download all files from this repository, add the Hangfire.PostgreSql.csproj to your solution.
Reference it in your project, and you are ready to go by using:

```csharp
app.UseHangfireServer(new BackgroundJobServerOptions(),
  new CockroachStorage((options) => {});
app.UseHangfireDashboard();
```

### For ASP.NET Core

First, NuGet package needs installation.

- Hangfire.AspNetCore
- Hangfire.PostgreSql (Uses Npgsql 6)
- Hangfire.PostgreSql.Npgsql5 (Uses Npgsql 5)

Historically both packages were functionally the same up until the package version 1.9.11, the only difference was the underlying Npgsql dependency version. Afterwards, the support for Npgsql v5 has been dropped and now minimum required version is 6.0.0.

In `Startup.cs` _ConfigureServices(IServiceCollection services)_ method add the following line:

```csharp
services.AddHangfire(config =>
    config.UseCockroachStorage(c =>
        c.UseNpgsqlConnection(Configuration.GetConnectionString("HangfireConnection"))));
```

In Configure method, add these two lines:

```csharp
app.UseHangfireServer();
app.UseHangfireDashboard();
```

And... That's it. You are ready to go. Also there exists sample application [here](https://github.com/hangfire-postgres/Hangfire.PostgreSql/releases/download/1.4.8.1/aspnetcore_hangfire_sample.zip).

If you encounter any issues/bugs or have idea of a feature regarding Hangfire.Postgresql, [create us an issue](https://github.com/hangfire-postgres/Hangfire.PostgreSql/issues/new). Thanks!

### Enabling SSL support

SSL support can be enabled for Hangfire.PostgreSql library using the following mechanism:

```csharp
config.UseCockroachStorage(c =>
    c.UseNpgsqlConnection(
        Configuration.GetConnectionString("HangfireConnection"), // connection string,
        connection => // connection setup - gets called after instantiating the connection and before any calls to DB are made
        {
            connection.ProvideClientCertificatesCallback += clientCerts =>
            {
                clientCerts.Add(X509Certificate.CreateFromCertFile("[CERT_FILENAME]"));
            };
        }
    )
);
```
 
### Environment variables for unit testing

|  | Description |
|--|--|
|Hangfire_Cockroach_DatabaseName  | Database name |
| Hangfire_Cockroach_SchemaName | Schema name |
| Hangfire_Cockroach_ConnectionStringTemplate | Connection string  |


### License

Copyright © 2014-2023 Frank Hommers https://github.com/hangfire-postgres/Hangfire.PostgreSql.

Collaborators:
Frank Hommers (frankhommers), Vytautas Kasparavičius (vytautask), Žygimantas Arūna (azygis) and Andrew Armstrong (Plasma)

Contributors:
Burhan Irmikci (barhun), Zachary Sims(zsims), kgamecarter, Stafford Williams (staff0rd), briangweber, Viktor Svyatokha (ahydrax), Christopher Dresel (Dresel), Vincent Vrijburg, David Roth (davidroth) and Ivan Tiniakov (Tinyakov).

Hangfire.PostgreSql is an Open Source project licensed under the terms of the LGPLv3 license. Please see http://www.gnu.org/licenses/lgpl-3.0.html for license text or COPYING.LESSER file distributed with the source code.

This work is based on the work of Sergey Odinokov, author of Hangfire. <http://hangfire.io/>

### Related Projects

- [Hangfire.Core](https://github.com/HangfireIO/Hangfire)
