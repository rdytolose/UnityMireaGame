using UnityEngine;

public class DoomWeapon : MonoBehaviour
{
    [Header("Weapon Settings")]
    public int damage = 25;
    [Tooltip("Пауза между выстрелами в секундах. Меньше = быстрее. Пистолет ~0.3, дробовик ~0.8, автомат ~0.08.")]
    public float fireRate = 0.1f;
    public float range = 100f;

    [Header("Effects")]
    public ParticleSystem muzzleFlash;
    public GameObject impactEffect;
    public AudioSource shootSound;
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

        // Эффекты
        if (muzzleFlash != null)
            muzzleFlash.Play();

        // PlayOneShot наслаивает выстрелы друг на друга (не обрубает предыдущий),
        // поэтому быстрый автомат звучит ровно, без «каши» от Play().
        if (shootSound != null && shootSound.clip != null)
            shootSound.PlayOneShot(shootSound.clip);

        // Отдача
        if (weaponModel != null)
            recoilOffset = new Vector3(0, 0, -recoilAmount);

        // Raycast
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, range, shootableLayers))
        {
            // Урон врагу
            DoomHealth enemyHealth = hit.collider.GetComponent<DoomHealth>();
            if (enemyHealth != null)
            {
                bool wasDead = enemyHealth.IsDead();
                enemyHealth.TakeDamage(damage);
                GameStats.AddHit();   // попадание по врагу (для точности)

                // Партиклы крови при попадании по врагу
                HitParticles.CreateBloodSplash(hit.point, hit.normal);

                // Хитмаркер: обычный при попадании, усиленный при добивании.
                if (enemyHealth.IsDead() && !wasDead) Hitmarker.Kill();
                else Hitmarker.Hit();
            }

            // Эффект попадания
            if (impactEffect != null)
            {
                GameObject impact = Instantiate(impactEffect, hit.point, Quaternion.LookRotation(hit.normal));
                Destroy(impact, 1f);
            }
        }
    }

}
