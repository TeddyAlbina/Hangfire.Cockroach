﻿using System;
using System.Globalization;
using System.Linq;

using Dapper;

using Hangfire.Cockroach.Tests.Utils;

using Npgsql;

using Xunit;

namespace Hangfire.Cockroach.Tests
{
    public class PostgreSqlInstallerFacts
    {
        [Fact]
        public void InstallingSchemaUpdatesVersionAndShouldNotThrowAnException()
        {
            Exception ex = Record.Exception(() =>
            {
                UseConnection(connection =>
                {
                    string schemaName = "hangfire_tests_" + Guid.NewGuid().ToString().Replace("-", "_").ToLower(CultureInfo.InvariantCulture);

                    CockroachObjectsInstaller.Install(connection, schemaName);

                    int lastVersion = connection.Query<int>($@"SELECT version FROM ""{schemaName}"".""schema""").Single();
                    Assert.Equal(21, lastVersion);

                    connection.Execute($@"DROP SCHEMA ""{schemaName}"" CASCADE;");
                });
            });

            Assert.Null(ex);
        }

        [Fact]
        public void InstallingSchemaWithCapitalsUpdatesVersionAndShouldNotThrowAnException()
        {
            Exception ex = Record.Exception(() =>
            {
                UseConnection(connection =>
                {
                    string schemaName = "Hangfire_Tests_" + Guid.NewGuid().ToString().Replace("-", "_").ToLower(CultureInfo.InvariantCulture);

                    CockroachObjectsInstaller.Install(connection, schemaName);

                    int lastVersion = connection.Query<int>($@"SELECT version FROM ""{schemaName}"".""schema""").Single();
                    Assert.Equal(21, lastVersion);

                    connection.Execute($@"DROP SCHEMA ""{schemaName}"" CASCADE;");
                });
            });

            Assert.Null(ex);
        }

        private static void UseConnection(Action<NpgsqlConnection> action)
        {
            using NpgsqlConnection connection = ConnectionUtils.CreateConnection();
            action(connection);
        }
    }
}
