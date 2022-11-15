using Blockcore.Consensus;

namespace Blockcore.Features.PoA
{
    /// <summary>Rules that might be thrown by consensus rules that are specific to PoA consensus.</summary>
    public static class PoAConsensusErrors
    {
        public static ConsensusError InvalidHeaderBits => new("invalid-header-bits", "invalid header bits");

        public static ConsensusError InvalidHeaderTimestamp => new("invalid-header-timestamp", "invalid header timestamp");

        public static ConsensusError InvalidHeaderSignature => new("invalid-header-signature", "invalid header signature");

        public static ConsensusError InvalidBlockSignature => new("invalid-block-signature", "invalid block signature");

        // Voting related errors.
        public static ConsensusError TooManyVotingOutputs => new("too-many-voting-outputs", "there could be only 1 voting output");

        public static ConsensusError VotingDataInvalidFormat => new("invalid-voting-data-format", "voting data format is invalid");

        // Collateral related errors.
        public static ConsensusError InvalidCollateralAmount => new("invalid-collateral-amount", "collateral requirement is not fulfilled");

        public static ConsensusError InvalidCollateralAmountNoCommitment => new("collateral-commitment-not-found", "collateral commitment not found");

        public static ConsensusError InvalidCollateralAmountCommitmentTooNew => new("collateral-commitment-too-new", "collateral commitment too new");
    }
}
