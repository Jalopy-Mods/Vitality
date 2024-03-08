using UnityEngine;

namespace Vitality
{
    public class VitalityStats : MonoBehaviour
    {
        public float AffectsFatigueBy = 0f;
        public float AffectsHungerBy = 0f;
        public float AffectsThirstBy = 0f;
        public float AffectsBathroomBy = 0f;
        public float AffectsStressBy = 0f;
        public float AffectsDrunknessBy = 0f;
        public bool AreAllValuesRandomWhenConsumed = false;
        public bool ChooseOnlyOneRandomValueWhenConsumed = false;
    }
}
