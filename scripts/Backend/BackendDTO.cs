using System;

[Serializable] public class EarnReq { public int amount; }
[Serializable] public class BuyReq { public string item_id; }
[Serializable] public class ScreenReq { public string screen; }

[Serializable] public class MoneyResp { public int money; }

[Serializable] public class MeResp
{
    public string username;
    public int money;
    public int current_level;
    public int highest_level;
    public string[] items;
    public string equipped_weapon;
    public float weapon_damage;
    public float weapon_fire_rate;
    public float weapon_range;
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

[Serializable] public class ClueDTO
{
    public string id;
    public string emoji;
    public string name;
    public string text;
    public string type;
    public string phrase;
}

[Serializable] public class CluesResp
{
    public int level;
    public ClueDTO[] clues;
}

[Serializable] public class ChatQuestionResp
{
    public int id;
    public string text;
    public int[] options;
    public string status;
}

[Serializable] public class ChatStateResp
{
    public int id;
    public string status;
}

[Serializable] public class IntroStatusResp { public bool done; }

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
    public string status;
    public string game_token;
    public string username;
}
