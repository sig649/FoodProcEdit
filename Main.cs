using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;
using System.Reflection.Emit;
using System.Reflection;

namespace s649FPE
{//>begin namespaceMain
    namespace Main
    {//>>begin namespaceSub
        public static class Config
        {
            public static float ExpMultiplier => PatchMain.Cf_FoodExpMulti;
        }


        [BepInPlugin("s649_FoodProcEdit", "FoodProcEdit", "0.0.0.0")]
        public class PatchMain : BaseUnityPlugin
        {//>>>begin class:PatchExe

            //////-----Config Entry---------------------------------------------------------------------------------- 

            private static ConfigEntry<float> CE_FoodProcExpMulti;
            public static float Cf_FoodExpMulti => CE_FoodProcExpMulti.Value;
            //loading------------------------------------------------------------------------------------------------------------------------------------------------------------
            internal void LoadConfig()
            {

                CE_FoodProcExpMulti = Config.Bind("#float", "FoodProc_ExpMulti", 10f, "expmulti");
            }

            private void Start()
            {//>>>>begin method:Start
                LoadConfig();
                //var harmony = new Harmony("PatchMain");
                new Harmony("PatchMain").PatchAll();
            }//<<<<end method:Start

            //internal method-------------------------------------------------------------------------------------------------
            public static void Log(string text)
            {
                Debug.Log(text);
            }
        }

        [HarmonyPatch]
        public static class FPE_Transpiler
        {
            [HarmonyPatch(typeof(FoodEffect), "Proc")]
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                bool patched = false;

                // Config.ExpMultiplier の getter を取得
                MethodInfo expMultiplierGetter = typeof(Config).GetProperty("ExpMultiplier", BindingFlags.Public | BindingFlags.Static)?.GetGetMethod();
                if (expMultiplierGetter == null)
                {
                    Debug.LogError("ExpMultiplier getter not found.");
                    return codes;
                }

                // "exp" にジャンプするラベルを保持
                Label? expCaseLabel = null;

                for (int i = 2; i < codes.Count - 1; i++)
                {
                    // Look for: ldstr "exp" → call string.Equals → brtrue → (jump target = case "exp")
                    if (codes[i - 2].opcode == OpCodes.Ldstr &&
                        codes[i - 2].operand is string s && s == "exp" &&
                        codes[i - 1].opcode == OpCodes.Call &&
                        codes[i].opcode == OpCodes.Brtrue_S &&
                        codes[i].operand is Label targetLabel)
                    {
                        expCaseLabel = targetLabel;
                        break;
                    }
                }

                if (expCaseLabel == null)
                {
                    Debug.LogWarning("Could not find label for case \"exp\".");
                    return codes;
                }

                // そのラベルの位置に移動して、ModExp(id, a) を探す
                for (int i = 0; i < codes.Count - 1; i++)
                {
                    if (codes[i].labels.Contains(expCaseLabel.Value))
                    {
                        // この位置から先で ModExp を探す（3命令前後）
                        for (int j = i; j < Math.Min(i + 20, codes.Count - 1); j++)
                        {
                            if (j >= 2 &&
                                codes[j - 2].opcode == OpCodes.Ldarg_0 &&    // c
                                codes[j - 1].IsLdloc() &&                    // id
                                codes[j].IsLdloc() &&                        // a
                                codes[j + 1].Calls(typeof(Chara).GetMethod("ModExp", new[] { typeof(int), typeof(int) })))
                            {
                                // a を ExpMultiplier で乗算
                                codes.InsertRange(j + 1, new[]
                                {
                            new CodeInstruction(OpCodes.Call, expMultiplierGetter),
                            new CodeInstruction(OpCodes.Mul),
                            new CodeInstruction(OpCodes.Conv_I4),
                        });

                                patched = true;
                                break;
                            }
                        }
                        break;
                    }
                }

                if (!patched)
                {
                    Debug.LogWarning("Failed to patch ModExp in 'exp' block.");
                }

                return codes;
            }

            private static bool IsLdloc(this CodeInstruction ci)
            {
                return ci.opcode == OpCodes.Ldloc || ci.opcode == OpCodes.Ldloc_S ||
                       ci.opcode == OpCodes.Ldloc_0 || ci.opcode == OpCodes.Ldloc_1 ||
                       ci.opcode == OpCodes.Ldloc_2 || ci.opcode == OpCodes.Ldloc_3;
            }
        }
    }
}