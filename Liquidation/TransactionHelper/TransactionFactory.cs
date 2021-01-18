using Neo;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.VM;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using RestSharp;

namespace Liquidation.TransactionHelper
{
    public static class TransactionFactory
    {
        public static string LiquidationTransaction(UInt160 perpContract, string liquidatorWIF, string liquidatorAddress, UInt160 trader)
        {
            KeyPair liquidator = WalletHelper.KeyPairFromWif(liquidatorWIF);
            byte[] script;
            using (var sb = new ScriptBuilder())
            {
                sb.EmitAppCall
                    (
                        perpContract,
                        "Liquidate",
                        new ContractParameter() { Type = ContractParameterType.Hash160, Value = trader },
                        new ContractParameter() { Type = ContractParameterType.Hash160, Value = liquidatorAddress.ToScriptHash() },
                        new ContractParameter() { Type = ContractParameterType.Integer, Value = BigInteger.Parse("999999999999999") }
                    );
                script = sb.ToArray();
            }
            Random random = new Random();
            var nonce = random.Next(int.MinValue, int.MaxValue);
            var nonceAttribute = Encoding.UTF8.GetBytes(nonce.ToString());
            InvocationTransaction tx = new InvocationTransaction
            {
                Version = 0,
                Script = script,
                Attributes = new TransactionAttribute[]
                {
                    new TransactionAttribute
                    {
                        Usage = TransactionAttributeUsage.Script, Data = liquidatorAddress.ToScriptHash().ToArray(),
                    },
                    new TransactionAttribute() { Usage = TransactionAttributeUsage.Remark, Data = nonceAttribute}
                },
                Witnesses = new Witness[0]
            };
            tx.Witnesses = new Witness[] { WalletHelper.CreateTransactionWitness(tx, liquidator) };
            Console.WriteLine(tx.Hash);
            return tx.ToArray().ToHexString();
        }

        public static string sendRawTransactionByUrl(this string rawTransaction, string Url)
        {
            var client = new RestClient(Url);
            client.Timeout = 3000;
            var request = new RestRequest(Method.POST);
            request.AddHeader("Content-Type", "application/json");
            string jsonTransaction = "{\r\n  \"jsonrpc\": \"2.0\",\r\n  \"method\": \"sendrawtransaction\",\r\n  \"params\": [\"" + rawTransaction + "\"],\r\n  \"id\": 1\r\n}";
            request.AddParameter("application/json", jsonTransaction, ParameterType.RequestBody);
            IRestResponse response = client.Execute(request);
            return response.Content;
        }

        public static bool LiquidationImplementation(UInt160 perpContract, string liquidatorWIF, string liquidatorAddress, UInt160 trader, string url)
        {
            string transactionResult = LiquidationTransaction(perpContract, liquidatorWIF, liquidatorAddress, trader).sendRawTransactionByUrl(url);
            if (transactionResult.Contains("true"))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public async static Task<bool> LiquidationAsyncImplementation(UInt160 perpContract, string liquidatorWIF, string liquidatorAddress, UInt160 trader, string url) 
        {
            try
            {
                await Task.Run(() =>
                {
                    string transactionResult = LiquidationTransaction(perpContract, liquidatorWIF, liquidatorAddress, trader).sendRawTransactionByUrl(url);
                    if (transactionResult.Contains("true"))
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                });
            }
            catch (Exception e) 
            {
                //补充log
            }
            return false;
        }

        public async static Task<bool> LiquidationByTask(UInt160 perpContract, string liquidatorWIF, string liquidatorAddress, UInt160 trader, string url) 
        {
            var liquidationTask = LiquidationAsyncImplementation(perpContract, liquidatorWIF, liquidatorAddress, trader, url);
            int timeOut = 10000;
            if (await Task.WhenAny(liquidationTask, Task.Delay(timeOut)) == liquidationTask)
            {
                var result = liquidationTask.Result;
                return result;
            }
            else 
            {
                //RPC超时, 返回false
                return false;
            }
        }
    }
}
