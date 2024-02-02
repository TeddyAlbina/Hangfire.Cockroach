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
      PersistentJobQueueMock = new Mock<IPersistentJobQueue>();

      Mock<IPersistentJobQueueProvider> provider = new();
      provider.Setup(x => x.GetJobQueue())
        .Returns(PersistentJobQueueMock.Object);

      PersistentJobQueueProviderCollection = new PersistentJobQueueProviderCollection(provider.Object);

      _storageOptions = new CockroachStorageOptions {
        SchemaName = ConnectionUtils.GetSchemaName(),
        EnableTransactionScopeEnlistment = true,
      };
    }

    public Mock<IPersistentJobQueue> PersistentJobQueueMock { get; }

    public PersistentJobQueueProviderCollection PersistentJobQueueProviderCollection { get; }

    public CockroachStorage Storage { get; private set; }
    public NpgsqlConnection MainConnection => _mainConnection ?? (_mainConnection = ConnectionUtils.CreateConnection());

    public void Dispose()
    {
      _mainConnection?.Dispose();
      _mainConnection = null;
    }

    public void SetupOptions(Action<CockroachStorageOptions> storageOptionsConfigure)
    {
      storageOptionsConfigure(_storageOptions);
    }

    public CockroachStorage SafeInit(NpgsqlConnection connection = null)
    {
      return _initialized
        ? Storage
        : ForceInit(connection);
    }

    public CockroachStorage ForceInit(NpgsqlConnection connection = null)
    {
      Storage = new CockroachStorage(new ExistingNpgsqlConnectionFactory(connection ?? MainConnection, _storageOptions), _storageOptions) {
        QueueProviders = PersistentJobQueueProviderCollection,
      };
      _initialized = true;
      return Storage;
    }

    public void SafeInit(CockroachStorageOptions options,
      PersistentJobQueueProviderCollection jobQueueProviderCollection = null,
      NpgsqlConnection connection = null)
    {
      if (!_initialized)
      {
        ForceInit(options, jobQueueProviderCollection, connection);
        return;
      }

      Storage.QueueProviders = jobQueueProviderCollection;
    }

    public void ForceInit(CockroachStorageOptions options,
      PersistentJobQueueProviderCollection jobQueueProviderCollection = null,
      NpgsqlConnection connection = null)
    {
      Storage = new CockroachStorage(new ExistingNpgsqlConnectionFactory(connection ?? MainConnection, options), options) {
        QueueProviders = jobQueueProviderCollection,
      };
      _initialized = true;
    }
  }
}
