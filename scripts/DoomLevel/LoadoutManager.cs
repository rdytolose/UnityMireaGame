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

    [Header("Звук выстрела по стволам (опц.)")]
    public AudioClip pistolSfx;
    public AudioClip shotgunSfx;
    public AudioClip smgSfx;

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
            // Урон/скорострельность приходят с бэка (редактируются в админке).
            float dmg = (me != null && me.weapon_damage > 0f) ? me.weapon_damage : -1f;
            float fr = (me != null && me.weapon_fire_rate > 0f) ? me.weapon_fire_rate : -1f;
            ApplyLoadout(equipped, owned, dmg, fr);
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
                             float backendDamage = -1f, float backendFireRate = -1f)
    {
        // Контроллер/звук — по стволу; урон/скорострельность берём с бэка (админка),
        // а если бэк не дал (оффлайн) — дефолты по стволу.
        RuntimeAnimatorController ctrl; AudioClip sfx;
        int defDmg; float defFr;
        switch (equippedWeapon)
        {
            case "smg":     ctrl = smgController;     sfx = smgSfx;     defDmg = 18; defFr = 0.08f; break;
            case "shotgun": ctrl = shotgunController; sfx = shotgunSfx; defDmg = 60; defFr = 0.80f; break;
            default:        ctrl = pistolController;  sfx = pistolSfx;  defDmg = 25; defFr = 0.30f; break;
        }
        int damage = backendDamage > 0f ? Mathf.RoundToInt(backendDamage) : defDmg;
        float fireRate = backendFireRate > 0f ? backendFireRate : defFr;
        EquipWeapon(damage, fireRate, ctrl, sfx);
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

    void EquipWeapon(int damage, float fireRate, RuntimeAnimatorController controller, AudioClip sfx)
    {
        if (weapon != null)
        {
            weapon.damage = damage;
            weapon.fireRate = fireRate;

            // Звук выстрела ствола: DoomWeapon.Shoot() зовёт shootSound.Play(),
            // поэтому достаточно подменить клип у его AudioSource.
            if (weapon.shootSound != null && sfx != null)
                weapon.shootSound.clip = sfx;
        }

        // Подменяем анимацию ствола (свой Idle + Shoot). DoomWeapon.Shoot() уже дёргает
        // SetTrigger("Shoot") на этом же Animator (он берёт его через GetComponent в Start).
        if (weaponAnimator != null && controller != null)
            weaponAnimator.runtimeAnimatorController = controller;
    }
}
