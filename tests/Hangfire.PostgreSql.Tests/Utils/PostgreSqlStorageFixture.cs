using System;

using Hangfire.Cockroach.Factories;

using Moq;

using Npgsql;

namespace Hangfire.Cockroach.Tests.Utils
{
    public class PostgreSqlStorageFixture : IDisposable
    {
        private readonly CockroachStorageOptions _storageOptions;
        private bool _initialized;
        private NpgsqlConnection _mainConnection;

        public PostgreSqlStorageFixture()
        {
            this.PersistentJobQueueMock = new Mock<IPersistentJobQueue>();

            Mock<IPersistentJobQueueProvider> provider = new();
            provider.Setup(x => x.GetJobQueue())
              .Returns(this.PersistentJobQueueMock.Object);

            this.PersistentJobQueueProviderCollection = new PersistentJobQueueProviderCollection(provider.Object);

            this._storageOptions = new CockroachStorageOptions
            {
                SchemaName = ConnectionUtils.GetSchemaName(),
                EnableTransactionScopeEnlistment = true,
            };
        }

        public Mock<IPersistentJobQueue> PersistentJobQueueMock { get; }

        public PersistentJobQueueProviderCollection PersistentJobQueueProviderCollection { get; }

        public CockroachStorage Storage { get; private set; }
        public NpgsqlConnection MainConnection => this._mainConnection ?? (this._mainConnection = ConnectionUtils.CreateConnection());

        public void Dispose()
        {
            this._mainConnection?.Dispose();
            this._mainConnection = null;
        }

        public void SetupOptions(Action<CockroachStorageOptions> storageOptionsConfigure)
        {
            storageOptionsConfigure(this._storageOptions);
        }

        public CockroachStorage SafeInit(NpgsqlConnection connection = null)
        {
            return this._initialized
              ? this.Storage
              : this.ForceInit(connection);
        }

        public CockroachStorage ForceInit(NpgsqlConnection connection = null)
        {
            this.Storage = new CockroachStorage(new ExistingNpgsqlConnectionFactory(connection ?? this.MainConnection, this._storageOptions), this._storageOptions)
            {
                QueueProviders = this.PersistentJobQueueProviderCollection,
            };
            this._initialized = true;
            return this.Storage;
        }

        public void SafeInit(CockroachStorageOptions options,
          PersistentJobQueueProviderCollection jobQueueProviderCollection = null,
          NpgsqlConnection connection = null)
        {
            if (!this._initialized)
            {
                this.ForceInit(options, jobQueueProviderCollection, connection);
                return;
            }

            this.Storage.QueueProviders = jobQueueProviderCollection;
        }

        public void ForceInit(CockroachStorageOptions options,
          PersistentJobQueueProviderCollection jobQueueProviderCollection = null,
          NpgsqlConnection connection = null)
        {
            this.Storage = new CockroachStorage(new ExistingNpgsqlConnectionFactory(connection ?? this.MainConnection, options), options)
            {
                QueueProviders = jobQueueProviderCollection,
            };
            this._initialized = true;
        }
    }
}
