﻿/*
 * Copyright (c) 2013-present, SteamDB. All rights reserved.
 * Use of this source code is governed by a BSD-style license that can be
 * found in the LICENSE file.
 */

using Dapper;
using SteamKit2;
using System.Globalization;

namespace SteamDatabaseBackend
{
    internal class ClanState : SteamHandler
    {
        public ClanState(CallbackManager manager)
        {
            manager.Subscribe<SteamFriends.ClanStateCallback>(OnClanState);
        }

        private static async void OnClanState(SteamFriends.ClanStateCallback callback)
        {
            if (callback.Events.Count == 0 && callback.Announcements.Count == 0)
            {
                return;
            }

            var groupName = callback.ClanName;
            
            if (string.IsNullOrEmpty(groupName))
            {
                groupName = Steam.Instance.Friends.GetClanName(callback.ClanID);

                // Check once more, because that can fail too
                if (string.IsNullOrEmpty(groupName))
                {
                    groupName = "Group";
                }
            }

            foreach (var announcement in callback.Announcements)
            {
                IRC.Instance.SendMain($"{Colors.BLUE}{groupName}{Colors.NORMAL} announcement: {Colors.OLIVE}{announcement.Headline}{Colors.NORMAL} -{Colors.DARKBLUE} https://steamcommunity.com/gid/{callback.ClanID.AccountID}/announcements/detail/{announcement.ID}");

                Log.WriteInfo(nameof(ClanState), $"{groupName} \"{announcement.Headline}\"");
            }

            await using var db = await Database.GetConnectionAsync();
            
            foreach (var groupEvent in callback.Events)
            {
                var link = $"https://steamcommunity.com/gid/{callback.ClanID.AccountID}/events/{groupEvent.ID}";
                var id = await db.ExecuteScalarAsync<int>("SELECT `ID` FROM `RSS` WHERE `Link` = @Link", new { Link = link });

                if (id > 0)
                {
                    continue;
                }

                IRC.Instance.SendMain(
                    $"{Colors.BLUE}{groupName}{Colors.NORMAL} event: {Colors.OLIVE}{groupEvent.Headline}{Colors.NORMAL} -{Colors.DARKBLUE} {link} {Colors.DARKGRAY}({groupEvent.EventTime.ToString("s", CultureInfo.InvariantCulture).Replace("T", " ")})"
                );

                Log.WriteInfo(nameof(ClanState), $"{groupName} Event \"{groupEvent.Headline}\" {link}");

                await db.ExecuteAsync("INSERT INTO `RSS` (`Link`, `Title`) VALUES(@Link, @Title)", new { Link = link, Title = groupEvent.Headline });
            }
        }
    }
}
