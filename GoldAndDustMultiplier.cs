using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShapeOfDreamsMods
{
    [BepInPlugin("com.blank.goldanddust", "Shape of Dreams Gold & Dreamdust Bonus", "2.2.0")]
    public class GoldAndDustMultiplier : BaseUnityPlugin
    {
        // ──────────────── Config ────────────────
        private static ConfigEntry<float> GoldMultiplier;
        private static ConfigEntry<float> DreamdustMultiplier;
        private static ConfigEntry<float> SellPriceMultiplier;
        private static ConfigEntry<float> BuyPriceMultiplier;

        private Harmony _harmony;
        private static readonly BepInEx.Logging.ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("Gold&Dust");
        private static CoroutineHost CoroutineRunner;

        // ──────────────── Sell Log Throttle (global) ────────────────
        private static float _lastSellLogTimeGlobal = 0f;
        private const float SellLogGlobalCooldown = 3f; // seconds

        private static void LogSellGoldOnce(int orig, int multiplied)
        {
            if (Time.time - _lastSellLogTimeGlobal < SellLogGlobalCooldown)
                return; // skip if still within cooldown

            _lastSellLogTimeGlobal = Time.time;
            Log.LogInfo($"🏪 SellGold x{SellPriceMultiplier.Value} → {orig} → {multiplied}");
        }


        // ──────────────── Init ────────────────
        private void Awake()
        {
            GoldMultiplier = Config.Bind("General", "GoldMultiplier", 1.5f, "Multiplier applied to all gold drops.");
            DreamdustMultiplier = Config.Bind("General", "DreamdustMultiplier", 1.5f, "Multiplier applied to all dreamdust drops.");
            SellPriceMultiplier = Config.Bind("Shop", "SellPriceMultiplier", 1.5f, "Multiplier applied to gold earned from selling items.");
            BuyPriceMultiplier = Config.Bind("Shop", "BuyPriceMultiplier", 1.0f, "Multiplier applied to shop buy prices (lower = cheaper).");

            Log.LogInfo($"✅ Gold&Dust v2.2.0 loaded — Gold x{GoldMultiplier.Value}, Dust x{DreamdustMultiplier.Value}, Sell x{SellPriceMultiplier.Value}, Buy x{BuyPriceMultiplier.Value}");

            // Coroutine runner
            var hostGO = new GameObject("GoldDustCoroutineHost");
            DontDestroyOnLoad(hostGO);
            hostGO.hideFlags = HideFlags.HideAndDontSave;
            CoroutineRunner = hostGO.AddComponent<CoroutineHost>();

            _harmony = new Harmony("com.blank.goldanddust");
            PatchGoldAndDust();
            PatchSellGoldMethods();

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // ──────────────── Scene Hook ────────────────
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "PlayGame") return;

            Log.LogInfo("🕓 PlayGame scene loaded — starting coroutine to apply shop multipliers...");
            if (CoroutineRunner != null)
                CoroutineRunner.StartCoroutine(WaitAndApplyShopMultipliers());
        }

        private System.Collections.IEnumerator WaitAndApplyShopMultipliers()
        {
            Log.LogInfo("⏳ [ShopMult] Coroutine started. Waiting for DewPlayer...");

            var dewPlayerType = AccessTools.TypeByName("DewPlayer");
            if (dewPlayerType == null)
            {
                Log.LogWarning("⚠️ [ShopMult] DewPlayer type not found.");
                yield break;
            }

            Log.LogInfo($"✅ [ShopMult] DewPlayer type found: {dewPlayerType.FullName}");

            float timeout = 10f;
            UnityEngine.Object player = null;
            while (timeout > 0f)
            {
                var players = GameObject.FindObjectsOfType(dewPlayerType);
                if (players != null && players.Length > 0)
                {
                    player = players[0];
                    break;
                }

                timeout -= 0.5f;
                yield return new WaitForSeconds(0.5f);
            }

            if (player == null)
            {
                Log.LogWarning("⚠️ [ShopMult] Timed out waiting for DewPlayer.");
                yield break;
            }

            Log.LogInfo("✅ [ShopMult] DewPlayer found — applying shop multipliers.");
            ApplyShopMultipliers(player, dewPlayerType);
        }

        private void ApplyShopMultipliers(UnityEngine.Object player, Type dewPlayerType)
        {
            try
            {
                var sellField = dewPlayerType.GetField("<sellPriceMultiplier>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
                var buyField = dewPlayerType.GetField("<buyPriceMultiplier>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

                if (sellField != null)
                {
                    sellField.SetValue(player, SellPriceMultiplier.Value);
                    Log.LogInfo($"🏪 [ShopMult] Applied SellPriceMultiplier = x{SellPriceMultiplier.Value}");
                }
                if (buyField != null)
                {
                    buyField.SetValue(player, BuyPriceMultiplier.Value);
                    Log.LogInfo($"🏪 [ShopMult] Applied BuyPriceMultiplier = x{BuyPriceMultiplier.Value}");
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"❌ [ShopMult] Failed to apply shop multipliers: {ex}");
            }
        }

        // ──────────────── Gold/Dust Drop Patches ────────────────
        private void PatchGoldAndDust()
        {
            var pickupManager = AccessTools.TypeByName("PickupManager");
            if (pickupManager == null)
            {
                Log.LogWarning("⚠️ PickupManager type not found.");
                return;
            }

            var methods = pickupManager.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (var m in methods.Where(m => m.Name == "DropGold"))
                PatchAmountMethod(m, true);

            foreach (var m in methods.Where(m => m.Name == "DropDreamDust"))
                PatchAmountMethod(m, false);
        }

        private void PatchAmountMethod(MethodInfo target, bool isGold)
        {
            var parms = target.GetParameters();
            int idx = Array.FindIndex(parms, p => p.ParameterType == typeof(int));
            if (idx < 0) return;

            var prefix = isGold ? GetGoldPrefixByIndex(idx) : GetDustPrefixByIndex(idx);
            if (prefix == null) return;

            try
            {
                _harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                Log.LogInfo($"✅ Patched {target.Name} (int idx {idx})");
            }
            catch (Exception ex)
            {
                Log.LogError($"❌ Failed to patch {target.Name}: {ex}");
            }
        }

        [HarmonyPrefix] private static void Prefix_Gold_Idx2([HarmonyArgument(2)] ref int amount) => ApplyGoldMult(ref amount);
        [HarmonyPrefix] private static void Prefix_Gold_Idx1([HarmonyArgument(1)] ref int amount) => ApplyGoldMult(ref amount);
        [HarmonyPrefix] private static void Prefix_Gold_Idx0([HarmonyArgument(0)] ref int amount) => ApplyGoldMult(ref amount);

        private static void ApplyGoldMult(ref int amount)
        {
            int orig = amount;
            amount = Mathf.Max(0, Mathf.RoundToInt(amount * GoldMultiplier.Value));
            Log.LogInfo($"💰 Gold x{GoldMultiplier.Value} → {orig} → {amount}");
        }

        private MethodInfo GetGoldPrefixByIndex(int idx) => idx switch
        {
            2 => AccessTools.Method(typeof(GoldAndDustMultiplier), nameof(Prefix_Gold_Idx2)),
            1 => AccessTools.Method(typeof(GoldAndDustMultiplier), nameof(Prefix_Gold_Idx1)),
            0 => AccessTools.Method(typeof(GoldAndDustMultiplier), nameof(Prefix_Gold_Idx0)),
            _ => null
        };

        [HarmonyPrefix] private static void Prefix_Dust_Idx2([HarmonyArgument(2)] ref int amount) => ApplyDustMult(ref amount);
        [HarmonyPrefix] private static void Prefix_Dust_Idx1([HarmonyArgument(1)] ref int amount) => ApplyDustMult(ref amount);
        [HarmonyPrefix] private static void Prefix_Dust_Idx0([HarmonyArgument(0)] ref int amount) => ApplyDustMult(ref amount);

        private static void ApplyDustMult(ref int amount)
        {
            int orig = amount;
            amount = Mathf.Max(0, Mathf.RoundToInt(amount * DreamdustMultiplier.Value));
            Log.LogInfo($"✨ Dust x{DreamdustMultiplier.Value} → {orig} → {amount}");
        }

        private MethodInfo GetDustPrefixByIndex(int idx) => idx switch
        {
            2 => AccessTools.Method(typeof(GoldAndDustMultiplier), nameof(Prefix_Dust_Idx2)),
            1 => AccessTools.Method(typeof(GoldAndDustMultiplier), nameof(Prefix_Dust_Idx1)),
            0 => AccessTools.Method(typeof(GoldAndDustMultiplier), nameof(Prefix_Dust_Idx0)),
            _ => null
        };

        // ──────────────── SellGold Patches ────────────────
        private void PatchSellGoldMethods()
        {
            PatchSellGoldOn("SkillTrigger");
            PatchSellGoldOn("Gem");
            PatchSellGoldOn("PropEnt_Merchant_Base"); // may or may not exist, safe to try
        }

        private void PatchSellGoldOn(string typeName)
        {
            var t = AccessTools.TypeByName(typeName);
            if (t == null)
            {
                Log.LogWarning($"⚠️ Sell patch: Type {typeName} not found.");
                return;
            }

            var method = AccessTools.Method(t, "GetSellGold");
            if (method == null)
            {
                Log.LogWarning($"⚠️ Sell patch: Method GetSellGold not found on {typeName}.");
                return;
            }

            try
            {
                _harmony.Patch(method, postfix: new HarmonyMethod(typeof(GoldAndDustMultiplier), nameof(SellGoldPostfix)));
                Log.LogInfo($"✅ Patched {typeName}.GetSellGold for sell multiplier");
            }
            catch (Exception ex)
            {
                Log.LogError($"❌ Failed to patch {typeName}.GetSellGold: {ex}");
            }
        }

        [HarmonyPostfix]
        private static void SellGoldPostfix(ref int __result)
        {
            int orig = __result;
            __result = Mathf.RoundToInt(__result * SellPriceMultiplier.Value);
            LogSellGoldOnce(orig, __result);
        }
    }

    // ──────────────── Helper MonoBehaviour ────────────────
    public class CoroutineHost : MonoBehaviour { }
}
