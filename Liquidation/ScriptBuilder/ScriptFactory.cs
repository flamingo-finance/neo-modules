using System;
using System.Collections.Generic;
using System.Text;
using System.Numerics;
using Neo;
using Neo.VM;
using Neo.SmartContract;

namespace Liquidation
{
    public class ScriptFactory
    {
        public static byte[] IsSafeScriptBuilder(UInt160 account, UInt160 perpContract) 
        {
            byte[] script;
            using (var sb = new ScriptBuilder())
            {
                sb.EmitAppCall
                    (
                        perpContract,
                        "IsSafe",
                        new ContractParameter { Type = ContractParameterType.Hash160, Value = account.ToArray()}
                    );
                script = sb.ToArray();
            }
            return script;
        }

        public static byte[] GetAccountCashBalance(UInt160 account, UInt160 perpContract)  
        {
            byte[] script;
            using (var sb = new ScriptBuilder()) 
            {
                sb.EmitAppCall
                    (
                        perpContract,
                        "GetMarginAccountDetail",
                        new ContractParameter { Type = ContractParameterType.Hash160, Value = account.ToArray() },
                        new ContractParameter { Type = ContractParameterType.Integer, Value = BigInteger.Parse("0") }
                    );
                script = sb.ToArray();
            }
            return script;
        }
    }
}
