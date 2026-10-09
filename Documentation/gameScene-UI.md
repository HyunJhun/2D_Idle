# gameScene UI prototype

Open `Assets/Workflow/Scenes/gameScene.unity` in Unity 6000.3.10f1.
The UI contains editable, serialized standard UGUI objects (Image, RawImage, Text, Button, Canvas), without a TMP dependency. The battle viewport shows SpriteRenderer actors; floating damage and skill effects are transient runtime objects.

Portrait reference: 720 × 1280. The reference layout fits inside the display safe area, retaining its proportions on wider and taller screens. Wide screens have dark margins.

## Hierarchy naming

| Prefix | Meaning | Example |
|---|---|---|
| CAN | Canvas | CAN_Game |
| PNL | Logical panel | PNL_Growth |
| BG | Background | BG_Quest |
| BTN | Button | BTN_Menu |
| TXT | Text | TXT_Gold |
| IMG | Illustration or icon | IMG_Hero |
| BAR | Progress fill | BAR_Health |
| CAM | Camera | CAM_Main |
| SYS | System object | SYS_EventSystem |

Top: player, currencies, experience, menu. Center: stage, battle illustration, boss, AFK reward and quest. Bottom: skill bar, auto mode, stat upgrades and six navigation tabs.

Play mode: navigation changes the content panel; upgrades spend gold and update levels and battle stats. AUTO controls basic attacks. Tap the battle viewport to attack manually. Five skill buttons now deal damage with cooldowns; Frost freezes enemy attacks and Rush also heals. Monsters approach before attacking, animate their attacks and deaths, reward gold and restore 15% player health on defeat; entering a stage restores full health. Menu, AFK reward and quest detail buttons still show preview messages. Inventory, purchases and persistence are not implemented. HUD power remains illustrative. Interface copy is English for this initial layout.

See [the playable demo guide](gameScene-playable-guide.md) for controls, character choices and verification.

## Sample stage progression

- Two chapters, five sections each: `1-1` through `1-5`, then `2-1` through `2-5`.
- Normal sections require three monster kills. The next section starts automatically.
- `1-5` and `2-5` always contain one boss with a 45-second deadline.
- Timeout returns to that chapter's section 4 (`1-4` / `2-4`) and enables repeat hunting. Clearing repeat-hunt waves does not automatically re-enter the boss.
- Press `BTN_Boss` (RETRY BOSS) to challenge again with full boss health and a fresh 45 seconds. The button is locked during normal progression and boss fights.
- Clearing `1-5` advances to `2-1`. Clearing `2-5` marks the sample complete and returns to `2-4` repeat hunting; boss replays remain available.
- Player defeat restarts the current normal section; defeat during a boss returns to section 4 repeat hunting.
- The deadline uses game time and continues while AUTO attacks are off or menu tabs are open. At the deadline, timeout is processed before an attack at that same time.

Select `Assets/Workflow/Data/StageCatalog.asset` to tune monster HP, ATK, rewards, kill targets and the boss deadline in the Inspector. Defaults increase both HP and ATK at every section, including the transition from a boss to the following chapter. `SYS_StageBattle` contains the controller and HUD references. Normal enemies and bosses use different sprites.

All 26 buttons have an Inspector-visible `UIEventLogger` listener that writes `<Feature> event action`, for example `Menu event action`, `UpgradeAttack100 event action`, `SkillSlash event action`, `DebugResetStats event action`, and `Boss event action`. Stage/combat events include `StageEnter 1-1 event action`, `MonsterDefeated 1-1 event action`, `BossStart 1-5 event action`, `BossTimeout 1-5 event action`, `FarmingStart 1-4 event action`, and `BossClear 1-5 event action`.

Each growth row offers +1, +10 and +100 with the full batch price shown. Pressing a growth button executes once immediately; holding for 2 seconds repeats that same onClick event every 0.25 seconds using unscaled time. Release does not add a second action. Pointer exit, drag, loss of focus, app pause, disabling the button or closing the panel stops repetition. Keyboard submit still executes once. Insufficient gold rejects the whole batch. Critical chance is capped at level 200 (100%); batches that exceed this limit are rejected without spending gold.

`BTN_DebugResetStats` is available in the Unity Editor and Development Builds. It restores the existing starting levels (ATK 120, HP 80, CRIT 25), derived stats and full player HP, and cancels held growth inputs. Currency, stage progression and skill cooldowns are preserved. Release builds hide the button and compile out the reset action.

Workflow textures and PNG sprites referenced by gameScene have persistent Point filtering, no mipmaps and uncompressed default import settings. Aseprite importers retain Point filtering. Run `Tools > Idle UI > Apply growth buttons and Point sprites` to apply these settings to newly added assets. The import inventory is `Documentation/gameScene-point-import.txt`; `GameSceneGrowthTests.Run` checks import settings and exercises actual pointer input in Play mode.

Use `Tools > Idle UI > Apply stage progression` to attach/update the stage system without rebuilding the UI. Batch verification entry point: `GameSceneStagesEditor.ApplyAndVerify`. It verifies progression, deadline boundaries, both boss fallbacks, repeated farming, retry resets, completion, combat integration and serialized button logging, then renders `Documentation/gameScene-preview.png`.

Combat characters and terrain now use the project's `Workflow/Sprites` assets. Character animation sets live in `Workflow/Data/Characters`; the two actor prefabs live in `Workflow/Prefabs/Battle`. The previous Layer Lab character UI images are disabled. Reference genre structure: Blade Idle and Slayer Legend; no artwork copied from either game.

References: [Blade Idle](https://play.google.com/store/apps/details?id=com.mobirix.mbbi), [Slayer Legend](https://play.google.com/store/apps/details?id=com.gear2.growslayer), [Slayer Legend UI guide](https://slayerlegend.wiki/getting-started/ui-guide).

`Tools > Idle UI > Create gameScene` creates the scene only when it does not already exist, preserving subsequent hand edits. Existing MainGame and build scene ordering are preserved. To include this screen in a player build, add gameScene to the desired Build Profile.
