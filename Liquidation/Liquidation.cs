using Liquidation;
using Microsoft.AspNetCore.Http;
using Neo.IO.Caching;
using Neo.IO.Data.LevelDB;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.Persistence;
using Neo.Persistence.LevelDB;
using Neo.SmartContract;
using Neo.VM;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Liquidation.TransactionHelper;
using Snapshot = Neo.Persistence.Snapshot;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;

namespace Neo.Plugins
{
    public class Liquidation : Plugin, IPersistencePlugin
    {
        private List<UInt160> Accounts;
        private List<UInt160> unsafeAccounts = new List<UInt160>();
        private UInt160 perpContract;
        private string liquidatorWIF;
        private string liquidatorAddress;
        private string url;
        private string FileName;

        private static readonly Fixed8 maxGas = Fixed8.FromDecimal(1m);
        public override void Configure()
        {
            var config = GetConfiguration();
            var perp = config.GetValue<string>("PerpContract");
            PerpSettings.Default = new PerpSettings()
            {
                PerpContract = UInt160.Parse(perp)
            };
        }

        public Liquidation() 
        {
            if (File.Exists(FileName)) 
            {
                Accounts = JsonConvert.DeserializeObject<List<UInt160>>(File.ReadAllText(FileName));
            }
        }

        public void OnCommit(Snapshot snapshot)
        {
        }

        public void OnPersist(Snapshot snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            foreach (Blockchain.ApplicationExecuted appExecuted in applicationExecutedList) 
            {
                foreach (var executionResults in appExecuted.ExecutionResults) 
                {
                    if (executionResults.VMState.HasFlag(VMState.FAULT)) continue;
                    foreach (var notifyEventArgs in executionResults.Notifications)
                    {
                        //监听是否有新的账户质押保证金， 并将其添加到监控账户中
                        if (!(notifyEventArgs?.State is VM.Types.Array stateItems) || stateItems.Count == 0 || !(notifyEventArgs.ScriptContainer is Transaction transaction)) continue;
                        HandleNotification(snapshot, transaction, notifyEventArgs.ScriptHash, stateItems);
                    }
                }
            }
            Liquidate(snapshot);
            //保存账户
        }

        private void Liquidate(Snapshot snapshot) 
        {
            if (Accounts.Count != 0) 
            {
                foreach (var account in Accounts) 
                {
                    using (ApplicationEngine engine = ApplicationEngine.Run(ScriptFactory.IsSafeScriptBuilder(account, perpContract), snapshot.Clone(), extraGAS: maxGas)) 
                    {
                        if (engine.State.HasFlag(VMState.FAULT)) continue;
                        bool isSafe = engine.ResultStack.Pop().GetBoolean();
                        if (!isSafe) 
                        {
                            //账户不安全，进行清算
                            unsafeAccounts.Add(account);
                        }
                    }
                }
                Dictionary<Task, UInt160> LiquidationTasks = new Dictionary<Task, UInt160>();
                foreach (var account in unsafeAccounts) 
                {
                    if (TransactionFactory.LiquidationImplementation(perpContract, liquidatorWIF, liquidatorAddress, account, url)) 
                    {
                        Accounts.Remove(account);
                    }
                }
            }
            SaveLiquidationAccount();
        }

        private void SaveLiquidationAccount() 
        {
            File.WriteAllText(FileName, JsonConvert.SerializeObject(Accounts));
        }

        private void HandleNotification(Snapshot snapshot, Transaction transaction, UInt160 scriptHash, VM.Types.Array stateItems) 
        {
            if (!(stateItems[0] is VM.Types.ByteArray)) return;
            var eventName = Encoding.UTF8.GetString((stateItems[0].GetByteArray()));
            //账户添加保证金， 添加账户至监控集合
            if (stateItems.Count < 3) return;
            if (!(stateItems[2] is VM.Types.Integer)) return;//TODO: 不一定检查数字
            byte[] userByteAddress = stateItems[1].GetByteArray();
            if (userByteAddress.Length != 20) return;
            UInt160 userAccount = UInt160.Parse(userByteAddress.ToHexString());
            if (eventName == "depositEvent") 
            {
                if (!Accounts.Contains(userAccount)) 
                {
                    Accounts.Add(userAccount);
                }
            }
            if (eventName == "withdrawEvent") 
            {
                //账户取现，判断保证金是否已经归零， 归零则移除检查账户
                using (ApplicationEngine engine = ApplicationEngine.Run(ScriptFactory.GetAccountCashBalance(userAccount, perpContract), snapshot, extraGAS: maxGas)) 
                {
                    if (engine.State.HasFlag(VMState.FAULT)) return;
                    BigInteger result = engine.ResultStack.Pop().GetBigInteger();
                    if (result == BigInteger.Zero) 
                    {
                        Accounts.Remove(userAccount);
                    }
                }
            }
        }

        public bool ShouldThrowExceptionFromCommit(Exception ex)
        {
            return true;
        }
    }
}
