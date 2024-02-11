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
using System.Collections;
using System.Collections.Generic;

namespace Hangfire.Cockroach;

public sealed class PersistentJobQueueProviderCollection : IEnumerable<IPersistentJobQueueProvider>
{
    private readonly IPersistentJobQueueProvider defaultProvider;

    private readonly List<IPersistentJobQueueProvider> providers = [];

    private readonly Dictionary<string, IPersistentJobQueueProvider> _providersByQueue = new(StringComparer.OrdinalIgnoreCase);

    public PersistentJobQueueProviderCollection(IPersistentJobQueueProvider defaultProvider)
    {
        this.defaultProvider = defaultProvider ?? throw new ArgumentNullException(nameof(defaultProvider));
        this.providers.Add(this.defaultProvider);
    }

    public IEnumerator<IPersistentJobQueueProvider> GetEnumerator()
    {
        return this.providers.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return this.GetEnumerator();
    }

    public void Add(IPersistentJobQueueProvider provider, IEnumerable<string> queues)
    {
        if (provider == null)
        {
            throw new ArgumentNullException(nameof(provider));
        }

        if (queues == null)
        {
            throw new ArgumentNullException(nameof(queues));
        }

        this.providers.Add(provider);

        foreach (var queue in queues)
        {
            this._providersByQueue.Add(queue, provider);
        }
    }

    public IPersistentJobQueueProvider GetProvider(string queue)
    {
        return this._providersByQueue.TryGetValue(queue, out var provider)
          ? provider
          : this.defaultProvider;
    }

    public void Remove(string queue)
    {
        if (!this._providersByQueue.ContainsKey(queue))
        {
            return;
        }

        var provider = this._providersByQueue[queue];
        this._providersByQueue.Remove(queue);
        this.providers.Remove(provider);
    }
}
