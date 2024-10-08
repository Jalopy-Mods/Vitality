using BepInEx;
using JaLoader;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using Console = JaLoader.Console;
using System;
using System.Reflection;
using HarmonyLib;
using Random = UnityEngine.Random;
using System.Runtime.CompilerServices;
using static UnityEngine.EventSystems.EventTrigger;
using System.Collections;
using System.Diagnostics;

namespace Vitality
{
    public class Vitality : Mod
    {
        public override string ModID => "Vitality";
        public override string ModName => "Vitality";
        public override string ModAuthor => "Leaxx";
        public override string ModDescription => "Adds fatigue, hunger, thirst, bathroom needs, and stress to Jalopy!";
        public override string ModVersion => "1.1.0";
        public override string GitHubLink => "https://github.com/Jalopy-Mods/Vitality";
        public override WhenToInit WhenToInit => WhenToInit.InGame;
        public override List<(string, string, string)> Dependencies => new List<(string, string, string)>()
        {
            ("JaLoader", "Leaxx", "3.2.0")
        };

        public override bool UseAssets => false;

        private bool isMobilityPresent = false;
        private bool hasCigLighterTexFix = false;
        private bool isUsingEnhMovement = false;
        private BaseUnityPlugin mobility;
        private Mod laikaAddons;

        private float fatigue = 0f;
        private float hunger = 0f;
        private float thirst = 0f;
        private float bathroom = 0f;
        private float stress = 0f;

        private int cigaretteCount = 0;
        private bool canSmoke = true;
        private bool hasCigarette = false;

        private float stamina = 100f;
        private bool staminaBeingChanged = false;
        private bool restoreStamina = false;

        public float drunkness = 0f;
        public bool paidDrunknessFine = false;

        private Transform playerHold1;
        private Transform playerHold2;
        private DragRigidbodyC dragRigidbodyC;
        private DragRigidbodyC_ModExtension dragRigidbodyC_ModExtension;
        private EnhancedMovement enhMovement;

        private Text messageText;
        private UILabel itemStatsLabel;

        // objectID, (fatigue, hunger, thirst, bathroom, stress)
        private Dictionary<int, (float, float, float, float, float)> vanillaConsumables = new Dictionary<int, (float, float, float, float, float)>()
        {
            { 153, (0, -20, +5, +5, 0) }, // meat
            { 159, (0, 0, -20, +15, 0)}, // water
            { 152, (-20, -5, +15, +10, -15)}, // coffee
            { 151, (+30, -70, -80, +20, -85)}, // wine
            { 156, (-10, -5, +5, +15, -30)}, // tobacco
            { 154, (0, 0, 0, 0, 0) } // medicine, values are randomized when con
        };

        private int barWidth = 180;
        private int barHeight = 15;
        private float spacing = 15f;

        private float minCheckInterval = 20f;
        private float maxCheckInterval = 180f;

        private float nextCheckTime;

        private int xPos = 50;

        private bool showVitals = true;
        private bool wasShowingVitals = false;

        private static Harmony harmony;
        private CarLogicC carLogic;
        private bool patched = false;

        private Color normalColor = new Color(0.71f, 0.53f, 0.57f, 1f);
        private Color desaturatedRed = new Color32(249, 77, 68, 255);

        public override void EventsDeclaration()
        {
            base.EventsDeclaration();

            EventsManager.Instance.OnCustomObjectsRegisterFinished += OnModsLoaded;
            EventsManager.Instance.OnSave += SaveValues;
            EventsManager.Instance.OnPause += OnPause;
            EventsManager.Instance.OnUnpause += OnUnpause;
            EventsManager.Instance.OnSleep += OnSleep;
            EventsManager.Instance.OnNewGame += ResetValues;
        }

        public void OnModsLoaded()
        {
            DoChecking();

            EventsManager.Instance.OnCustomObjectsRegisterFinished -= OnModsLoaded;
        }

        public override void OnReload()
        {
            base.OnReload();

            DoChecking();
        }

        public void DoChecking()
        {
            if (!gameObject.activeSelf)
                return;

            var mod = ModLoader.Instance.FindMod("", "", "Mobility");
            if (mod != null && !ModLoader.Instance.disabledMods.Contains(mod))
            {
                isMobilityPresent = true;
                mobility = (BaseUnityPlugin)mod;
            }

            var mod2 = ModLoader.Instance.FindMod("Leaxx", "LaikaAddons", "Laika Addons");
            if (mod2 != null && !ModLoader.Instance.disabledMods.Contains(mod2))
            {
                laikaAddons = (Mod)mod2;

                if(laikaAddons.GetToggleValue("CigLighterFix") == true)
                    hasCigLighterTexFix = true;
            }

            if (SettingsManager.Instance.UseExperimentalCharacterController)
                isUsingEnhMovement = true;

            if (harmony == null)
            {
                harmony = new Harmony("Leaxx.Vitality.Mod");
                Patch();
            }
        }

        private void Patch()
        {
            if (patched)
                return;

            patched = true;

            if (GetToggleValue("EnableMobilityIntegration") == true && isMobilityPresent)
                harmony.PatchAll();
            else
            {
                harmony.PatchAll(typeof(BorderLogicC).Assembly);

                MethodInfo original = typeof(EnhancedMovement).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
                HarmonyMethod postFix = new HarmonyMethod(typeof(EnhancedMovement_Update_Patch).GetMethod("Postfix"));
                harmony.Patch(original, null, postFix);
            }
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

            harmony?.UnpatchSelf();
        }

        public override void OnDisable()
        {
            base.OnDisable();

            harmony?.UnpatchSelf();
        }

        public void OnPause()
        {
            if (VitalityVisionManager.Instance == null)
                return;

            if (showVitals)
                wasShowingVitals = true;

            showVitals = false;

            VitalityVisionManager.Instance.isPaused = true;
            VitalityVisionManager.Instance.image.gameObject.SetActive(false);
        }

        public void OnSleep()
        {
            if (VitalityVisionManager.Instance == null)
                return;

            fatigue = 0;
            if(GetToggleValue("EnableHunger") == true)
                hunger += 20;
            if(GetToggleValue("EnableThirst") == true)
                thirst += 30;
            if(GetToggleValue("EnableBathroom") == true)
                bathroom += 30;
            if(GetToggleValue("EnableStress") == true)
                stress -= 40;

            SaveValues();
        }

        public void OnUnpause()
        {
            if (VitalityVisionManager.Instance == null)
                return;

            if (wasShowingVitals)
            {
                wasShowingVitals = false;
                showVitals = true;
            }

            VitalityVisionManager.Instance.isPaused = false;
            VitalityVisionManager.Instance.image.gameObject.SetActive(true);
        }

        public override void SettingsDeclaration()
        {
            base.SettingsDeclaration();

            InstantiateSettings();

            AddToggle("EnableFatigue", "Fatigue", true);
            AddToggle("EnableHunger", "Hunger", true);
            AddToggle("EnableThirst", "Thirst", true);
            AddToggle("EnableBathroom", "Bathroom Needs", true);
            AddToggle("EnableStress", "Stress", true);
            AddToggle("EnableDrunkness", "Drunkness", true);

            AddHeader("Integrations");

            AddToggle("EnableMobilityIntegration", "Mobility Integration", true);
            AddToggle("EnableEnhMovIntegration", "Enhanced Movement Integration", true);

            AddHeader("Keybinds");
            AddKeybind("ConsumeItem", "Consume currently held item:", KeyCode.E);
            AddKeybind("Interact", "Interact with certain objects:", KeyCode.F);
            AddKeybind("ToggleVitals", "Toggle Vitals UI:", KeyCode.V);
            AddKeybind("SmokeCigarette", "Smoke a cigarette:", KeyCode.I);
            AddKeybind("CheckCigCount", "Check Cigarette count:", KeyCode.LeftShift, KeyCode.I);

            AddHeader("Vitality UI");
            AddToggle("ShowPercentage", "Show percentages in bars", true);
            AddToggle("TransitionColor", "Slowly transition the color to red as it gets more critical", true);

            AddHeader("Rates");
            AddSlider("FatigueRate", "Fatigue Rate", 10, 20, 15, false);
            AddSlider("HungerRate", "Hunger Rate", 5, 20, 12, false);
            AddSlider("ThirstRate", "Thirst Rate", 5, 20, 10, false);
            AddSlider("BathroomRate", "Bathroom Rate", 5, 22, 12, false);
            AddSlider("StressRate", "Stress Rate", 10, 25, 17, false);

            AddHeader("Features");
            AddToggle("SmokeTobacco", "Allow smoking tobacco", true);

            AddHeader("hello there :)");
        }

        public override void CustomObjectsRegistration()
        {
            base.CustomObjectsRegistration();
        }

        public override void OnEnable()
        {
            base.OnEnable();
            LoadValues();
            DoChecking();
        }

        public override void Awake()
        {
            base.Awake();
        }

        public override void Start()
        {
            base.Start();
            
            playerHold1 = ModHelper.Instance.player.transform.Find("Main Camera").Find("CarryHolder1");
            playerHold2 = ModHelper.Instance.player.transform.Find("Main Camera").Find("CarryHolder2");
            dragRigidbodyC = DragRigidbodyC.Global;
            dragRigidbodyC_ModExtension = dragRigidbodyC.transform.GetComponent<DragRigidbodyC_ModExtension>();

            var obj = Instantiate(new GameObject("VitalityUI"), UIManager.Instance.UICanvas.transform);
            obj.AddComponent<RectTransform>();
            obj.AddComponent<CanvasRenderer>();
            obj.GetComponent<RectTransform>().localPosition = new Vector3(0, -30, 0);
            obj.GetComponent<RectTransform>().rect.Set(0, -20, 500, 100);
            obj.AddComponent<Outline>();
            messageText = obj.AddComponent<Text>();
            messageText.font = UIManager.Instance.UICanvas.transform.Find("JLUpdateDialog/Title").GetComponent<Text>().font;
            messageText.fontSize = 20;
            messageText.rectTransform.sizeDelta = new Vector2(500, 100);
            messageText.alignment = TextAnchor.MiddleCenter;

            var obj2 = Instantiate(GameObject.Find("UI Root").transform.Find("Container/stat4"), GameObject.Find("UI Root").transform.Find("Container"));
            obj2.name = "ObjectStats";
            itemStatsLabel = obj2.GetComponent<UILabel>();
            itemStatsLabel.color = Color.white;

            GameObject imageGO = new GameObject("VitalityDozing");
            imageGO.transform.SetParent(UIManager.Instance.UICanvas.transform, false);

            var image = imageGO.AddComponent<Image>();

            image.color = new Color(0, 0, 0, 0f);
            image.raycastTarget = false;

            RectTransform rectTransform = image.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;

            Camera.main.gameObject.AddComponent<VitalityVisionManager>();
            VitalityVisionManager.Instance.image = image;

            ModHelper.Instance.laika.AddComponent<VitalityBorderManager>();

            SetNextCheckTime(60);

            if(isUsingEnhMovement)
                enhMovement = FindObjectOfType<EnhancedMovement>();

            carLogic = FindObjectOfType<CarLogicC>();

            Console.Instance.AddCommand("resetallvitals", "Resets all vitals to 0", nameof(ResetAllTo0), this);
            Console.Instance.AddCommand("maxvitals", "Sets all vitals to 100", nameof(SetAllToMax), this);
            Console.Instance.AddCommand("alcoholist", "Sets the drunkess to maximum", nameof(SetMaxDrunkness), this);
        }

        private void OnGUI()
        {
            if(!showVitals)
                return;

            float yPos = Screen.height - (barHeight + spacing) * 4 - 40;

            if (isMobilityPresent && !isUsingEnhMovement)
                yPos -= 50 + 5;

            float staminaXPos = Screen.width - 50 - 180;
            float staminaYPos = Screen.height - 50 - 15;

            if (isUsingEnhMovement && GetToggleValue("EnableEnhMovIntegration") == true)
            {
                if (staminaBeingChanged)
                    DrawStaminaBar(staminaXPos, staminaYPos);
            }

            for (int i = 0; i < 5; i++)
            {
                float currentValue = 0f;
                string text = "";

                switch(i)
                {
                    case 0:
                        if(GetToggleValue("EnableFatigue") == false)
                            continue;
                        currentValue = fatigue;
                        text = "Fatigue";
                        break;

                    case 1:
                        if(GetToggleValue("EnableHunger") == false)
                            continue;
                        currentValue = hunger;
                        text = "Hunger";
                        break;

                    case 2:
                        if(GetToggleValue("EnableThirst") == false)
                            continue;
                        currentValue = thirst;
                        text = "Thirst";
                        break;

                    case 3:
                        if(GetToggleValue("EnableBathroom") == false)
                            continue;
                        currentValue = bathroom;
                        text = "Bathroom";
                        break;

                    case 4:
                        if(GetToggleValue("EnableStress") == false)
                            continue;
                        currentValue = stress;
                        text = "Stress";
                        break;
                }

                float num = Mathf.Clamp01(currentValue / 100f);
                Rect rect = new Rect(xPos, yPos + (barHeight + spacing) * i, barWidth * num, barHeight);

                GUI.DrawTexture(new Rect(rect.x - 5f, rect.y - 5f, barWidth + 10f, rect.height + 10f), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(1f, 1f, 0.78f, 1f), 1f, 1f);

                if (num > 0f)
                {
                    if (GetToggleValue("TransitionColor") == true)
                    {
                        float t = Mathf.Clamp(currentValue / 100, 0f, 1f);
                        Color interpolatedColor = Color.Lerp(normalColor, desaturatedRed, t);

                        GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, color: interpolatedColor, 0, 0);
                    }
                    else
                        GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, color: normalColor, 0, 0);
                }

                GUIStyle style = new GUIStyle(GUI.skin.label);
                style.normal.textColor = Color.white;
                style.font = messageText.font;
                style.fontSize = 20;
                float textX = xPos + barWidth + 20;
                float textY = rect.y - 5; 
                GUI.Label(new Rect(textX, textY, 100f, 25), text, style);
                if (GetToggleValue("ShowPercentage") == true)
                {
                    GUIStyle smallerFont = new GUIStyle(style);
                    smallerFont.fontSize = 14;

                    float x = textX - 35;
                    if((int)currentValue > 9 && (int)currentValue < 100)
                        x -= 5;
                    else if((int)currentValue == 100)
                        x -= 15;

                    GUI.Label(new Rect(x, textY + 2.5f, 100f, 25), $"{(int)currentValue}%", smallerFont);
                }
            }
        }

        private void DrawStaminaBar(float xPos, float yPos)
        {
            float currentValue = stamina;
            float num = Mathf.Clamp01(currentValue / 100f);
            Rect rect = new Rect(xPos, yPos, barWidth * num, barHeight);

            GUI.DrawTexture(new Rect(rect.x - 5f, rect.y - 5f, barWidth + 10f, rect.height + 10f), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(1f, 1f, 0.78f, 1f), 1f, 1f);

            if (num > 0f)
            {
                GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, color: new Color(0.71f, 0.53f, 0.57f, 1f), 0, 0);
            }
        }

        public override void Update()
        {
            base.Update();

            #region Value management
            if (GetToggleValue("EnableFatigue") == true)
            {
                if (fatigue < 100)
                {
                    fatigue += Time.deltaTime / GetSliderValue("FatigueRate");
                    fatigue += hunger * 0.005f * Time.deltaTime / 2.5f;
                    fatigue += thirst * 0.0025f * Time.deltaTime / 2.5f;
                }
                else
                    fatigue = 100;
            }
            else
                fatigue = 0;

            if (GetToggleValue("EnableHunger") == true)
            {
                if (hunger < 100)
                    hunger += Time.deltaTime / GetSliderValue("HungerRate");
                else
                    hunger = 100;
            }
            else
                hunger = 0;

            if (GetToggleValue("EnableThirst") == true)
            {
                if (thirst < 100)
                    thirst += Time.deltaTime / GetSliderValue("ThirstRate");
                else
                    thirst = 100;
            }
            else
                thirst = 0;

            if (GetToggleValue("EnableBathroom") == true)
            {
                if (bathroom < 100)
                    bathroom += Time.deltaTime / GetSliderValue("BathroomRate");
                else
                    bathroom = 100;
            }
            else
                bathroom = 0;

            if (GetToggleValue("EnableStress") == true)
            {
                if (stress < 100)
                    stress += Time.deltaTime / GetSliderValue("StressRate");
                else
                    stress = 100;
            }
            else
                stress = 0;
            #endregion

            #region Affect player and show UI

            if(Input.GetKeyDown(GetPrimaryKeybind("ToggleVitals")))
                showVitals = !showVitals;

            if (Time.time >= nextCheckTime)
            {
                if (fatigue >= 60f && GetToggleValue("EnableFatigue") == true)
                {
                    float dozeTime = CalculateDozeTime(fatigue);
                    VitalityVisionManager.Instance.DozeFor(dozeTime);
                    SetNextCheckTime(fatigue);
                    return;
                }

                if (drunkness > 0 && GetToggleValue("EnableDrunkness") == true)
                {
                    float dozeTime = CalculateDozeTimeAlcohol(drunkness);
                    VitalityVisionManager.Instance.DozeFor(dozeTime);
                    SetNextCheckTime(drunkness);
                }
            }

            if(GetToggleValue("EnableDrunkness") == true)
            {
                if (drunkness > 0)
                {
                    drunkness -= Time.deltaTime / 15;
                    VitalityVisionManager.Instance.isShaking = true;
                }
                else
                {
                    drunkness = 0;
                    VitalityVisionManager.Instance.isShaking = false;
                }
            }
            else
                drunkness = 0;

            if (VitalityVisionManager.Instance.lookingAt != null)
            {
                var transform = VitalityVisionManager.Instance.lookingAt;

                if (transform.name == "Cube_689" && transform.GetComponent<BedLogicC>())
                {
                    BedLogicC bedLogic = transform.GetComponent<BedLogicC>();
                    FieldInfo blockedField = typeof(BedLogicC).GetField("block", BindingFlags.NonPublic | BindingFlags.Instance);

                    if (hunger > 70)
                    {
                        messageText.text = "You are too hungry to sleep!";
                        blockedField.SetValue(bedLogic, true);
                    }
                    else if (thirst > 70)
                    {
                        messageText.text = "You are too thirsty to sleep!";
                        blockedField.SetValue(bedLogic, true);
                    }
                    else if (fatigue < 30 && GetToggleValue("EnableFatigue") == true)
                    {
                        messageText.text = "You are not tired enough to sleep!";
                        blockedField.SetValue(bedLogic, true);
                    }
                    else if (bathroom > 40)
                    {
                        messageText.text = "You need to use the bathroom before sleeping!";
                        blockedField.SetValue(bedLogic, true);
                    }
                    else if (stress > 60)
                    {
                        messageText.text = "You are too stressed to sleep!";
                        blockedField.SetValue(bedLogic, true);
                    }
                    else if (drunkness > 15)
                    {
                        messageText.text = "You are too drunk to sleep!";
                        blockedField.SetValue(bedLogic, true);
                    }
                    else
                        blockedField.SetValue(bedLogic, false);
                }
                else if (transform.GetComponent<ObjectPickupC>())
                {
                    var comp = transform.GetComponent<ObjectPickupC>();

                    if (transform.GetComponent<VitalityStats>() != null || vanillaConsumables.ContainsKey(comp.objectID))
                    {
                        bool isDrinkable = false;
                        if (transform.GetComponent<VitalityStats>() != null)
                        {
                            var stats = transform.GetComponent<VitalityStats>();

                            if(stats.IsDrinkable)
                                isDrinkable = true;

                            if(stats.AreAllValuesRandomWhenConsumed || stats.ChooseOnlyOneRandomValueWhenConsumed)
                                itemStatsLabel.text = GetVitalityChangesStringRandom();
                            else
                                itemStatsLabel.text = GetVitalityChangesString(stats.AffectsFatigueBy, stats.AffectsHungerBy, stats.AffectsThirstBy, stats.AffectsBathroomBy, stats.AffectsStressBy, stats.AffectsDrunknessBy);
                        }
                        else
                        {
                            if(comp.objectID == 151)
                                itemStatsLabel.text = GetVitalityChangesString(vanillaConsumables[comp.objectID].Item1, vanillaConsumables[comp.objectID].Item2, vanillaConsumables[comp.objectID].Item3, vanillaConsumables[comp.objectID].Item4, vanillaConsumables[comp.objectID].Item5, 15);
                            else if(comp.objectID == 154)
                                itemStatsLabel.text = GetVitalityChangesStringRandom();
                            else
                                itemStatsLabel.text = GetVitalityChangesString(vanillaConsumables[comp.objectID].Item1, vanillaConsumables[comp.objectID].Item2, vanillaConsumables[comp.objectID].Item3, vanillaConsumables[comp.objectID].Item4, vanillaConsumables[comp.objectID].Item5, 0);
                        }

                        if (!comp.isPurchased)
                            return;

                        string _objectName = "";
                        if (transform.GetComponent<CustomObjectInfo>() != null)
                            _objectName = transform.GetComponent<CustomObjectInfo>().objName;
                        else
                            _objectName = Language.Get(comp.componentHeader, "Inspector_UI");

                        if (isDrinkable || comp.objectID == 151 || comp.objectID == 159)
                            messageText.text = $"Hold object then press {GetPrimaryKeybind("ConsumeItem")} to drink {_objectName}";
                        else if(comp.objectID == 156)
                            messageText.text = $"Hold object then press {GetPrimaryKeybind("ConsumeItem")} to take cigarettes";
                        else
                            messageText.text = $"Hold object then press {GetPrimaryKeybind("ConsumeItem")} to consume {_objectName}";

                    }
                    else
                        itemStatsLabel.text = "";
                }
                else
                {
                    itemStatsLabel.text = "";
                    messageText.text = "";
                }
            }
            else
            {
                itemStatsLabel.text = "";
                messageText.text = "";
            }

            if (VitalityVisionManager.Instance.lookingAtToilet)
            {
                if (bathroom > 20)
                {
                    messageText.text = $"Press {GetPrimaryKeybind("Interact")} to use the toilet";
                    if (Input.GetKeyDown(GetPrimaryKeybind("Interact")))
                    {
                        bathroom = 0;
                        stress -= 15f;
                        drunkness -= 2f;
                    }
                }
                else
                    messageText.text = $"You don't need to use the toilet yet";
            }

            #endregion

            #region Enhanced Movement Integration
            if (isUsingEnhMovement && GetToggleValue("EnableEnhMovIntegration") == true)
            {
                if (enhMovement.isSprinting && enhMovement.isMoving)
                {
                    stamina -= GetStaminaDecreaseRate();
                    restoreStamina = false;
                    StopAllCoroutines();

                    staminaBeingChanged = true;
                }

                if (stamina <= 0)
                {
                    enhMovement.canSprint = false;
                    enhMovement.isSprinting = false;
                }
                else
                    enhMovement.canSprint = true;

                if (stamina <= 10)
                    enhMovement.canJump = false;
                else
                    enhMovement.canJump = true;

                if (!enhMovement.isSprinting && stamina < 100)
                {
                    StartCoroutine(StaminaDelay());

                    if (restoreStamina)
                    {
                        stamina += GetStaminaIncreaseRate();
                    }
                }

                if (stamina > 100)
                    stamina = 100;

                if (stamina == 100)
                    staminaBeingChanged = false;
            }
            #endregion

            #region Smoking tobacco
            if(GetToggleValue("SmokeTobacco") == true)
            {
                if (hasCigLighterTexFix && VitalityVisionManager.Instance.lookingAtCigLighter && hasCigarette)
                {
                    messageText.text = "Click to light the cigarette";

                    if (Input.GetMouseButtonDown(0) && canSmoke && carLogic.engineOn)
                    {
                        Smoke();
                        hasCigarette = false;
                    }
                }

                if (Input.GetKey(GetPrimaryKeybind("CheckCigCount")))
                {
                    if (Input.GetKey(GetSecondaryKeybind("CheckCigCount")))
                        messageText.text = $"You have {cigaretteCount} cigarettes left";
                }
                else if (Input.GetKey(GetPrimaryKeybind("SmokeCigarette")) && cigaretteCount > 0 && canSmoke)
                {
                    if (hasCigLighterTexFix)
                    {
                        hasCigarette = true;
                        messageText.text = "Use the cigarette lighter to light the cigarette";
                    }
                    else if (!hasCigarette)
                        Smoke();
                    else
                        messageText.text = "You can't smoke again yet!";
                }
            }

            #endregion

            #region Consume item
            if (playerHold1.childCount == 0) return;

            var heldObject = playerHold1.GetChild(0).gameObject;
            var objectPickupC = heldObject.GetComponent<ObjectPickupC>();
            bool isConsumable = false;
            VitalityStats vitalityStats = null;

            if (!objectPickupC.isPurchased)
                return;

            if (heldObject.GetComponent<VitalityStats>() != null)
            {
                vitalityStats = heldObject.GetComponent<VitalityStats>();
                isConsumable = true;
            }

            if (vanillaConsumables.ContainsKey(objectPickupC.objectID))
                isConsumable = true;

            if (!isConsumable)
            {
                messageText.text = "";
                return;
            }

            string objectName = "";
            if (heldObject.GetComponent<CustomObjectInfo>() != null)
                objectName = heldObject.GetComponent<CustomObjectInfo>().objName;
            else
                objectName = Language.Get(objectPickupC.componentHeader, "Inspector_UI");

            if ((vitalityStats != null && vitalityStats.IsDrinkable == true) || objectPickupC.objectID == 151 || objectPickupC.objectID == 159)
                messageText.text = $"Press {GetPrimaryKeybind("ConsumeItem")} to drink {objectName}";
            else if (objectPickupC.objectID == 156)
                if(cigaretteCount > 0)
                    messageText.text = $"You can't carry any more cigarettes!";
                else
                    messageText.text = $"Press {GetPrimaryKeybind("ConsumeItem")} to take cigarettes";
            else
                messageText.text = $"Press {GetPrimaryKeybind("ConsumeItem")} to consume {objectName}";

            if (Input.GetKeyDown(GetPrimaryKeybind("ConsumeItem")))
            {
                WaterBottleLogicC waterLogic = null;

                if (objectPickupC.objectID == 159)
                {
                    waterLogic = objectPickupC.GetComponent<WaterBottleLogicC>();

                    if (waterLogic.waterLevel == 0)
                    {
                        objectPickupC.gameObject.AddComponent<VitalityStats>();
                        return;
                    }
                }

                if (vitalityStats != null)
                {
                    if (vitalityStats.AreAllValuesRandomWhenConsumed)
                    {
                        if (GetToggleValue("EnableFatigue") == true)
                        {
                            fatigue += Random.Range(-100, 101);
                        }

                        if (GetToggleValue("EnableHunger") == true)
                        {
                            hunger += Random.Range(-100, 101);
                        }

                        if (GetToggleValue("EnableThirst") == true)
                        {
                            thirst += Random.Range(-100, 101);
                        }

                        if (GetToggleValue("EnableBathroom") == true)
                        {
                            bathroom += Random.Range(-100, 101);
                        }

                        if (GetToggleValue("EnableStress") == true)
                        {
                            stress += Random.Range(-100, 101);
                        }

                        if (GetToggleValue("EnableDrunkness") == true)
                        {
                            drunkness += Random.Range(-100, 101);
                        }
                    }
                    else if (vitalityStats.ChooseOnlyOneRandomValueWhenConsumed)
                    {
                        GetOneRandomValueAndAffect();
                    }
                    else
                    {
                        if (GetToggleValue("EnableHunger") == true)
                        {
                            hunger += vitalityStats.AffectsHungerBy;
                        }
                        if (GetToggleValue("EnableThirst") == true)
                        {
                            thirst += vitalityStats.AffectsThirstBy;
                        }
                        if (GetToggleValue("EnableBathroom") == true)
                        {
                            bathroom += vitalityStats.AffectsBathroomBy;
                        }
                        if (GetToggleValue("EnableStress") == true)
                        {
                            stress += vitalityStats.AffectsStressBy;
                        }
                        if (GetToggleValue("EnableDrunkness") == true)
                        {
                            drunkness += vitalityStats.AffectsDrunknessBy;
                        }
                    }
                   
                }
                else
                {
                    switch(objectPickupC.objectID)
                    {
                        case 154:
                            GetOneRandomValueAndAffect();
                            break;

                        case 156:
                            if (GetToggleValue("SmokeTobacco") == true && cigaretteCount == 0)
                            {
                                cigaretteCount = 20;
                                DestroyConsumedObject(heldObject);
                            }
                            break;

                        default:
                            if (GetToggleValue("EnableFatigue") == true)
                                fatigue += vanillaConsumables[objectPickupC.objectID].Item1;
                            if (GetToggleValue("EnableHunger") == true)
                                hunger += vanillaConsumables[objectPickupC.objectID].Item2;
                            if (GetToggleValue("EnableThirst") == true)
                                thirst += vanillaConsumables[objectPickupC.objectID].Item3;
                            if (GetToggleValue("EnableBathroom") == true)
                                bathroom += vanillaConsumables[objectPickupC.objectID].Item4;
                            if (GetToggleValue("EnableStress") == true)
                                stress += vanillaConsumables[objectPickupC.objectID].Item5;
                            break;
                    }
                }

                if(fatigue < 0)
                    fatigue = 0;
                if(hunger < 0)
                    hunger = 0;
                if(thirst < 0)
                    thirst = 0;
                if(bathroom < 0)
                    bathroom = 0;
                if(stress < 0)
                    stress = 0;

                if (objectPickupC.objectID == 151 && GetToggleValue("EnableDrunkness") == true)
                    drunkness += 15;

                if (waterLogic != null)
                {
                    waterLogic.waterLevel--;
                    waterLogic.WaterUpdate();
                }
                else
                    DestroyConsumedObject(heldObject);
            }
            #endregion       
        }

        private void Smoke()
        {
            canSmoke = false;

            cigaretteCount--;
            fatigue -= 3.5f;
            hunger -= 2.5f;
            thirst += 2.5f;
            bathroom += 5f;
            stress -= 15f;

            ResetIfBelowZero();

            StartCoroutine(ResetCanSmoke());
        }

        private IEnumerator ResetCanSmoke()
        {
            yield return new WaitForSeconds(20);
            canSmoke = true;
        }

        private void DestroyConsumedObject(GameObject heldObject)
        {
            Destroy(heldObject);

            if (playerHold2.childCount != 0)
                dragRigidbodyC.Holding2ToHands();
        }

        public void ResetAllTo0()
        {
            fatigue = hunger = thirst = bathroom = stress = drunkness = 0;
        }

        public void SetAllToMax()
        {
            fatigue = hunger = thirst = bathroom = stress = 100;
        }

        public void SetMaxDrunkness()
        {
            drunkness = 100;
        }

        private void GetOneRandomValueAndAffect()
        {
            int randomValue = Random.Range(0, 6);
            int affectValue = Random.Range(0, 2);

            if (affectValue == 0)
                affectValue = -100;
            else
                affectValue = 100;

            if (randomValue == 0 && GetToggleValue("EnableFatigue") == true)
            {
                fatigue += affectValue;
            }
            else if (randomValue == 1 && GetToggleValue("EnableHunger") == true)
            {
                hunger += affectValue;
            }
            else if (randomValue == 2 && GetToggleValue("EnableThirst") == true)
            {
                thirst += affectValue;
            }
            else if (randomValue == 3 && GetToggleValue("EnableBathroom") == true)
            {
                bathroom += affectValue;
            }
            else if (randomValue == 4 && GetToggleValue("EnableStress") == true)
            {
                stress += affectValue;
            }
            else if (randomValue == 5 && GetToggleValue("EnableDrunkness") == true)
            {
                drunkness += affectValue;
            }
        }

        private void ResetIfBelowZero()
        {
            if (fatigue < 0)
                fatigue = 0;
            if (hunger < 0)
                hunger = 0;
            if (thirst < 0)
                thirst = 0;
            if (bathroom < 0)
                bathroom = 0;
            if (stress < 0)
                stress = 0;
            if (drunkness < 0)
                drunkness = 0;
        }

        private void SaveValues()
        {
            if (VitalityVisionManager.Instance == null)
                return;

            var values = new ValuesSave
            {
                { "fatigue", fatigue },
                { "hunger", hunger },
                { "thirst", thirst },
                { "bathroom", bathroom },
                { "stress", stress },
                { "drunkness", drunkness }
            };

            File.WriteAllText(Path.Combine(Application.persistentDataPath, $@"ModSaves\{ModID}\Values.json"), JsonUtility.ToJson(values, true));
        }

        private void LoadValues()
        {
            if (!gameObject.activeSelf)
                return;

            var values = new ValuesSave();
        
            if (File.Exists(Path.Combine(Application.persistentDataPath, $@"ModSaves\{ModID}\Values.json")))
            {
                string json = File.ReadAllText(Path.Combine(Application.persistentDataPath, $@"ModSaves\{ModID}\Values.json"));
                values = JsonUtility.FromJson<ValuesSave>(json);

                fatigue = values["fatigue"];
                hunger = values["hunger"];
                thirst = values["thirst"];
                bathroom = values["bathroom"];
                stress = values["stress"];
                drunkness = values["drunkness"];
            }
        }

        private void ResetValues()
        {
            var values = new ValuesSave
            {
                { "fatigue", 0 },
                { "hunger", 0 },
                { "thirst", 0 },
                { "bathroom", 0 },
                { "stress", 0 },
                { "drunkness", 0 }
            };

            File.WriteAllText(Path.Combine(Application.persistentDataPath, $@"ModSaves\{ModID}\Values.json"), JsonUtility.ToJson(values, true));
        }

        public string GetVitalityChangesStringRandom()
        {
            string changesString = "\n\n\n\n\n";

            if(GetToggleValue("EnableFatigue") == true)
            {
                if (changesString != "\n\n\n\n\n")
                    changesString += " [000a50]|[-] ";

                changesString += $"[367c7a]Fatigue ?[-]";
            }

            if(GetToggleValue("EnableHunger") == true)
            {
                if (changesString != "\n\n\n\n\n")
                    changesString += " [000a50]|[-] ";

                changesString += $"[367c7a]Hunger ?[-]";
            }

            if(GetToggleValue("EnableThirst") == true)
            {
                if (changesString != "\n\n\n\n\n")
                    changesString += " [000a50]|[-] ";

                changesString += $"[367c7a]Thirst ?[-]";
            }

            if(GetToggleValue("EnableBathroom") == true)
            {
                if (changesString != "\n\n\n\n\n")
                    changesString += " [000a50]|[-] ";

                changesString += $"[367c7a]Bathroom ?[-]";
            }

            if(GetToggleValue("EnableStress") == true)
            {
                if (changesString != "\n\n\n\n\n")
                    changesString += " [000a50]|[-] ";

                changesString += $"[367c7a]Stress ?[-]";
            }

            if(GetToggleValue("EnableDrunkness") == true)
            {
                if (changesString != "\n\n\n\n\n")
                    changesString += " [000a50]|[-] ";

                changesString += $"[367c7a]Drunkness ?[-]";
            }

            return changesString;
        }

        public string GetVitalityChangesString(float fatigue, float hunger, float thirst, float bathroom, float stress, float drunkness)
        {
            string changesString = "\n\n\n\n\n";

            if (fatigue != 0f && GetToggleValue("EnableFatigue") == true)
            {
                if (changesString != "\n\n\n\n\n")
                    changesString += " [000a50]|[-] ";

                if (fatigue > 0)
                    changesString += $"[7c3636]Fatigue +{fatigue}[-]";
                else
                    changesString += $"[4f723d]Fatigue {fatigue}[-]";
            }

            if (hunger != 0f && GetToggleValue("EnableHunger") == true)
            {
                if (changesString != "\n\n\n\n\n")
                    changesString += " [000a50]|[-] ";

                if (hunger > 0)
                    changesString += $"[7c3636]Hunger +{hunger}[-]";
                else
                    changesString += $"[4f723d]Hunger {hunger}[-]";
            }

            if (thirst != 0f && GetToggleValue("EnableThirst") == true)
            {
                if (changesString != "\n\n\n\n\n")
                    changesString += " [000a50]|[-] ";

                if (thirst > 0)
                    changesString += $"[7c3636]Thirst +{thirst}[-]";
                else
                    changesString += $"[4f723d]Thirst {thirst}[-]";
            }

            if (bathroom != 0f && GetToggleValue("EnableBathroom") == true)
            {
                if (changesString != "\n\n\n\n\n")
                    changesString += " [000a50]|[-] ";

                if (bathroom > 0)
                    changesString += $"[7c3636]Bathroom +{bathroom}[-]";
                else
                    changesString += $"[4f723d]Bathroom {bathroom}[-]";
            }

            if (stress != 0f && GetToggleValue("EnableStress") == true)
            {
                if (changesString != "\n\n\n\n\n")
                    changesString += " [000a50]|[-] ";

                if (bathroom > 0)
                    changesString += $"[7c3636]Stress +{bathroom}[-]";
                else
                    changesString += $"[4f723d]Stress {bathroom}[-]";
            }

            if(drunkness != 0f && GetToggleValue("EnableDrunkness") == true)
            {
                if (changesString != "\n\n\n\n\n")
                    changesString += " [000a50]|[-] ";

                if (drunkness > 0)
                    changesString += $"[7c3636]Drunkness +{drunkness}[-]";
                else
                    changesString += $"[4f723d]Drunkness {drunkness}[-]";
            }

            return changesString;
        }

        public void Jumped()
        {
            if (GetToggleValue("EnableEnhMovIntegration") == false)
                return;

            stamina -= GetStaminaDecreaseFromJump();
            restoreStamina = false;
            StopAllCoroutines();

            staminaBeingChanged = true;
        }

        float CalculateDozeTime(float fatigueLevel)
        {
            return Mathf.Lerp(0.5f, 10, (fatigueLevel - 60f) / (100 - 60f));
        }

        float CalculateDozeTimeAlcohol(float alcoholLevel)
        {
            return Mathf.Lerp(0.5f, 5, alcoholLevel / 100);
        }

        void SetNextCheckTime(float modifier = 0)
        {
            float scaledModifier = maxCheckInterval - modifier;
            float checkInterval = Random.Range(minCheckInterval, maxCheckInterval - scaledModifier);
            nextCheckTime = Time.time + checkInterval;
        }

        float GetStaminaIncreaseRate()
        {
            float increment = 0.1f * (1 - fatigue / 100) * (1 - hunger / 100) * (1 - thirst / 100);

            if(increment <= 0.05f)
                increment = 0.05f;

            if(increment > 0.1f)
                increment = 0.1f;

            return increment;
        }

        float GetStaminaDecreaseFromJump()
        {
            float increment = 2.5f * (1 + fatigue / 100) * (1 + hunger / 100) * (1 + thirst / 100);

            if(increment < 2.5f)
                increment = 2.5f;

            if(increment > 10)
                increment = 10;

            return increment;
        }

        float GetStaminaDecreaseRate()
        {
            float increment = 0.1f * (1 + fatigue / 100) * (1 + hunger / 100) * (1 + thirst / 100);
            increment -= 0.075f;

            if(increment < 0.1f)
                increment = 0.1f;

            if (increment > 0.5f)
                increment = 0.5f;

            return increment;
        }

        IEnumerator StaminaDelay()
        {
            staminaBeingChanged = true;
            yield return new WaitForSeconds(3f);
            restoreStamina = true;
        }
    }
}

[Serializable]
    public class ValuesSave : SerializableDictionary<string, float> { };
