using Neo.Cryptography;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.VM;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Liquidation.TransactionHelper
{
    public class WalletHelper
    {
        public static KeyPair KeyPairFromWif(string wif)
        {
            var decodedWif = wif.Base58CheckDecode();
            if (decodedWif.Length != 34 || decodedWif[0] != 0x80 || decodedWif[33] != 0x01)
            {
                throw new Exception("Invalid wif");
            }
            KeyPair keyPair = new KeyPair(decodedWif.Skip(1).Take(32).ToArray());
            return keyPair;
        }

        public static KeyPair KeyPairFromNep2(string nep2Key, string passphrase)
        {
            var privateKey = Wallet.GetPrivateKeyFromNEP2(nep2Key, passphrase);
            KeyPair keyPair = new KeyPair(privateKey);
            return keyPair;
        }

        public static Witness CreateTransactionWitness(Transaction tx, KeyPair keyPair)
        {
            var signature = tx.Sign(keyPair);
            var sb = new ScriptBuilder();
            sb = sb.EmitPush(signature);
            var invocationScript = sb.ToArray();

            var verificationScript = Contract.CreateSignatureRedeemScript(keyPair.PublicKey);
            Witness witness = new Witness() { InvocationScript = invocationScript, VerificationScript = verificationScript };

            return witness;
        }
    }
}
