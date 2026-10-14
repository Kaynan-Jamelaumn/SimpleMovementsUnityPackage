/// <summary>
/// The layout of the Tools ▸ SimpleMovements menu, in one place: the root path and the priority of every section.
/// Unity sorts menu items by priority and draws a separator wherever two neighbours differ by more than 10, so
/// sections that share a hundred (200, 201, 202...) form one block and blocks are separated:
/// <code>
/// Project Validator...          ┐ checking and fixing
/// Setup Checklist...            │
/// Validate ▸                    │
/// Auto-Assign ▸                 ┘
/// ─────────────
/// Project Setup ▸               ┐ project assets and input
/// Input ▸                       ┘
/// ─────────────
/// Player ▸ Inventory ▸ Mobs ▸ NPC ▸ Social ▸           characters and items
/// ─────────────
/// Hub World ▸ Weather ▸ Dungeon ▸ Scene ▸              the world and scenes
/// ─────────────
/// Network ▸
/// ─────────────
/// Legacy ▸ Help ▸
/// </code>
/// A section's items use its priority plus a small offset; offsets 10 apart make separators inside the section.
/// </summary>
public static class SMMenu
{
    public const string Root = "Tools/SimpleMovements/";

    // ---------------------------------------------------------------- checking and fixing
    public const int Top = 0;
    public const int Validate = 2;
    public const int AutoAssign = 3;

    // ---------------------------------------------------------------- project
    public const int ProjectSetup = 100;
    public const int Input = 101;

    // ---------------------------------------------------------------- characters and items
    public const int Player = 200;
    public const int Inventory = 201;
    public const int Mobs = 202;
    public const int NPC = 203;
    public const int Social = 204;

    // ---------------------------------------------------------------- world
    public const int HubWorld = 300;
    public const int Weather = 301;
    public const int Dungeon = 302;
    public const int Scene = 303;

    // ---------------------------------------------------------------- multiplayer
    public const int Network = 400;

    // ---------------------------------------------------------------- other
    public const int Legacy = 500;
    public const int Help = 501;
}
