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

        if (weaponModel != null)
        {
            recoilOffset = Vector3.Lerp(recoilOffset, Vector3.zero, Time.deltaTime * recoilSpeed);
            weaponModel.localPosition = originalPosition + recoilOffset;
        }
    }

    void Shoot()
    {
        GameStats.AddShot();

        if (weaponAnimator != null)
            weaponAnimator.SetTrigger("Shoot");

        if (!isMelee && muzzleFlash != null)
            muzzleFlash.Play();

        if (!isMelee && shootSound != null && shootSound.clip != null)
            shootSound.PlayOneShot(shootSound.clip);

        if (weaponModel != null)
            recoilOffset = new Vector3(0, 0, -recoilAmount);

        bool hitEnemy = false;
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, range, shootableLayers))
        {
            DoomHealth enemyHealth = hit.collider.GetComponent<DoomHealth>();
            if (enemyHealth != null)
            {
                hitEnemy = true;
                bool wasDead = enemyHealth.IsDead();
                enemyHealth.TakeDamage(damage);
                GameStats.AddHit();

                HitParticles.CreateBloodSplash(hit.point, hit.normal);

                if (enemyHealth.IsDead() && !wasDead) Hitmarker.Kill(killSound);
                else Hitmarker.Hit(hitSound);
            }

            if (!isMelee && impactEffect != null)
            {
                GameObject impact = Instantiate(impactEffect, hit.point, Quaternion.LookRotation(hit.normal));
                Destroy(impact, 1f);
            }
        }

        if (isMelee && !hitEnemy && shootSound != null && shootSound.clip != null)
            shootSound.PlayOneShot(shootSound.clip);
    }

}
