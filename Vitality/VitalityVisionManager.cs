using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Vitality
{
    public class VitalityVisionManager : MonoBehaviour
    {
        public static VitalityVisionManager Instance;
        private Vitality vitality;

        private float maxShakeSpeed = 1.5f;
        private float maxRotationIntensity = 45f;

        public float shakeSpeed = 1f;
        public float rotationIntensity = 25f;

        private Vector3 originalRotation;

        public bool isShaking = false;
        private bool isDozing = false;
        public bool isPaused = false;

        private MouseLook mouseLook;
        public Image image;

        private float checkValue = 0;

        public GameObject lookingAt;

        private Camera _camera;
        private float maxRayDistance;
        private LayerMask myLayerMask;

        public void Awake()
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
        }

        void Start()
        {
            originalRotation = transform.localEulerAngles;
            mouseLook = GetComponent<MouseLook>();

            _camera = GetComponent<Camera>();
            DragRigidbodyC dragRigidbody = GetComponent<DragRigidbodyC>();
            maxRayDistance = dragRigidbody.maxRayDistance;
            myLayerMask = dragRigidbody.myLayerMask;
        }

        void Update()
        {
            if (Physics.Raycast(GetComponent<Camera>().ScreenPointToRay(Input.mousePosition), out var hitInfo, maxRayDistance, myLayerMask, QueryTriggerInteraction.Collide))
            {
                if (hitInfo.collider.tag == "Pickup" && (bool)hitInfo.collider.GetComponent<ObjectPickupC>() && !hitInfo.collider.GetComponent<EngineComponentC>())
                {
                    lookingAt = hitInfo.collider.gameObject;
                }
                else
                {
                    lookingAt = null;
                }
            }
            else
            {
                lookingAt = null;
            }

            if (!isShaking || isPaused)
                return;

            if (mouseLook.noClipBreaker == checkValue)
                return;

            shakeSpeed = Map(vitality.drunkness, 0f, 100f, 0f, maxShakeSpeed);
            rotationIntensity = Map(vitality.drunkness, 0f, 100f, 0f, maxRotationIntensity);

            if(shakeSpeed > maxShakeSpeed)
                shakeSpeed = maxShakeSpeed;

            if(rotationIntensity > maxRotationIntensity)
                rotationIntensity = maxRotationIntensity;

            Vector3 newRotation = originalRotation;

            float rotateX = Mathf.PerlinNoise(Time.time * shakeSpeed, 0) * 2 - 1;
            float rotateZ = Mathf.PerlinNoise(Time.time * shakeSpeed, Time.time * shakeSpeed) * 2 - 1;

            rotateX *= rotationIntensity;
            rotateZ *= rotationIntensity;

            rotateX += transform.localEulerAngles.x;
            newRotation += new Vector3(rotateX, 0, rotateZ);

            newRotation = new Vector3(newRotation.x, 0, newRotation.z);
            transform.localEulerAngles = newRotation;

            checkValue = mouseLook.noClipBreaker;
        }

        public void DozeFor(float seconds)
        {
            if (!isDozing)
            {
                isDozing = true;
                StartCoroutine(FadeInAndOut(seconds));
            }
        }

        private IEnumerator FadeInAndOut(float seconds)
        {
            yield return StartCoroutine(Fade(1f, 1));

            yield return new WaitForSeconds(seconds);

            yield return StartCoroutine(Fade(0f, 1));

            isDozing = false;
        }

        private IEnumerator Fade(float targetAlpha, float duration)
        {
            float startTime = Time.time;
            float endTime = startTime + duration;
            Color startColor = image.color;
            Color targetColor = new Color(startColor.r, startColor.g, startColor.b, targetAlpha);

            while (!isPaused)
            {
                while (Time.time < endTime)
                {
                    float t = (Time.time - startTime) / duration;
                    image.color = Color.Lerp(startColor, targetColor, t);
                    yield return null;
                }

                if(Time.time >= endTime)
                    break;
            }

            image.color = targetColor;
        }

        float Map(float value, float inMin, float inMax, float outMin, float outMax)
        {
            return (value - inMin) * (outMax - outMin) / (inMax - inMin) + outMin;
        }
    }
}
