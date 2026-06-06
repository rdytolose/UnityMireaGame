// DTO-классы для JsonUtility. Имена полей ДОЛЖНЫ совпадать с JSON бэкенда.
using System;

[Serializable] public class EarnReq { public int amount; }
[Serializable] public class BuyReq { public string item_id; }
[Serializable] public class ScreenReq { public string screen; } // wait|clues|shop|chat|pause

[Serializable] public class MoneyResp { public int money; }

[Serializable] public class MeResp
{
    public string username;
    public int money;
    public int current_level;
    public int highest_level;
    public string[] items;   // купленное снаряжение (id из каталога), отдаёт /api/me
    public string equipped_weapon;  // активный ствол, выбранный на сайте
    public float weapon_damage;     // эффективный урон активного оружия (из админки)
    public float weapon_fire_rate;  // эффективная скорострельность (пауза между выстрелами)
    public float weapon_range;      // дальность луча: ближний бой (топор) = маленькое значение
}

[Serializable] public class CompleteLevelResp
{
    public int current_level;
    public int highest_level;
    public bool finished_game;
    public LevelConfig next_level_config;
}

[Serializable] public class ShooterCfg
{
    public float survival_time;
    public float enemy_health_mult;
    public float enemy_speed_mult;
    public float enemy_damage_mult;
    public int max_enemies;
    public float spawn_min_delay;
    public float spawn_max_delay;
}

[Serializable] public class ShopCfg
{
    public int clue_types;
    public float bad_chance;
    public int min_order;
    public int max_order;
    public float customer_move_duration;
}

[Serializable] public class LevelConfig
{
    public int level;
    public ShooterCfg shooter;
    public ShopCfg shop;
}

// Clues (приметы плохого клиента)
[Serializable] public class ClueDTO
{
    public string id;
    public string emoji;
    public string name;
    public string text;
    public string type;     // "object" | "text"
    public string phrase;   // только для type="text": фраза в реплику клиента
}

[Serializable] public class CluesResp
{
    public int level;
    public ClueDTO[] clues;
}

// Chat math (математика в чате doom)
[Serializable] public class ChatQuestionResp
{
    public int id;
    public string text;
    public int[] options;
    public string status;   // idle | pending | correct | wrong
}

[Serializable] public class ChatStateResp
{
    public int id;
    public string status;
}

// Intro (вступительный чат на телефоне): игра ждёт его окончания перед стартом doom
[Serializable] public class IntroStatusResp { public bool done; }

// Pairing
[Serializable] public class PairStartResp
{
    public string code;
    public string qr_url;
    public string qr_png_base64;
    public string poll_secret;
    public int expires_in;
}

[Serializable] public class PairStatusResp
{
    public string status;       // pending | linked | consumed | expired
    public string game_token;
    public string username;
}
