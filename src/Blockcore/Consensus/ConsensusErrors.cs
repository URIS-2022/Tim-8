namespace Blockcore.Consensus
{
    /// <summary>
    /// A class that holds consensus errors.
    /// </summary>
    public static class ConsensusErrors
    {
        public static ConsensusError InvalidPrevTip => new("invalid-prev-tip", "invalid previous tip");

        public static ConsensusError HighHash => new("high-hash", "proof of work failed");

        public static ConsensusError BadCoinbaseHeight => new("bad-cb-height", "block height mismatch in coinbase");

        public static ConsensusError BadTransactionNonFinal => new("bad-txns-nonfinal", "non-final transaction");

        public static ConsensusError BadWitnessNonceSize => new("bad-witness-nonce-size", "invalid witness nonce size");

        public static ConsensusError BadWitnessMerkleMatch => new("bad-witness-merkle-match", "witness merkle commitment mismatch");

        public static ConsensusError UnexpectedWitness => new("unexpected-witness", "unexpected witness data found");

        public static ConsensusError BadBlockWeight => new("bad-blk-weight", "weight limit failed");

        public static ConsensusError BadDiffBits => new("bad-diffbits", "incorrect proof of work");

        public static ConsensusError TimeTooOld => new("time-too-old", "block's timestamp is too early");

        public static ConsensusError TimeTooNew => new("time-too-new", "timestamp too far in the future");

        public static ConsensusError BadVersion => new("bad-version", "block version rejected");

        public static ConsensusError BadMerkleRoot => new("bad-txnmrklroot", "hashMerkleRoot mismatch");

        public static ConsensusError BadBlockLength => new("bad-blk-length", "size limits failed");

        public static ConsensusError BadCoinbaseMissing => new("bad-cb-missing", "first tx is not coinbase");

        public static ConsensusError BadCoinbaseSize => new("bad-cb-length", "invalid coinbase size");

        public static ConsensusError BadMultipleCoinbase => new("bad-cb-multiple", "more than one coinbase");

        public static ConsensusError BadMultipleCoinstake => new("bad-cs-multiple", "more than one coinstake");

        public static ConsensusError BadBlockSigOps => new("bad-blk-sigops", "out-of-bounds SigOpCount");

        public static ConsensusError BadTransactionDuplicate => new("bad-txns-duplicate", "duplicate transaction");

        public static ConsensusError BadTransactionNoInput => new("bad-txns-vin-empty", "no input in the transaction");

        public static ConsensusError BadTransactionNoOutput => new("bad-txns-vout-empty", "no output in the transaction");

        public static ConsensusError BadTransactionOversize => new("bad-txns-oversize", "oversized transaction");

        public static ConsensusError BadTransactionEmptyOutput => new("user-txout-empty", "user transaction output is empty");

        public static ConsensusError BadTransactionNegativeOutput => new("bad-txns-vout-negative", "the transaction contains a negative value output");

        public static ConsensusError BadTransactionTooLargeOutput => new("bad-txns-vout-toolarge", "the transaction contains a too large value output");

        public static ConsensusError BadTransactionTooLargeTotalOutput => new("bad-txns-txouttotal-toolarge", "the sum of outputs'value is too large for this transaction");

        public static ConsensusError BadTransactionDuplicateInputs => new("bad-txns-inputs-duplicate", "duplicate inputs");

        public static ConsensusError BadTransactionNullPrevout => new("bad-txns-prevout-null", "this transaction contains a null prevout");

        public static ConsensusError BadTransactionBIP30 => new("bad-txns-BIP30", "tried to overwrite transaction");

        public static ConsensusError BadTransactionMissingInput => new("bad-txns-inputs-missingorspent", "input missing/spent");

        public static ConsensusError BadCoinbaseAmount => new("bad-cb-amount", "coinbase pays too much");

        public static ConsensusError BadCoinstakeAmount => new("bad-cs-amount", "coinstake pays too much");

        public static ConsensusError BadTransactionPrematureCoinbaseSpending => new("bad-txns-premature-spend-of-coinbase", "tried to spend coinbase before maturity");

        public static ConsensusError BadTransactionPrematureCoinstakeSpending => new("bad-txns-premature-spend-of-coinstake", "tried to spend coinstake before maturity");

        public static ConsensusError BadTransactionInputValueOutOfRange => new("bad-txns-inputvalues-outofrange", "input value out of range");

        public static ConsensusError BadTransactionInBelowOut => new("bad-txns-in-belowout", "input value below output value");

        public static ConsensusError BadTransactionNegativeFee => new("bad-txns-fee-negative", "negative fee");

        public static ConsensusError BadTransactionFeeOutOfRange => new("bad-txns-fee-outofrange", "fee out of range");

        public static ConsensusError BadTransactionEarlyTimestamp => new("bad-txns-early-timestamp", "timestamp earlier than input");

        public static ConsensusError BadTransactionScriptError => new("bad-txns-script-failed", "a script failed");

        public static ConsensusError NonCoinstake => new("non-coinstake", "non-coinstake");

        public static ConsensusError ReadTxPrevFailed => new("read-txPrev-failed", "read txPrev failed");

        public static ConsensusError ReadTxPrevFailedInsufficient => new("read-txPrev-failed-insufficient", "read txPrev failed insufficient information");

        public static ConsensusError InvalidStakeDepth => new("invalid-stake-depth", "tried to stake at depth");

        public static ConsensusError StakeTimeViolation => new("stake-time-violation", "stake time violation");

        public static ConsensusError BadStakeBlock => new("bad-stake-block", "bad stake block");

        public static ConsensusError PrevStakeNull => new("prev-stake-null", "previous stake is not found");

        public static ConsensusError StakeHashInvalidTarget => new("proof-of-stake-hash-invalid-target", "proof-of-stake hash did not meet target protocol");

        public static ConsensusError EmptyCoinstake => new("empty-coinstake", "empty-coinstake");

        public static ConsensusError ModifierNotFound => new("modifier-not-found", "unable to get last modifier");

        public static ConsensusError FailedSelectBlock => new("failed-select-block", "unable to select block at round");

        public static ConsensusError SetStakeEntropyBitFailed => new("set-stake-entropy-bit-failed", "failed to set stake entropy bit");

        public static ConsensusError CoinstakeVerifySignatureFailed => new("verify-signature-failed-on-coinstake", "verify signature failed on coinstake");

        public static ConsensusError BlockTimestampTooFar => new("block-timestamp-to-far", "block timestamp too far in the future");

        public static ConsensusError BlockTimestampTooEarly => new("block-timestamp-to-early", "block timestamp too early");

        public static ConsensusError BadBlockSignature => new("bad-block-signature", "bad block signature");

        public static ConsensusError BlockTimeBeforeTrx => new("block-time-before-trx", "block timestamp earlier than transaction timestamp");

        public static ConsensusError ProofOfWorkTooHigh => new("proof-of-work-too-high", "proof of work too high");

        public static ConsensusError CheckpointViolation => new("checkpoint-violation", "block header hash does not match the checkpointed value");

        // Proven header validation errors.
        public static ConsensusError BadProvenHeaderMerkleProofSize => new("proven-header-merkle-proof-size", "proven header's merkle proof size must be less than 512 bytes");

        public static ConsensusError BadProvenHeaderCoinstakeSize => new("proven-header-coinstake-size", "proven header's coinstake size must be less than 1,000,000 bytes");

        public static ConsensusError BadProvenHeaderSignatureSize => new("proven-header-signature-size", "proven header's signature size must be less than 80 bytes");

        public static ConsensusError BadTransactionCoinstakeSpending => new("bad-txns-spend-of-coinstake", "coinstake is already spent");

        public static ConsensusError UtxoNotFoundInRewindData => new("utxo-not-found-in-rewind-data", "utxo not found in rewind data");

        public static ConsensusError InvalidPreviousProvenHeader => new("proven-header-invalid-previous-header", "previous header in chain is expected to be of proven header type");

        public static ConsensusError InvalidPreviousProvenHeaderStakeModifier => new("proven-header-invalid-previous-header-stake-modifier", "previous proven header's StakeModifier is null");

        public static ConsensusError BadColdstakeAmount => new("bad-coldstake-amount", "coldstake is negative");

        public static ConsensusError BadColdstakeInputs => new("bad-coldstake-inputs", "coldstake inputs contain mismatching scriptpubkeys");

        public static ConsensusError BadColdstakeOutputs => new("bad-coldstake-outputs", "coldstake outputs contain unexpected scriptpubkeys");
    }
}
