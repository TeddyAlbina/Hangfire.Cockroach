// This file is part of Hangfire.PostgreSql.
// Copyright © 2014 Frank Hommers <http://hmm.rs/Hangfire.PostgreSql>.
// 
// Hangfire.PostgreSql is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as 
// published by the Free Software Foundation, either version 3 
// of the License, or any later version.
// 
// Hangfire.PostgreSql  is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public 
// License along with Hangfire.PostgreSql. If not, see <http://www.gnu.org/licenses/>.
//
// This work is based on the work of Sergey Odinokov, author of 
// Hangfire. <http://hangfire.io/>
//   
//    Special thanks goes to him.

using System;

namespace Hangfire.Cockroach;

public static class CockroachBootstrapperConfigurationExtensions
{

    /// <summary>
    /// Tells the bootstrapper to use PostgreSQL as the job storage with the default storage options.
    /// </summary>
    /// <param name="configuration">Configuration instance.</param>
    /// <param name="configure">Bootstrapper configuration action.</param>
    /// <returns><see cref="IGlobalConfiguration{T}"/> instance whose generic type argument is <see cref="CockroachStorage"/>.</returns>
    public static IGlobalConfiguration<CockroachStorage> UseCockroachStorage(this IGlobalConfiguration configuration, Action<CockroachBootstrapperOptions> configure)
    {
        return configuration.UseCockroachStorage(configure, new CockroachStorageOptions());
    }

    /// <summary>
    /// Tells the bootstrapper to use PostgreSQL as the job storage with the specified storage options.
    /// </summary>
    /// <param name="configuration">Configuration instance.</param>
    /// <param name="configure">Bootstrapper configuration action.</param>
    /// <param name="options">Storage options.</param>
    /// <returns><see cref="IGlobalConfiguration{T}"/> instance whose generic type argument is <see cref="CockroachStorage"/>.</returns>
    /// <exception cref="InvalidOperationException">Throws if <see cref="IConnectionFactory"/> is not set up in the <paramref name="configure"/> action.</exception>
    public static IGlobalConfiguration<CockroachStorage> UseCockroachStorage(this IGlobalConfiguration configuration, Action<CockroachBootstrapperOptions> configure, CockroachStorageOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        CockroachBootstrapperOptions bootstrapperOptions = new(options);
        configure(bootstrapperOptions);

        var connectionFactory = bootstrapperOptions.ConnectionFactory;
        
        if (connectionFactory == null)
        {
            throw new InvalidOperationException("Connection factory is not specified");
        }

        CockroachStorage storage = new(connectionFactory, options);
        return configuration.UseStorage(storage);
    }
}
