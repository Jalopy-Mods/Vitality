using BepInEx;
using JaLoader;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Console = JaLoader.Console;
using System;

namespace Vitality
{
    public class Vitality : Mod
    {
        public override string ModID => "Vitality";
        public override string ModName => "Vitality";
        public override string ModAuthor => "Leaxx";
        public override string ModDescription => "Adds fatigue, hunger, thirst and bathroom needs to Jalopy!";
        public override string ModVersion => "1.0.0";
        public override string GitHubLink => "";
        public override WhenToInit WhenToInit => WhenToInit.InGame;
        public override List<(string, string, string)> Dependencies => new List<(string, string, string)>()
        {
            ("JaLoader", "Leaxx", "3.0.0")
        };

        public override bool UseAssets => false;

        private bool isMobilityPresent = false;
        private BaseUnityPlugin mobility;

        private float fatigue = 0f;
        private float hunger = 0f;
        private float thirst = 0f;
        private float bathroom = 0f;

        private Transform playerHold1;
        private Transform playerHold2;

        public override void EventsDeclaration()
        {
            base.EventsDeclaration();

            EventsManager.Instance.OnCustomObjectsRegisterFinished += OnModsLoaded;
            EventsManager.Instance.OnSave += SaveValues;
        }

        public void OnModsLoaded()
        {
            var mod = ModLoader.Instance.FindMod("", "", "Mobility");
            if (mod != null)
            {
                isMobilityPresent = true;
                mobility = (BaseUnityPlugin)mod;
                Console.Instance.Log("Mobility is present!");
            }
        }

        public override void SettingsDeclaration()
        {
            base.SettingsDeclaration();

            InstantiateSettings();

            AddToggle("EnableFatigue", "Fatigue:", true);
            AddToggle("EnableHunger", "Hunger:", true);
            AddToggle("EnableThirst", "Thirst:", true);
            AddToggle("EnableBathroom", "Bathroom Needs:", true);

            AddHeader("Integrations");

            AddToggle("EnableMobilityIntegration", "Mobility Integration:", true);
            AddToggle("EnableEnhMovIntegration", "Enhanced Movement Integration:", true);

            AddHeader("Keybinds");
            AddKeybind("ConsumeItem", "Consume currently held item:", KeyCode.E);
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
        }

        public override void Update()
        {
            base.Update();

            if (fatigue < 100)
            {
                fatigue += Time.deltaTime / 10;
                fatigue += hunger * 0.005f * Time.deltaTime / 2.5f;
                fatigue += thirst * 0.0025f * Time.deltaTime / 2.5f;
            }
            else
                fatigue = 100;

            if (hunger < 100)
                hunger += Time.deltaTime / 7;
            else
                hunger = 100;

            if (thirst < 100)
                thirst += Time.deltaTime / 6;
            else
                thirst = 100;

            if (bathroom < 100)
                bathroom += Time.deltaTime / 7.5f;
            else
                bathroom = 100;

            if (playerHold1.childCount == 0) return;

            if(Input.GetKeyDown(GetPrimaryKeybind("ConsumeItem")))
            {
                var heldObject = playerHold1.GetChild(0).gameObject;
                var objectPickupC = heldObject.GetComponent<ObjectPickupC>();

                if (heldObject.GetComponent<VitalityStats>() != null)
                {
                    var vitalityStats = heldObject.GetComponent<VitalityStats>();
                    fatigue += vitalityStats.AffectsFatigueBy;
                    hunger += vitalityStats.AffectsHungerBy;
                    thirst += vitalityStats.AffectsThirstBy;
                    bathroom += vitalityStats.AffectsBathroomBy;
                }
                else
                {
                    if (objectPickupC.objectID == 153)
                    {
                        hunger += -20;
                        thirst += 5;
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

                Destroy(heldObject);

                if (playerHold2.childCount != 0)
                {
                    DragRigidbodyC.Global.Holding2ToHands();
                }
            }
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
                { "bathroom", bathroom }
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
            }
        }
    }

    [Serializable]
    public class ValuesSave : SerializableDictionary<string, float> { };
}
