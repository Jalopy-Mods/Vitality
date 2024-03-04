using HarmonyLib;
using JaLoader;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Vitality
{
    public class VitalityBorderManager : MonoBehaviour
    {
        public static VitalityBorderManager Instance;
        private Vitality vitality;

        private CarLogicC carLogic;
        private IgnitionLogicC ignitionLogic;
        private DoorLogicC doorLogic;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }

            vitality = FindObjectOfType<Vitality>();

            carLogic = FindObjectOfType<CarLogicC>();
            ignitionLogic = carLogic.ignition.GetComponent<IgnitionLogicC>();
            doorLogic = carLogic.leftDoor.GetComponent<DoorLogicC>();
        }

        private void Update()
        {
            if (ignitionLogic.preventIgnition && doorLogic.isLocked)
            {
                //BorderLogicC borderLogic = FindObjectOfType<BorderLogicC>();


            }
        }

    }

    [HarmonyPatch(typeof(BorderLogicC), "FinePaid")]
    public static class BorderLogicC_FinePaid_Patch
    {
        [HarmonyPrefix]
        public static void Postfix(BorderLogicC __instance)
        {
            if (GetDrunknes() > 5)
            {
                int finePrice = (int)GetDrunknes() * 10;
                WalletC wallet = GameObject.FindObjectOfType<WalletC>();
                if ((float)finePrice > wallet.totalWealth)
                {
                    __instance.StartCoroutine("FinePaidInsufficiantFunds");
                    return;
                }

                wallet.GetComponent<WalletC>().TotalWealth -= finePrice;
                wallet.GetComponent<WalletC>().UpdateWealth();
                __instance.carLogic.GetComponent<CarLogicC>().penaltyFare = 0;

                var component = GameObject.Find("Vitality_Leaxx_Vitality").GetComponents<MonoBehaviour>()[0];
                var type = component.GetType();

                if (type != null)
                {
                    var field = type.GetField("paidDrunknessFine", BindingFlags.Public | BindingFlags.Instance);
                    if (field != null)
                    {
                        field.SetValue(component, true);
                    }
                }

                __instance.StartCoroutine("NoSearch");
            }
        }

        public static float GetDrunknes()
        {
            var component = GameObject.Find("Vitality_Leaxx_Vitality").GetComponents<MonoBehaviour>()[0];
            var type = component.GetType();
            float value = 0;
            if (type != null)
            {
                var field = type.GetField("drunkness", BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    value = (float)field.GetValue(component);
                }
            }

            return value;
        }
    }

    [HarmonyPatch(typeof(BorderLogicC), "OpenGate")]
    public static class BorderLogicC_OpenGate_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(BorderLogicC __instance)
        {
            if (!__instance.director.GetComponent<RouteGeneratorC>().routeGenerated)
            {
                __instance.StartCoroutine("NeedToSelectRoute");
                __instance.GetType().GetField("checkingForRoute", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(__instance, true);
                return;
            }

            if (IsDrunk() && !PaidFine())
            {
                __instance.CloseGate();
                RestrictMovement(__instance);
                CoroutineManager.StartStaticCoroutine(FoundDrunk(__instance, GetDrunkness()));
                return;
            }

            if(PaidFine())
            {
                SetPaidFineFalse();
            }
        }

        public static IEnumerator FoundDrunk(BorderLogicC logic, float drunkness)
        {
            logic.dialogueChecker = "FoundDrunk";
            logic.StartCoroutine("DialogueCheck");
            yield return new WaitForSeconds(0.1f);
            if (logic.canSpeak)
            {
                int num = (int)drunkness * 8;
                float bac = (float)Math.Round(drunkness / 100f * 0.35f, 2);
                logic.speechStack.Clear();
                logic.speechStack.Add("There is a problem.");
                logic.speechStack.Add("You are under the influence of alcohol.");
                logic.speechStack.Add($"Your BAC is [b]{bac}%[/b].");
                logic.speechStack.Add($"You will be charged a fine of [b]{num}[/b].");
                logic.speechStack.Add("Please, pay the fine and you may continue on your journey.");
                logic.StartCoroutine("DialogueSpeech");
                logic.readyToTakeWallet = true;
                logic.windowRelay.GetComponent<Collider>().enabled = true;
                logic.searchGuard.GetComponent<Animator>().SetBool("forceStandStill", true);
            }
            logic.CloseGate();
            RestrictMovement(logic);
        }

        public static bool IsDrunk()
        {
            var component = GameObject.Find("Vitality_Leaxx_Vitality").GetComponents<MonoBehaviour>()[0];
            var type = component.GetType();
            float value = 0;
            if (type != null)
            {
                var field = type.GetField("drunkness", BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    value = (float)field.GetValue(component);
                }
            }

            if (value > 5)
                return true;
            else
                return false;
        }

        public static bool PaidFine()
        {
            var component = GameObject.Find("Vitality_Leaxx_Vitality").GetComponents<MonoBehaviour>()[0];
            var type = component.GetType();
            bool value = false;
            if (type != null)
            {
                var field = type.GetField("paidDrunknessFine", BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    value = (bool)field.GetValue(component);
                }
            }

            return value;
        }

        public static void SetPaidFineFalse()
        {
            var component = GameObject.Find("Vitality_Leaxx_Vitality").GetComponents<MonoBehaviour>()[0];
            var type = component.GetType();
            if (type != null)
            {
                var field = type.GetField("paidDrunknessFine", BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(component, false);
                }
            }
        }

        public static void RestrictMovement(BorderLogicC logic)
        {
            logic.RestrictCarControl();
            logic.director.GetComponent<DirectorC>().isSatAtBorder = true;
        }

        public static float GetDrunkness()
        {
            var component = GameObject.Find("Vitality_Leaxx_Vitality").GetComponents<MonoBehaviour>()[0];
            var type = component.GetType();
            float value = 0;
            if (type != null)
            {
                var field = type.GetField("drunkness", BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    value = (float)field.GetValue(component);
                }
            }

            return value;
        }
    }
}
