﻿/*
 * Copyright (c) 2013-2015, SteamDB. All rights reserved.
 * Use of this source code is governed by a BSD-style license that can be
 * found in the LICENSE file.
 */

using System.Linq;
using System.Threading.Tasks;
using SteamKit2;
using SteamKit2.Internal;

namespace SteamDatabaseBackend
{
    class KeyCommand : Command
    {
        public KeyCommand()
        {
            Trigger = "key";
            IsSteamCommand = true;
        }

        public override async Task OnCommand(CommandArguments command)
        {
            var key = command.Message.Trim();

            if (key.Length < 17)
            {
                command.Reply("Usage:{0} key <steam key>", Colors.OLIVE);

                return;
            }
            
            var msg = new ClientMsgProtobuf<CMsgClientRegisterKey>(EMsg.ClientRegisterKey)
            {
                SourceJobID = Steam.Instance.Client.GetNextJobID(),
                Body =
                {
                    key = key
                }
            };

            Steam.Instance.Client.Send(msg);

            var job = await new AsyncJob<PurchaseResponseCallback>(Steam.Instance.Client, msg.SourceJobID);
            
            if (job.Packages.Count == 0)
            {
                command.Reply($"Nothing has been activated: {Colors.OLIVE}{job.PurchaseResultDetail}");

                return;
            }

            if (job.PurchaseResultDetail == EPurchaseResultDetail.BadActivationCode)
            {
                command.Reply("This key is invalid.");

                return;
            }

            if (job.PurchaseResultDetail == EPurchaseResultDetail.DuplicateActivationCode)
            {
                command.Reply("This key has already been used by someone else.");

                return;
            }

            if (job.PurchaseResultDetail == EPurchaseResultDetail.NoDetail)
            {
                command.Reply($"{Colors.OLIVE}Key activated!{Colors.NORMAL} Packages:{Colors.OLIVE} {string.Join(", ", job.Packages.Select(x => $"{x.Key}: {x.Value}"))}");
            }
            else
            {
                command.Reply($"I don't know what happened. {Colors.OLIVE}{job.PurchaseResultDetail}");
            }
            
            JobManager.AddJob(() => Steam.Instance.Apps.PICSGetProductInfo(Enumerable.Empty<SteamApps.PICSRequest>(), job.Packages.Keys.Select(Utils.NewPICSRequest)));
        }
    }
}
