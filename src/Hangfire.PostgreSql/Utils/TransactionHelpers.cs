using System;
using System.Transactions;

namespace Hangfire.Cockroach.Utils;

public static class TransactionHelpers
{
    internal static TransactionScope CreateTransactionScope(IsolationLevel? isolationLevel = IsolationLevel.ReadCommitted, bool enlist = true, TimeSpan? timeout = null)
    {
        var scopeOption = TransactionScopeOption.RequiresNew;
        if (enlist)
        {
            var currentTransaction = Transaction.Current;

            if (currentTransaction != null)
            {
                isolationLevel = currentTransaction.IsolationLevel;
                scopeOption = TransactionScopeOption.Required;
            }
        }

        return new TransactionScope(
          scopeOption,
          new TransactionOptions
          {
              IsolationLevel = isolationLevel.GetValueOrDefault(IsolationLevel.ReadCommitted),
              Timeout = timeout.GetValueOrDefault(TransactionManager.DefaultTimeout),
          });
    }
}
