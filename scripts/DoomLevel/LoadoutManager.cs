using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Применяет купленное снаряжение к бою. Повесь на пустой объект в сцене doom.
///
/// На старте тянет /api/me (там список items, купленных в телефоне через /api/shop/buy)
/// и применяет эффекты к DoomWeapon / DoomHealth / DoomPlayerController.
///
/// Оружие — анимированный спрайт на одном объекте Weapon (SpriteRenderer + Animator):
/// под нужный ствол подменяется Animator Controller (у каждого свой Idle + триггер Shoot)
/// и статы урона/скорострельности/патронов.
///
/// Минимальный набор:
///   Оружие: pistol (дефолт) / shotgun / smg — экипируется лучшее купленное.
///   Перки:  armor (+50 HP), speed_boots (скорость x1.25).
///
/// Если бэкенд недоступен — ничего не трогаем, остаётся то, что в инспекторе.
/// </summary>
public class LoadoutManager : MonoBehaviour
{
    [Header("Refs (находятся сами, если пусто)")]
    public DoomWeapon weapon;
    public DoomHealth playerHealth;
    public DoomPlayerController playerController;

    [Header("Animator Controllers оружия (у каждого Idle + триггер Shoot)")]
    public RuntimeAnimatorController pistolController;
    public RuntimeAnimatorController shotgunController;
    public RuntimeAnimatorController smgController;
    public RuntimeAnimatorController axeController;   // ближний бой

    [Header("Звук выстрела/удара по стволам (опц.)")]
    public AudioClip pistolSfx;
    public AudioClip shotgunSfx;
    public AudioClip smgSfx;
    public AudioClip axeSfx;

    [Header("Звук попадания по врагу по стволам (опц., пусто → дефолт из Hitmarker)")]
    public AudioClip pistolHitSfx;
    public AudioClip shotgunHitSfx;
    public AudioClip smgHitSfx;
    public AudioClip axeHitSfx;

    [Header("Звук добивания врага по стволам (опц., пусто → дефолт из Hitmarker)")]
    public AudioClip pistolKillSfx;
    public AudioClip shotgunKillSfx;
    public AudioClip smgKillSfx;
    public AudioClip axeKillSfx;

    [Header("Перки")]
    public int armorHealthBonus = 50;
    public float speedBootsMult = 1.25f;

    Animator weaponAnimator;

    void Start()
    {
        AutoWire();
        GameApi.Ensure();
        // FetchMe вызывает колбэк только при успехе — оффлайн оставит настройки из инспектора.
        GameSession.FetchMe(me =>
        {
            string equipped = me != null && !string.IsNullOrEmpty(me.equipped_weapon)
                ? me.equipped_weapon : "pistol";
            var owned = new HashSet<string>(me != null && me.items != null ? me.items : new string[0]);
            // Урон/скорострельность/дальность приходят с бэка (редактируются в админке).
            float dmg = (me != null && me.weapon_damage > 0f) ? me.weapon_damage : -1f;
            float fr = (me != null && me.weapon_fire_rate > 0f) ? me.weapon_fire_rate : -1f;
            float rng = (me != null && me.weapon_range > 0f) ? me.weapon_range : -1f;
            ApplyLoadout(equipped, owned, dmg, fr, rng);
        });
    }

    void AutoWire()
    {
        if (playerController == null) playerController = FindFirstObjectByType<DoomPlayerController>();
        if (weapon == null) weapon = FindFirstObjectByType<DoomWeapon>();
        // Animator висит на том же объекте Weapon, что и DoomWeapon.
        if (weapon != null) weaponAnimator = weapon.GetComponent<Animator>();
        if (playerHealth == null)
        {
            // нужен DoomHealth именно игрока (у врагов он тоже есть)
            foreach (var h in FindObjectsByType<DoomHealth>(FindObjectsSortMode.None))
                if (h.isPlayer) { playerHealth = h; break; }
        }
    }

    public void ApplyLoadout(string equippedWeapon, HashSet<string> owned,
                             float backendDamage = -1f, float backendFireRate = -1f,
                             float backendRange = -1f)
    {
        // Контроллер/звук — по стволу; урон/скорострельность/дальность берём с бэка
        // (админка), а если бэк не дал (оффлайн) — дефолты по стволу.
        RuntimeAnimatorController ctrl; AudioClip sfx; AudioClip hitSfx; AudioClip killSfx;
        int defDmg; float defFr; float defRange;
        switch (equippedWeapon)
        {
            case "axe":     ctrl = axeController;     sfx = axeSfx;     hitSfx = axeHitSfx;     killSfx = axeKillSfx;     defDmg = 80; defFr = 0.50f; defRange = 2.5f; break;
            case "smg":     ctrl = smgController;     sfx = smgSfx;     hitSfx = smgHitSfx;     killSfx = smgKillSfx;     defDmg = 18; defFr = 0.08f; defRange = 100f; break;
            case "shotgun": ctrl = shotgunController; sfx = shotgunSfx; hitSfx = shotgunHitSfx; killSfx = shotgunKillSfx; defDmg = 60; defFr = 0.80f; defRange = 100f; break;
            default:        ctrl = pistolController;  sfx = pistolSfx;  hitSfx = pistolHitSfx;  killSfx = pistolKillSfx;  defDmg = 25; defFr = 0.30f; defRange = 100f; break;
        }
        int damage = backendDamage > 0f ? Mathf.RoundToInt(backendDamage) : defDmg;
        float fireRate = backendFireRate > 0f ? backendFireRate : defFr;
        float range = backendRange > 0f ? backendRange : defRange;
        bool melee = equippedWeapon == "axe";
        EquipWeapon(damage, fireRate, range, melee, ctrl, sfx, hitSfx, killSfx);
        GameStats.SetWeapon(equippedWeapon);   // для итогов

        // ---- Перки ----
        if (owned.Contains("armor") && playerHealth != null)
        {
            playerHealth.maxHealth += armorHealthBonus;
            playerHealth.ResetHealth();   // вылечит до нового максимума
        }

        if (owned.Contains("speed_boots") && playerController != null)
        {
            playerController.walkSpeed *= speedBootsMult;
            playerController.runSpeed  *= speedBootsMult;
        }

    }

    void EquipWeapon(int damage, float fireRate, float range, bool melee,
                     RuntimeAnimatorController controller, AudioClip sfx, AudioClip hitSfx, AudioClip killSfx)
    {
        if (weapon != null)
        {
            weapon.damage = damage;
            weapon.fireRate = fireRate;
            weapon.range = range;       // ближний бой = маленькая дальность
            weapon.isMelee = melee;     // отключает дульную вспышку/следы пуль

            // Звук выстрела ствола: DoomWeapon.Shoot() зовёт shootSound.Play(),
            // поэтому достаточно подменить клип у его AudioSource.
            if (weapon.shootSound != null && sfx != null)
                weapon.shootSound.clip = sfx;

            // Кастомные звуки попадания/добивания. null → Hitmarker сыграет дефолтный.
            weapon.hitSound = hitSfx;
            weapon.killSound = killSfx;
        }

        // Подменяем анимацию ствола (свой Idle + Shoot). DoomWeapon.Shoot() уже дёргает
        // SetTrigger("Shoot") на этом же Animator (он берёт его через GetComponent в Start).
        if (weaponAnimator != null && controller != null)
            weaponAnimator.runtimeAnimatorController = controller;
    }
}
