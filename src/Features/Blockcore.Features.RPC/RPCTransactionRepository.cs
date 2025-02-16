﻿using System;
using System.Threading.Tasks;
using Blockcore.Consensus.TransactionInfo;
using NBitcoin;

namespace Blockcore.Features.RPC
{
    public class RPCTransactionRepository : ITransactionRepository
    {
        private readonly RPCClient _Client;
        public RPCTransactionRepository(RPCClient client)
        {
            this._Client = client ?? throw new ArgumentNullException(nameof(client));
        }
        #region ITransactionRepository Members

        public Task<Transaction> GetAsync(uint256 txId)
        {
            return this._Client.GetRawTransactionAsync(txId, null, false);
        }

        public Task BroadcastAsync(Transaction tx)
        {
            return this._Client.SendRawTransactionAsync(tx);
        }

        public Task PutAsync(uint256 txId, Transaction tx)
        {
            return Task.FromResult(false);
        }

        #endregion
    }
}
