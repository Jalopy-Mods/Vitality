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

namespace Vitality
{
    public class Vitality : Mod
    {
        public override string ModID => "Vitality";
        public override string ModName => "Vitality";
        public override string ModAuthor => "Leaxx";
        public override string ModDescription => "Adds fatigue, hunger, thirst, bathroom needs, and stress to Jalopy!";
        public override string ModVersion => "1.0.1";
        public override string GitHubLink => "https://github.com/Jalopy-Mods/Vitality";
        public override WhenToInit WhenToInit => WhenToInit.InGame;
        public override List<(string, string, string)> Dependencies => new List<(string, string, string)>()
        {
            ("JaLoader", "Leaxx", "3.1.0")
        };

        public override bool UseAssets => false;

        private bool isMobilityPresent = false;
        private bool isUsingEnhMovement = false;
        private BaseUnityPlugin mobility;

        private float fatigue = 0f;
        private float hunger = 0f;
        private float thirst = 0f;
        private float bathroom = 0f;
        private float stress = 0f;

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
            { 156, (-10, -5, +5, +15, -30)} // tobacco
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

        public override void EventsDeclaration()
        {
            base.EventsDeclaration();

            EventsManager.Instance.OnCustomObjectsRegisterFinished += OnModsLoaded;
            EventsManager.Instance.OnSave += SaveValues;
            EventsManager.Instance.OnPause += OnPause;
            EventsManager.Instance.OnUnpause += OnUnpause;
            EventsManager.Instance.OnSleep += OnSleep;
        }

        public void OnModsLoaded()
        {
            var mod = ModLoader.Instance.FindMod("", "", "Mobility");
            if (mod != null)
            {
                isMobilityPresent = true;
                mobility = (BaseUnityPlugin)mod;
            }
            if(SettingsManager.Instance.UseExperimentalCharacterController)
                isUsingEnhMovement = true;

            if (harmony == null)
            {
                harmony = new Harmony("Leaxx.Vitality.Mod");
                if (GetToggleValue("EnableMobilityIntegration") == true && isMobilityPresent)
                    harmony.PatchAll();
                else
                    harmony.PatchAll(typeof(BorderLogicC).Assembly);
            }
        }

        public void OnPause()
        {
            if(showVitals)
                wasShowingVitals = true;

            showVitals = false;

            VitalityVisionManager.Instance.isPaused = true;
            VitalityVisionManager.Instance.image.gameObject.SetActive(false);
        }

        public void OnSleep()
        {
            fatigue = 0;
            hunger += 20;
            thirst += 30;
            bathroom += 30;
            stress -= 40;

            SaveValues();
        }

        public void OnUnpause()
        {
            if(wasShowingVitals)
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
        }

        public override void CustomObjectsRegistration()
        {
            base.CustomObjectsRegistration();
        }

        public override void OnEnable()
        {
            base.OnEnable();
            LoadValues();
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
                    GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, color: new Color(0.71f, 0.53f, 0.57f, 1f), 0, 0);
                }

                GUIStyle style = new GUIStyle(GUI.skin.label);
                style.normal.textColor = Color.white;
                style.font = messageText.font;
                style.fontSize = 20;
                float textX = xPos + barWidth + 20;
                float textY = rect.y - 5; 
                GUI.Label(new Rect(textX, textY, 100f, 25), text, style);
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
                    fatigue += Time.deltaTime / 12;
                    fatigue += hunger * 0.005f * Time.deltaTime / 2.5f;
                    fatigue += thirst * 0.0025f * Time.deltaTime / 2.5f;
                }
                else
                    fatigue = 100;
            }

            if (GetToggleValue("EnableHunger") == true)
            {
                if (hunger < 100)
                    hunger += Time.deltaTime / 7;
                else
                    hunger = 100;
            }

            if (GetToggleValue("EnableThirst") == true)
            {
                if (thirst < 100)
                    thirst += Time.deltaTime / 6;
                else
                    thirst = 100;
            }

            if (GetToggleValue("EnableBathroom") == true)
            {
                if (bathroom < 100)
                    bathroom += Time.deltaTime / 7.5f;
                else
                    bathroom = 100;
            }

            if (GetToggleValue("EnableStress") == true)
            {
                if (stress < 100)
                    stress += Time.deltaTime / 15;
                else
                    stress = 100;
            }  
            #endregion

            #region Affect player and show UI

            if(Input.GetKeyDown(GetPrimaryKeybind("ToggleVitals")))
                showVitals = !showVitals;

            if (Time.time >= nextCheckTime)
            {
                if (fatigue >= 60f)
                {
                    float dozeTime = CalculateDozeTime(fatigue);
                    VitalityVisionManager.Instance.DozeFor(dozeTime);
                    SetNextCheckTime(fatigue);
                    return;
                }

                if (drunkness > 0)
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

            if (dragRigidbodyC_ModExtension.lookingAt != null)
            {
                var transform = dragRigidbodyC_ModExtension.lookingAt;

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
                else if (transform.name == "toiletSeat")
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
                else if (transform.GetComponent<ObjectPickupC>())
                {
                    var comp = transform.GetComponent<ObjectPickupC>();

                    if (transform.GetComponent<VitalityStats>() != null || vanillaConsumables.ContainsKey(comp.objectID))
                    {
                        if (transform.GetComponent<VitalityStats>() != null)
                        {
                            var stats = transform.GetComponent<VitalityStats>();

                            itemStatsLabel.text = GetVitalityChangesString(stats.AffectsFatigueBy, stats.AffectsHungerBy, stats.AffectsThirstBy, stats.AffectsBathroomBy, stats.AffectsStressBy, stats.AffectsDrunknessBy);
                        }
                        else
                        {
                            if(comp.objectID == 151)
                                itemStatsLabel.text = GetVitalityChangesString(vanillaConsumables[comp.objectID].Item1, vanillaConsumables[comp.objectID].Item2, vanillaConsumables[comp.objectID].Item3, vanillaConsumables[comp.objectID].Item4, vanillaConsumables[comp.objectID].Item5, 15);
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
                    if (GetToggleValue("EnableFatigue") == true)
                    {
                        fatigue += vitalityStats.AffectsFatigueBy;
                    }
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
                else
                {
                    if (GetToggleValue("EnableFatigue") == true)
                    {
                        fatigue += vanillaConsumables[objectPickupC.objectID].Item1;
                    }
                    if (GetToggleValue("EnableHunger") == true)
                    {
                        hunger += vanillaConsumables[objectPickupC.objectID].Item2;
                    }
                    if (GetToggleValue("EnableThirst") == true)
                    {
                        thirst += vanillaConsumables[objectPickupC.objectID].Item3;
                    }
                    if (GetToggleValue("EnableBathroom") == true)
                    {
                        bathroom += vanillaConsumables[objectPickupC.objectID].Item4;
                    }
                    if (GetToggleValue("EnableStress") == true)
                    {
                        stress += vanillaConsumables[objectPickupC.objectID].Item5;
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
                {
                    Destroy(heldObject);

                    if (playerHold2.childCount != 0)
                        dragRigidbodyC.Holding2ToHands();
                }
            }
            #endregion       
        }

        public override void OnDisable()
        {
            base.OnDisable();
        }

        private void SaveValues()
        {
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
            Mathf.Clamp(increment, 0.01f, 0.1f);

            return increment;
        }

        float GetStaminaDecreaseFromJump()
        {
            float increment = 2.5f * (1 + fatigue / 100) * (1 + hunger / 100) * (1 + thirst / 100);
            Mathf.Clamp(increment, 2.5f, 10);

            return increment;
        }

        float GetStaminaDecreaseRate()
        {
            float increment = 0.1f * (1 + fatigue / 100) * (1 + hunger / 100) * (1 + thirst / 100);
            increment -= 0.075f;
            Mathf.Clamp(increment, 0.1f, 0.5f);

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
