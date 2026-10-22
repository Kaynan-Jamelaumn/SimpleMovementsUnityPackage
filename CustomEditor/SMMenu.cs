/// <summary>
/// The layout of the Tools ▸ SimpleMovements menu, in one place: the root path and the priority of every section.
/// Unity sorts menu items by priority and draws a separator wherever two neighbours differ by more than 10, so
/// sections that share a hundred (200, 201, 202...) form one block and blocks are separated:
/// <code>
/// Project Validator...          ┐ checking and fixing
/// Check Everything...           │
/// Scene and Prefab Setup ▸      │  one page per scene / prefab the game needs, with Fix buttons
/// Validate ▸                    │  one check per area (results in the Console / Project Validator)
/// Auto-Assign ▸                 ┘  fill empty references automatically
/// ─────────────
/// Project Setup ▸               ┐ project assets and input
/// Input ▸                       ┘
/// ─────────────
/// Player ▸ Inventory ▸ Mobs ▸ NPC ▸ Social ▸ Alchemy ▸ characters and items
/// ─────────────
/// Hub World ▸ Weather ▸ Dungeon ▸ Scene ▸              the world and scenes
/// ─────────────
/// Network ▸
/// ─────────────
/// Legacy ▸ Help ▸
/// </code>
/// Where things go: a tool that creates or changes one kind of thing lives in that thing's section (Player ▸ for
/// players, Weather ▸ for the weather...); Validate ▸ only checks (plus the one-click fix of the scene setup next to its
/// check); Auto-Assign ▸ only fills empty references; Scene and Prefab Setup ▸ checks and completes a whole scene or
/// prefab. Each section ends with its Help entry when it has one.
/// A section's items use its priority plus a small offset; offsets 10 apart make separators inside the section.
/// </summary>
public static class SMMenu
{
    public const string Root = "Tools/SimpleMovements/";

    // ---------------------------------------------------------------- checking and fixing
    public const int Top = 0;
    public const int Setup = 2;
    public const int Validate = 3;
    public const int AutoAssign = 4;

    // ---------------------------------------------------------------- project
    public const int ProjectSetup = 100;
    public const int Input = 101;

    // ---------------------------------------------------------------- characters and items
    public const int Player = 200;
    public const int Inventory = 201;
    public const int Mobs = 202;
    public const int NPC = 203;
    public const int Social = 204;
    public const int Alchemy = 205;

    // ---------------------------------------------------------------- world
    public const int HubWorld = 300;
    public const int Weather = 301;
    public const int Dungeon = 302;
    public const int Scene = 303;
    public const int Minigames = 304;

    // ---------------------------------------------------------------- multiplayer
    public const int Network = 400;

    // ---------------------------------------------------------------- other
    public const int Legacy = 500;
    public const int Help = 501;
}
