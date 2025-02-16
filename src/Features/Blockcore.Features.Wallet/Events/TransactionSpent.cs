﻿using Blockcore.Consensus.TransactionInfo;
using Blockcore.EventBus;

namespace Blockcore.Features.Wallet.Events
{
    /// <summary>
    /// Event that is executed when a transactions output is spent in the wallet.
    /// </summary>
    /// <seealso cref="Blockcore.EventBus.EventBase" />
    public class TransactionSpent : EventBase
    {
        public Transaction SpentTransaction { get; }

        public OutPoint SpentOutPoint { get; }

        public TransactionSpent(Transaction spentTransaction, OutPoint spentOutPoint)
        {
            this.SpentTransaction = spentTransaction;
            this.SpentOutPoint = spentOutPoint;
        }
    }
}