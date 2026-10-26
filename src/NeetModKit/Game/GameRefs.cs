using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeetModKit.Game;

/// <summary>
/// Null-safe shortcuts to the game's main objects. Everything returns null outside the house
/// scene or before the house finished setting up.
/// </summary>
public static class GameRefs
{
    public const string HouseScene = "NEET_House";

    public static HouseManager House => HouseManager._;

    public static GameObject Player => House != null ? House.player : null;

    public static PlayerStats PlayerStats => Get<PlayerStats>(Player);

    public static Player_Abilities PlayerAbilities => Get<Player_Abilities>(Player);

    public static Player_Movement PlayerMovement => Get<Player_Movement>(Player);

    public static Brain PlayerBrain => Get<Brain>(Player);

    public static Computer Computer => House != null ? House.mainComputer : null;

    public static AnimeCardGenerator CardGame => Computer != null ? Computer.animeCardGenerator : null;

    public static string SceneName => SceneManager.GetActiveScene().name;

    public static bool InHouse => House != null && SceneName == HouseScene;

    private static T Get<T>(GameObject go) where T : Component => go != null ? go.GetComponent<T>() : null;
}
