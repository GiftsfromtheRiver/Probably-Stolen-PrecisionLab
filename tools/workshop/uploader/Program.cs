using System;
using System.IO;
using System.Threading;
using Steamworks;

namespace WorkshopUploader
{
    /// <summary>
    /// Minimal Steam Workshop uploader for Probably Stolen (appid 4348910), using the game's own
    /// Steamworks.NET assembly. Run it while the Steam client is running and logged in.
    /// First run creates the Workshop item; pass --pubfile to update it later.
    /// Use --query &lt;id&gt; to print the published item state (title/visibility/result).
    /// </summary>
    internal static class Program
    {
        private const uint AppIdValue = 4348910;
        private static readonly AppId_t AppId = new AppId_t(AppIdValue);

        private static int Main(string[] args)
        {
            string query = GetArg(args, "--query");
            if (!string.IsNullOrEmpty(query) && ulong.TryParse(query, out ulong queryId) && queryId != 0)
            {
                return RunQuery(queryId);
            }

            string content = GetArg(args, "--content");
            string preview = GetArg(args, "--preview");
            string title = GetArg(args, "--title");
            string descFile = GetArg(args, "--desc-file");
            string pubFileArg = GetArg(args, "--pubfile");
            string idOut = GetArg(args, "--id-out");
            string visibilityArg = GetArg(args, "--visibility") ?? "public";
            string changeNote = GetArg(args, "--changenote") ?? "Precision Lab update";

            if (string.IsNullOrEmpty(content) || !Directory.Exists(content))
            {
                Console.Error.WriteLine("Missing --content <folder>");
                return 2;
            }
            if (string.IsNullOrEmpty(preview) || !File.Exists(preview))
            {
                Console.Error.WriteLine("Missing --preview <png>");
                return 2;
            }
            if (string.IsNullOrEmpty(title))
            {
                title = "Precision Lab / 精密实验室";
            }
            string description = !string.IsNullOrEmpty(descFile) && File.Exists(descFile)
                ? File.ReadAllText(descFile)
                : "Precision Lab for Probably Stolen 0.46D.";
            content = Path.GetFullPath(content);
            preview = Path.GetFullPath(preview);

            ERemoteStoragePublishedFileVisibility visibility;
            if (visibilityArg.Equals("private", StringComparison.OrdinalIgnoreCase))
            {
                visibility = ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPrivate;
            }
            else if (visibilityArg.Equals("unlisted", StringComparison.OrdinalIgnoreCase))
            {
                visibility = ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityUnlisted;
            }
            else
            {
                visibility = ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic;
            }

            if (!SteamAPI.Init())
            {
                Console.Error.WriteLine("SteamAPI.Init() failed. Start Steam, log in, and run this from a folder that contains steam_appid.txt = 4348910.");
                return 3;
            }
            try
            {
                Console.WriteLine("[workshop] Steam initialized as " + SteamFriends.GetPersonaName());
                PublishedFileId_t publishedFileId = default;
                if (!string.IsNullOrEmpty(pubFileArg) && ulong.TryParse(pubFileArg.Trim(), out ulong existingId) && existingId != 0)
                {
                    publishedFileId = new PublishedFileId_t(existingId);
                    Console.WriteLine("[workshop] updating existing item " + existingId);
                }
                else
                {
                    CreateItemResult_t createResult = default;
                    bool createDone = false;
                    CallResult<CreateItemResult_t> createCall = CallResult<CreateItemResult_t>.Create((CreateItemResult_t result, bool ioFailure) =>
                    {
                        createResult = result;
                        createDone = true;
                    });
                    SteamAPICall_t createHandle = SteamUGC.CreateItem(AppId, EWorkshopFileType.k_EWorkshopFileTypeCommunity);
                    createCall.Set(createHandle);
                    if (!WaitFor(() => createDone, 120))
                    {
                        Console.Error.WriteLine("[workshop] CreateItem timed out");
                        return 4;
                    }
                    if (createResult.m_eResult != EResult.k_EResultOK)
                    {
                        Console.Error.WriteLine("[workshop] CreateItem failed: " + createResult.m_eResult);
                        return 4;
                    }
                    publishedFileId = createResult.m_nPublishedFileId;
                    Console.WriteLine("[workshop] created item " + publishedFileId.m_PublishedFileId);
                    if (!string.IsNullOrEmpty(idOut))
                    {
                        File.WriteAllText(idOut, publishedFileId.m_PublishedFileId.ToString());
                    }
                }

                UGCUpdateHandle_t update = SteamUGC.StartItemUpdate(AppId, publishedFileId);
                Console.WriteLine("[workshop] title: " + SteamUGC.SetItemTitle(update, title));
                Console.WriteLine("[workshop] desc: " + SteamUGC.SetItemDescription(update, description));
                Console.WriteLine("[workshop] content: " + SteamUGC.SetItemContent(update, content));
                Console.WriteLine("[workshop] preview: " + SteamUGC.SetItemPreview(update, preview));
                Console.WriteLine("[workshop] visibility: " + SteamUGC.SetItemVisibility(update, visibility));

                SubmitItemUpdateResult_t submitResult = default;
                bool submitDone = false;
                CallResult<SubmitItemUpdateResult_t> submitCall = CallResult<SubmitItemUpdateResult_t>.Create((SubmitItemUpdateResult_t result, bool ioFailure) =>
                {
                    submitResult = result;
                    submitDone = true;
                });
                SteamAPICall_t submitHandle = SteamUGC.SubmitItemUpdate(update, changeNote);
                submitCall.Set(submitHandle);
                DateTime submitDeadline = DateTime.UtcNow.AddSeconds(900);
                int lastPercent = -1;
                while (!submitDone && DateTime.UtcNow < submitDeadline)
                {
                    SteamAPI.RunCallbacks();
                    SteamUGC.GetItemUpdateProgress(update, out ulong processed, out ulong total);
                    if (total > 0)
                    {
                        int percent = (int)(processed * 100 / total);
                        if (percent != lastPercent)
                        {
                            lastPercent = percent;
                            Console.WriteLine("[workshop] upload progress " + processed + "/" + total + " (" + percent + "%)");
                        }
                    }
                    Thread.Sleep(100);
                }
                if (!submitDone || submitResult.m_eResult != EResult.k_EResultOK)
                {
                    Console.Error.WriteLine("[workshop] SubmitItemUpdate failed: " + submitResult.m_eResult);
                    return 5;
                }
                Console.WriteLine("[workshop] published: https://steamcommunity.com/sharedfiles/filedetails/?id=" + publishedFileId.m_PublishedFileId);
                if (!string.IsNullOrEmpty(idOut))
                {
                    File.WriteAllText(idOut, publishedFileId.m_PublishedFileId.ToString());
                }
                return 0;
            }
            finally
            {
                SteamAPI.Shutdown();
            }
        }

        private static int RunQuery(ulong idValue)
        {
            if (!SteamAPI.Init())
            {
                Console.Error.WriteLine("SteamAPI.Init() failed for query.");
                return 3;
            }
            try
            {
                PublishedFileId_t id = new PublishedFileId_t(idValue);
                UGCQueryHandle_t queryHandle = SteamUGC.CreateQueryUGCDetailsRequest(new[] { id }, 1);
                SteamUGCQueryCompleted_t queryResult = default;
                bool queryDone = false;
                CallResult<SteamUGCQueryCompleted_t> queryCall = CallResult<SteamUGCQueryCompleted_t>.Create((SteamUGCQueryCompleted_t r, bool io) =>
                {
                    queryResult = r;
                    queryDone = true;
                });
                SteamAPICall_t queryCallHandle = SteamUGC.SendQueryUGCRequest(queryHandle);
                queryCall.Set(queryCallHandle);
                WaitFor(() => queryDone, 90);
                if (!queryDone || queryResult.m_eResult != EResult.k_EResultOK)
                {
                    Console.Error.WriteLine("Query failed: " + (queryDone ? queryResult.m_eResult.ToString() : "timeout"));
                    return 6;
                }
                SteamUGCDetails_t details;
                if (!SteamUGC.GetQueryUGCResult(queryResult.m_handle, 0, out details))
                {
                    Console.Error.WriteLine("GetQueryUGCResult failed");
                    return 6;
                }
                Console.WriteLine("[workshop] id=" + details.m_nPublishedFileId.m_PublishedFileId);
                Console.WriteLine("[workshop] title=" + details.m_rgchTitle);
                Console.WriteLine("[workshop] visibility=" + details.m_eVisibility + " banned=" + details.m_bBanned + " result=" + details.m_eResult);
                Console.WriteLine("[workshop] fileSize=" + details.m_nFileSize + " previewSize=" + details.m_nPreviewFileSize);
                Console.WriteLine("[workshop] descLen=" + (details.m_rgchDescription != null ? details.m_rgchDescription.Length : 0));
                return 0;
            }
            finally
            {
                SteamAPI.Shutdown();
            }
        }

        private static bool WaitFor(Func<bool> condition, int seconds)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(seconds);
            while (DateTime.UtcNow < deadline)
            {
                SteamAPI.RunCallbacks();
                if (condition())
                {
                    return true;
                }
                Thread.Sleep(50);
            }
            return condition();
        }

        private static string GetArg(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }
            return null;
        }
    }
}
