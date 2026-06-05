using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Breachwardens.Core;
using Breachwardens.Skills;

namespace Breachwardens.Player
{
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        [Header("Health")]
        [SerializeField] private float maxHp = 120f;
        [SerializeField] private float currentHp = 120f;
        [SerializeField] private bool isAlive = true;

        [Header("Stats (optional)")]
        [Tooltip("If assigned, MaxHpMult is applied to scale max HP.")]
        [SerializeField] private HeroStats heroStats;

        private float _baseMaxHp;

        [Header("Death")]
        [Tooltip("Optional: disable these gameobjects on death.")]
        [SerializeField] private GameObject[] disableGameObjectsOnDeath;

        [Header("Healthbar")]
        [SerializeField] private bool faceCamera = true;
        [SerializeField] private Transform healthbarT;
        [SerializeField] private Image fillImage;
        private Transform mainCameraT;

        //Events
        public event Action<float, float> OnHealthChanged; // (current, max)
        public event Action<float> OnDamaged;              // current hp after damage
        public event Action OnDied;

        // IDamageable
        public Transform Transform => transform;
        public bool IsAlive => isAlive;

        //Player Scripts
        private PlayerController pController;
        private PlayerRepair pRepair;
        private PlayerWeapon pWeapon;
        private Animator anim;

        private void Awake()
        {
            _baseMaxHp = maxHp;

            pController = GetComponent<PlayerController>();
            pRepair = GetComponent<PlayerRepair>();
            pWeapon = GetComponent<PlayerWeapon>();
            anim = GetComponent<Animator>();

            if (heroStats != null)
                heroStats.OnStatsChanged += ApplyMaxHpMult;

            ApplyMaxHpMult();
            currentHp = maxHp;
            isAlive = currentHp > 0f;
        }

        private void OnDestroy()
        {
            if (heroStats != null)
                heroStats.OnStatsChanged -= ApplyMaxHpMult;
        }

        private void ApplyMaxHpMult()
        {
            float mult      = heroStats != null ? heroStats.GetFinal(StatType.MaxHpMult) : 1f;
            float newMaxHp  = Mathf.Max(1f, _baseMaxHp * mult);
            float hpRatio   = maxHp > 0f ? currentHp / maxHp : 1f;
            maxHp           = newMaxHp;
            currentHp       = maxHp * hpRatio;
            UpdateHealthbar();
            OnHealthChanged?.Invoke(currentHp, maxHp);
        }

        private void OnEnable()
        {
            // Pooling-friendly register: GameManager may not be initialized yet.
            StartCoroutine(RegisterCoroutine());

            // Restore visuals on reuse.
            ApplyAliveStateStuff(true);
            UpdateHealthbar();
            OnHealthChanged?.Invoke(currentHp, maxHp);
        }

        private void OnDisable()
        {
            // Unregister safely (optional).
            if (GameManagerScript.instance != null)
                GameManagerScript.instance.UnregisterDamageable(this);
        }

        private void LateUpdate()
        {
            if (!faceCamera || healthbarT == null)
                return;

            if (mainCameraT == null)
                mainCameraT = Camera.main.transform;

            if (mainCameraT != null)
            {
                healthbarT.rotation = Quaternion.LookRotation(healthbarT.position - mainCameraT.position);
            }
        }

        private IEnumerator RegisterCoroutine()
        {
            // One frame is usually enough; does not depend on physics.
            yield return null;

            if (GameManagerScript.instance != null)
                GameManagerScript.instance.RegisterDamageable(this);
        }

        public void TakeDamage(float amount)
        {
            if (!isAlive) return;
            if (amount <= 0f) return;

            currentHp -= amount;
            if (currentHp < 0f) currentHp = 0f;

            OnDamaged?.Invoke(currentHp);
            OnHealthChanged?.Invoke(currentHp, maxHp);
            UpdateHealthbar();

            if (currentHp <= 0f) Die();
            else anim.SetTrigger("getHit");
        }

        public void Heal(float amount)
        {
            if (!isAlive) return;
            if (amount <= 0f) return;

            currentHp += amount;
            if (currentHp > maxHp) currentHp = maxHp;

            OnHealthChanged?.Invoke(currentHp, maxHp);
            UpdateHealthbar();
        }

        private void Die()
        {
            if (!isAlive) return;
            isAlive = false;
            ApplyAliveStateStuff(false);
            anim.SetTrigger("death");
            OnDied?.Invoke();
            if (GameManagerScript.instance != null)
            {
                GameManagerScript.instance.GameOver();
            }
        }

        private void ApplyAliveStateStuff(bool isAlive)
        {
            pController.enabled = isAlive;
            pRepair.enabled = isAlive;
            pWeapon.enabled = isAlive;
            if (GetComponent<Rigidbody>()) GetComponent<Rigidbody>().isKinematic = !isAlive;
            if (GetComponent<Collider>()) GetComponent<Collider>().enabled = isAlive;
            if (disableGameObjectsOnDeath != null)
            {
                for (int i = 0; i < disableGameObjectsOnDeath.Length; i++)
                    if (disableGameObjectsOnDeath[i] != null)
                        disableGameObjectsOnDeath[i].SetActive(isAlive);
            }
        }

        private void UpdateHealthbar()
        {
            if (fillImage == null) return;

            float t = (maxHp <= 0f) ? 0f : (currentHp / maxHp);
            fillImage.fillAmount = Mathf.Clamp01(t);
        }
    }
}