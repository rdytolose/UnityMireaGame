using UnityEngine;

public class DoomWeapon : MonoBehaviour
{
    [Header("Weapon Settings")]
    public int damage = 25;
    [Tooltip("Пауза между выстрелами в секундах. Меньше = быстрее. Пистолет ~0.3, дробовик ~0.8, автомат ~0.08.")]
    public float fireRate = 0.1f;
    [Tooltip("Дальность луча. Дальнобойное ~100, ближний бой (топор) ~2.5 — бьёт только вплотную.")]
    public float range = 100f;
    [Tooltip("Ближний бой: без дульной вспышки и следов от пуль (взмах вместо выстрела).")]
    public bool isMelee = false;

    [Header("Effects")]
    public ParticleSystem muzzleFlash;
    public GameObject impactEffect;
    public AudioSource shootSound;
    [Tooltip("Звук попадания по врагу для этого оружия. Пусто — дефолтный из Hitmarker.")]
    public AudioClip hitSound;
    [Tooltip("Звук добивания врага для этого оружия. Пусто — дефолтный killSound из Hitmarker.")]
    public AudioClip killSound;
    public LayerMask shootableLayers;

    [Header("Recoil")]
    public float recoilAmount = 0.5f;
    public float recoilSpeed = 10f;

    [Header("References")]
    public Camera playerCamera;
    public Transform weaponModel;
    public Animator weaponAnimator;

    private float nextFireTime = 0f;
    private Vector3 originalPosition;
    private Vector3 recoilOffset;

    void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;
        
        if (weaponModel != null)
            originalPosition = weaponModel.localPosition;
        
        if (weaponAnimator == null)
            weaponAnimator = GetComponent<Animator>();
    }

    void Update()
    {
        if (Input.GetButton("Fire1") && Time.time >= nextFireTime)
        {
            Shoot();
            nextFireTime = Time.time + fireRate;
        }

        // Плавный возврат после отдачи
        if (weaponModel != null)
        {
            recoilOffset = Vector3.Lerp(recoilOffset, Vector3.zero, Time.deltaTime * recoilSpeed);
            weaponModel.localPosition = originalPosition + recoilOffset;
        }
    }

    void Shoot()
    {
        GameStats.AddShot();   // для статистики/точности

        // Анимация стрельбы
        if (weaponAnimator != null)
            weaponAnimator.SetTrigger("Shoot");

        // Эффекты (у ближнего боя дульной вспышки нет — это взмах)
        if (!isMelee && muzzleFlash != null)
            muzzleFlash.Play();

        // Дальнобойное: звук выстрела играет всегда. Ближний бой: звук взмаха играем
        // только на ПРОМАХЕ (ниже), чтобы при убийстве звучал лишь килл-звук.
        // PlayOneShot наслаивает звуки (не обрубает предыдущий) — автомат звучит ровно.
        if (!isMelee && shootSound != null && shootSound.clip != null)
            shootSound.PlayOneShot(shootSound.clip);

        // Отдача
        if (weaponModel != null)
            recoilOffset = new Vector3(0, 0, -recoilAmount);

        // Raycast
        bool hitEnemy = false;
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, range, shootableLayers))
        {
            // Урон врагу
            DoomHealth enemyHealth = hit.collider.GetComponent<DoomHealth>();
            if (enemyHealth != null)
            {
                hitEnemy = true;
                bool wasDead = enemyHealth.IsDead();
                enemyHealth.TakeDamage(damage);
                GameStats.AddHit();   // попадание по врагу (для точности)

                // Партиклы крови при попадании по врагу
                HitParticles.CreateBloodSplash(hit.point, hit.normal);

                // Хитмаркер: обычный при попадании, усиленный при добивании.
                // hit/killSound — кастомные звуки оружия (если пусто, Hitmarker берёт дефолт).
                if (enemyHealth.IsDead() && !wasDead) Hitmarker.Kill(killSound);
                else Hitmarker.Hit(hitSound);
            }

            // Эффект попадания по поверхности (след от пули — не для ближнего боя)
            if (!isMelee && impactEffect != null)
            {
                GameObject impact = Instantiate(impactEffect, hit.point, Quaternion.LookRotation(hit.normal));
                Destroy(impact, 1f);
            }
        }

        // Ближний бой: промах (не задели врага) → звук взмаха (звук1). При попадании
        // топор ваншотит и звук убийства (звук2) играет хитмаркер — взмах не дублируем.
        if (isMelee && !hitEnemy && shootSound != null && shootSound.clip != null)
            shootSound.PlayOneShot(shootSound.clip);
    }

}
