using UnityEngine;
using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;

namespace Oxide.Plugins
{

    [Info("Sign Artist", "Bombardir", "0.2.3", ResourceId = 992)]
    class SignArtist : RustPlugin
    {
        static GameObject WebObject;
        static UnityWeb UWeb;
        static MethodInfo getFileData = typeof(FileStorage).GetMethod("StorageGet", (BindingFlags.Instance | BindingFlags.NonPublic));
        static readonly Dictionary<ulong, float> CoolDowns = new Dictionary<ulong, float>();

        #region Unity WWW

        struct QueueItem
        {
            public readonly string Url;
            public readonly Signage Sign;
            public readonly ulong SenderId;

            public QueueItem(string url, ulong senderId, Signage signage)
            {
                Url = url;
                SenderId = senderId;
                Sign = signage;
            }
        }

        class UnityWeb : MonoBehaviour
        {
            internal static bool ConsoleLog = true;
            internal static string ConsoleLogMsg = "Player[{steam} {name}] loaded {id} image from {url}!";
            internal static int MaxActiveLoads = 3;
            static readonly Queue<QueueItem> Queue = new Queue<QueueItem>();
            static int _activeLoads = 0;

            public void Add(string url, ulong senderId, Signage s)
            {
                Queue.Enqueue(new QueueItem(url, senderId, s));
                if (_activeLoads < MaxActiveLoads)
                    Next();
            }

            void Next()
            {
                _activeLoads++;
                var qi = Queue.Dequeue();

                var www = new WWW(qi.Url);
                StartCoroutine(WaitForRequest(www, qi));
            }

            IEnumerator WaitForRequest(WWW www, QueueItem info)
            {
                yield return www;

                var player = BasePlayer.FindByID(info.SenderId);

	            if (player != null && !string.IsNullOrEmpty(info.Url) && www != null)
	            {
		            if (www.error == null)
		            {
						//texture size does not matter
			            var texture = new Texture2D(2, 2);
			            var result = texture.LoadImage(www.bytes);

			            if (!result)
			            {
				            player.ChatMessage(String.Format(Error, "Битое изображение"));
							CoolDowns.Remove(info.SenderId);
			            }
			            else
			            {
				            if (www.size > MaxSize)
							{
								player.ChatMessage(SizeError);
								CoolDowns.Remove(player.userID);
							}
							else
							{
								var sign = info.Sign;

								if (sign.textureID > 0U)
									FileStorage.server.Remove(sign.textureID, FileStorage.Type.png, sign.net.ID);

								sign.textureID = FileStorage.server.Store(texture.EncodeToPNG(), FileStorage.Type.png, sign.net.ID, 0U);
								sign.SendNetworkUpdate();
								player.ChatMessage(Loaded);

								if (ConsoleLog)
									ServerConsole.PrintColoured(ConsoleColor.DarkYellow,
										"[Sign Artist]" +
										String.Format(ConsoleLogMsg, player.userID, player.displayName, sign.textureID, info.Url));
							}
			            }
		            }
		            else
		            {
			            player.ChatMessage(String.Format(Error, www.error));
			            CoolDowns.Remove(info.SenderId);
		            }
	            }

	            _activeLoads = Math.Max(0, _activeLoads-1);

                if (Queue.Count > 0)
                    Next();
            }
        }

        #endregion 

        #region Chat Commands

        [ChatCommand("sil")]
        void sil(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                player.ChatMessage(Syntax);
                return;
            }
                
            float cd;
            if (CoolDowns.TryGetValue(player.userID, out cd) && cd > Time.realtimeSinceStartup && !HasPerm(player, "sil_cd"))
            {
                player.ChatMessage( String.Format( CooldownMsg, ToReadableString(cd - Time.realtimeSinceStartup) ) );
                return;
            }

            RaycastHit hit;
            Signage sign = null;
            if (Physics.Raycast(player.eyes.HeadRay(), out hit, MaxDist))
                sign = hit.transform.GetComponentInParent<Signage>();

            if (sign == null)
            {
                player.ChatMessage(NoSignFound);
                return;
            }

            if (!(player.CanBuild() || HasPerm(player, "sil_owner")))
            {
                player.ChatMessage(NotYourSign);
                return;
            }

            if (HasPerm(player, "sil_url"))
            {
                UWeb.Add(args[0], player.userID, sign);
                player.ChatMessage(AddedToQueue);
                if (UrlCooldown > 0)
                    CoolDowns[player.userID] = Time.realtimeSinceStartup + UrlCooldown;
            }
            else
                player.ChatMessage(NoPerm);
        }

        #endregion

        #region Config | Init | Unload

        static float MaxDist = 2f;
        static float StorageCooldown = 180f;
        static float UrlCooldown = 180f;
        static uint MaxSize = 2048U;

        static string NoPerm = "You don't have permission to use this command!";
        static string Syntax = "Syntax: /sil <URL> | /sil s <number>";
        static string NoSignFound = "You need to look/get closer to a sign!";
        static string NotYourSign = "You can't change this sign! (protected by tool cupboard)";
        static string CooldownMsg = "You have recently used this command! You need to wait: {time}";
        static string AddedToQueue = "Your picture was added to load queue!";
        static string Loaded = "Image was loaded to Sign!";
        static string Error = "Image loading fail! Error: {error}";
        static string NotExists = "File with this name not exists in storage folder!";
        static string SizeError = "This file is too large. Max size: {size}KB";

        void LoadDefaultConfig() { }

        void OnServerInitialized()
        {
            permission.RegisterPermission("sil_url", this);
            permission.RegisterPermission("sil_owner", this);
            permission.RegisterPermission("sil_cd", this);

            CheckCfg<bool>("Log url console", ref UnityWeb.ConsoleLog);
            CheckCfg<string>("Log format", ref UnityWeb.ConsoleLogMsg);
            CheckCfg<int>("Max active uploads", ref UnityWeb.MaxActiveLoads);
            CheckCfg<float>("Max sign detection distance", ref MaxDist);
            CheckCfg<uint>("Max file size(KB)", ref MaxSize);
            CheckCfg<float>("Command cooldown after storage", ref StorageCooldown);
            CheckCfg<float>("Command cooldown after url", ref UrlCooldown);
            CheckCfg<string>("Command cooldown msg", ref CooldownMsg);
            CheckCfg<string>("NoPermission", ref NoPerm);
            CheckCfg<string>("Syntax", ref Syntax);
            CheckCfg<string>("No sign", ref NoSignFound);
            CheckCfg<string>("Not your sign", ref NotYourSign);
            CheckCfg<string>("Added to queue", ref AddedToQueue);
            CheckCfg<string>("Loaded", ref Loaded);
            CheckCfg<string>("Not Exists", ref NotExists);
            CheckCfg<string>("Error", ref Error);
            SaveConfig();

            // Small performance improvements
            UnityWeb.ConsoleLogMsg = UnityWeb.ConsoleLogMsg
                                .Replace("{steam}", "{0}")
                                .Replace("{name}", "{1}")
                                .Replace("{id}", "{2}")
                                .Replace("{url}", "{3}");
            Error = Error.Replace("{error}", "{0}");

            CooldownMsg = CooldownMsg.Replace("{time}", "{0}");

            SizeError = SizeError.Replace("{size}", MaxSize.ToString());
            // ----------------------------- //
            
            MaxSize *= 1024;

            WebObject = new GameObject("WebObject");
            UWeb = WebObject.AddComponent<UnityWeb>();
        }

        void Unload()
        {
            UnityEngine.Object.Destroy(WebObject);
	        UWeb = null;
        }

        #endregion

        #region Util methods

        void CheckCfg<T>(string key, ref T var)
        {
            if (Config[key] == null)
                Config[key] = var;
            else
	            try
	            {
		            var = (T) Convert.ChangeType(Config[key], typeof (T));
	            }
	            catch
	            {
		            Config[key] = var;
	            }
        }

        bool HasPerm(BasePlayer p, string pe) => permission.UserHasPermission(p.userID.ToString(), pe);

        static string ToReadableString(float seconds)
        {
            TimeSpan span = TimeSpan.FromSeconds(seconds).Duration();
            string formatted = string.Format("{0}{1}{2}{3}",
                span.Days > 0 ? string.Format("{0:0} day{1}, ", span.Days, span.Days == 1 ? String.Empty : "s") : string.Empty,
                span.Hours > 0 ? string.Format("{0:0} hour{1}, ", span.Hours, span.Hours == 1 ? String.Empty : "s") : string.Empty,
                span.Minutes > 0 ? string.Format("{0:0} minute{1}, ", span.Minutes, span.Minutes == 1 ? String.Empty : "s") : string.Empty,
                span.Seconds > 0 ? string.Format("{0:0} second{1}", span.Seconds, span.Seconds == 1 ? String.Empty : "s") : string.Empty);

            if (formatted.EndsWith(", ")) formatted = formatted.Substring(0, formatted.Length - 2);

            if (string.IsNullOrEmpty(formatted)) formatted = "0 seconds";

            return formatted;
        }

        #endregion
    }
}