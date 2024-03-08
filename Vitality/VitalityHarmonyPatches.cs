using HarmonyLib;
using System;
using UnityEngine;
using System.Reflection;
using JaLoader;

namespace Vitality
{
    [HarmonyPatch(typeof(EnhancedMovement), "Update")]
    public static class EnhancedMovement_Update_Patch
    {
        static EnhancedMovement_Update_Patch()
        {
            Debug.Log("PATCHED EM");
        }

        [HarmonyPostfix]
        public static void Postfix(EnhancedMovement __instance)
        {
            if (Input.GetKeyDown(KeyCode.Space) && __instance.isGrounded && __instance.canJump)
            {
                var component = GameObject.Find("Vitality_Leaxx_Vitality").GetComponents<MonoBehaviour>()[0];
                component.GetType().GetMethod("Jumped", BindingFlags.Instance | BindingFlags.Public).Invoke(component, null);
            }
        }
    }

    [HarmonyPatch("Stamina", "RegenerateStamina")]
    public static class Stamina_RegenerateStamina_Patch
    {
        private static Type StaminaType;
        private static PropertyInfo StaminaValueProperty;

        static Stamina_RegenerateStamina_Patch()
        {
            StaminaType = Type.GetType($"Mobility.Features.Stamina, Mobility");
            StaminaValueProperty = StaminaType.GetProperty("StaminaValue", BindingFlags.Static | BindingFlags.Public);
        }

        [HarmonyPrefix]
        public static void Prefix()
        {
            float currentStaminaValue = (float)StaminaValueProperty.GetValue(null, null);

            if (currentStaminaValue < 100)
            {
                float incrementModifier = GetIncrementModifiera();

                StaminaValueProperty.SetValue(null, currentStaminaValue + incrementModifier, null);
                return;
            }
            StaminaValueProperty.SetValue(null, 100f, null);
            return;
        }

        public static float GetIncrementModifiera()
        {
            var component = GameObject.Find("Vitality_Leaxx_Vitality").GetComponents<MonoBehaviour>()[0];
            var type = component.GetType();

            float increment = 0.1f * (1 - (float)type.GetField("fatigue", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(component) / 100) * (1 - (float)type.GetField("hunger", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(component) / 100) * (1 - (float)type.GetField("thirst", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(component) / 100);
            Mathf.Clamp(increment, 0.001f, 0.1f);

            return increment;
        }
    }

    [HarmonyPatch("Stamina", "ConsumeStamina")]
    public static class Stamina_ConsumeStamina_Patch
    {
        private static Type StaminaType;
        private static PropertyInfo StaminaValueProperty;

        static Stamina_ConsumeStamina_Patch()
        {
            StaminaType = Type.GetType($"Mobility.Features.Stamina, Mobility");
            StaminaValueProperty = StaminaType.GetProperty("StaminaValue", BindingFlags.Static | BindingFlags.Public);
        }

        [HarmonyPrefix]
        public static void Prefix()
        {
            float currentStaminaValue = (float)StaminaValueProperty.GetValue(null, null);

            if (currentStaminaValue > 0)
            {
                float incrementModifier = GetIncrementModifier();

                StaminaValueProperty.SetValue(null, currentStaminaValue - incrementModifier, null);
                return;
            }
            StaminaValueProperty.SetValue(null, 0, null);
            return;
        }

        public static float GetIncrementModifier()
        {
            var component = GameObject.Find("Vitality_Leaxx_Vitality").GetComponents<MonoBehaviour>()[0];
            var type = component.GetType();

            float increment = 0.1f * (1 + (float)type.GetField("fatigue", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(component) / 100) * (1 + (float)type.GetField("hunger", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(component) / 100) * (1 + (float)type.GetField("thirst", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(component) / 100);
            increment -= 0.075f;
            Mathf.Clamp(increment, 0.1f, 0.5f);

            return increment;
        }
    }
}
