# Hooks: Stars (辉星), Void Form, Character Select, Star-cost preview, PowerCmd

All paths relative to `research/src_full/` unless noted. Line numbers refer to the decompiled files.

---

## 1. Regent Stars resource

### Storage
- `MegaCrit.Sts2.Core.Entities.Players.Player.PlayerCombatState` (`PlayerCombatState?`, `Player.cs:114`; new instance per combat at `Player.cs:799`).
- `MegaCrit.Sts2.Core.Entities.Players.PlayerCombatState.Stars` (`int`, get/set) — `PlayerCombatState.cs:103-119`.
  The setter (when value changes) calls `CombatManager.Instance.History.StarsModified(...)` and then fires
  `public event Action<int,int>? StarsChanged` (oldStars, newStars) — `PlayerCombatState.cs:127`.
- Path from a creature: `creature.Player?.PlayerCombatState?.Stars`.

### Gain
- `MegaCrit.Sts2.Core.Commands.PlayerCmd.GainStars(decimal amount, Player player)` → `static async Task` — `PlayerCmd.cs:89`.
  Checks `Hook.ShouldGainStars(...)` (`Hook.cs:2339`), calls `player.PlayerCombatState.GainStars(amount)` (`PlayerCombatState.cs:220`), then `await Hook.AfterStarsGained(combatState, (int)amount, player)` (`Hook.cs:1192`).
  Already patched by mod: `mod/src/Patches/GameplayPatches.cs:91` (`GainStarsPatch`).
  Callers: relics (`DivineRight`, `DivineDestiny`, `LunarPastry`), `StarPotion`, cards, `StarsConsoleCmd`.

### Spend / lose
- Card play path: `PlayCardAction` (`MegaCrit.Sts2.Core.GameActions/PlayCardAction.cs:92`) → `CardModel.SpendResources()` → `public async Task<(int energy,int stars)>` (`CardModel.cs:1807`)
  → `private async Task SpendStars(int amount)` (`CardModel.cs:1836`): sets `LastStarsSpent`, calls `Owner.PlayerCombatState.LoseStars(amount)` then `await Hook.AfterStarsSpent(combatState, amount, Owner)` (`Hook.cs:1204`).
  Note: `SpendResources` also converts excess energy cost to stars (×2) if `Hook.ShouldPayExcessEnergyCostWithStars` (`Hook.cs:2354`).
- `PlayerCmd.LoseStars(decimal amount, Player player)` → `static Task` (not async) — `PlayerCmd.cs:103`.
- `PlayerCmd.SetStars(decimal amount, Player player)` → `static async Task` — `PlayerCmd.cs:118` (delegates to Gain/Lose).
- Low level: `PlayerCombatState.LoseStars(decimal amount)` (void, `PlayerCombatState.cs:211`), `PlayerCombatState.GainStars(decimal amount)` (void, `:220`). Both just assign `Stars`.

### Events / hooks on change
- `PlayerCombatState.StarsChanged` (`Action<int,int>`, old,new) — fires synchronously for ALL changes (gain, spend, lose, set). Subscribers: `NStarCounter`, `NMultiplayerPlayerState` (`Nodes.Multiplayer/NMultiplayerPlayerState.cs:515`), `CombatStateTracker` (`Combat/CombatStateTracker.cs:119` → `CombatStateChanged` event, line 29).
- Model hooks (virtual on `AbstractModel`): `AfterStarsSpent(int amount, Player spender)` (`AbstractModel.cs:1175`), `AfterStarsGained(int amount, Player gainer)` (`:1186`), `ShouldGainStars(decimal, Player)` (`:2315`), `TryModifyStarCost(CardModel, decimal, out decimal)` (`:2078`).
- Static dispatchers: `Hook.AfterStarsGained(ICombatState,int,Player)` `Hook.cs:1192`, `Hook.AfterStarsSpent(ICombatState,int,Player)` `Hook.cs:1204`, `Hook.ModifyStarCost(ICombatState, CardModel, decimal)` `Hook.cs:2023`.
- **Recommended single Harmony point (sync, covers everything):** postfix the property setter
  `[HarmonyPatch(typeof(PlayerCombatState), nameof(PlayerCombatState.Stars), MethodType.Setter)]` and compare with a prefix-captured old value; or simply subscribe to `StarsChanged` per combat. Harmony postfixes on the `async Task` methods (`GainStars`, `SpendStars`, `Hook.*`) run when the Task is *returned* (first await), not at completion.
- To distinguish "spent on card play" from other losses: postfix `CardModel.SpendResources` (`__instance` = card) or private `CardModel.SpendStars(int amount)` — the `LoseStars` inside runs synchronously before its first await, so a postfix already sees the decreased count.

### Card star cost API (`MegaCrit.Sts2.Core.Models.CardModel`)
| Member | Line |
|---|---|
| `public virtual int CanonicalStarCost => -1` (−1 = no star cost) | 410 |
| `public int BaseStarCost { get; set; }` (set fires `StarCostChanged`) | 412 |
| `public bool WasStarCostJustUpgraded` | 439 |
| `public TemporaryCardCost? TemporaryStarCost` | 441 |
| `public virtual int CurrentStarCost` (base + temporary overrides) | 451 |
| `public virtual bool HasStarCostX => false` | 468 |
| `public int LastStarsSpent { get; set; }` | 478 |
| `public event Action? StarCostChanged` | 1075 |
| `SetStarCostUntilPlayed/ThisTurn/ThisCombat(int)` | 1279-1291 |
| `protected void UpgradeStarCostBy(int addend)` (no vanilla card uses it) | 1311 |
| **`public int GetStarCostWithModifiers()`** — final cost incl. hooks (VoidForm→0 etc.); for X returns current stars | 1556 |
| `public async Task<(int,int)> SpendResources()` | 1807 |
| `private async Task SpendStars(int amount)` | 1836 |
Affordability: `PlayerCombatState.HasEnoughResourcesFor(CardModel, out UnplayableReason)` (`PlayerCombatState.cs:190`) → `UnplayableReason.StarCostTooHigh`.
Color helper: `MegaCrit.Sts2.Core.Helpers.Models.CardCostHelper.GetStarCostColor(CardModel, ICombatState?)` (`Helpers.Models/CardCostHelper.cs:52`).
Card UI: `NCard` star label/icon updated at `Nodes.Cards/NCard.cs:1061` using `GetStarCostWithModifiers()`.

### Regent cards with star cost (`Models.Cards/*.cs`, `CanonicalStarCost`; energy = first `base(` arg). No card changes star cost on upgrade.
| Card | Stars | Energy |
|---|---|---|
| SevenStars | 7 | 2 |
| DecisionsDecisions | 6 | 0 |
| Comet | 5 | 0 |
| NeutronAegis | 5 | 1 |
| RoyalGamble | 5 | 0 |
| TheSmith | 4 | 1 |
| Devastate | 4 | 1 |
| Reflect | 3 | 1 |
| DyingStar | 3 | 1 |
| AstralPulse | 3 | 0 |
| GammaBlast | 3 | 0 |
| TheSealedThrone | 3 | 1 |
| Quasar | 2 | 0 |
| ParticleWall | 2 | 0 |
| Constellation | 2 | 0 |
| Alignment | 2 | 0 |
| MeteorShower | 2 | 0 |
| FallingStar | 2 | 0 |
| Resonance | 2 | 1 |
| GuidingStar | 1 | 1 |
| CrescentSpear | 1 | 1 |
| CloakOfStars | 1 | 0 |
| Stardust | X (`HasStarCostX`) | 0 |

### Vanilla star UI + VFX
- Node: `MegaCrit.Sts2.Core.Nodes.Combat.NStarCounter : Control` (`Nodes.Combat/NStarCounter.cs`). Lives in `res://scenes/combat/combat_ui.tscn` as `%StarCounter`; `NCombatUi._Ready` gets it (`NCombatUi.cs:286`), `NCombatUi.Activate(CombatState)` calls `_starCounter.Initialize(me)` and **reparents it under the energy counter** (`NCombatUi.cs:314-330`). Regent (`ShouldAlwaysShowStarCounter => true`, `Models.Characters/Regent.cs:36`) moves energy counter to (100,806).
- Children: `%CountLabel` (MegaRichTextLabel), `%RotationLayers` (rotated in `_Process`), `Icon` (HSV ShaderMaterial).
- Methods: `Initialize(Player)` :212, `private void OnStarsChanged(int,int)` :229, `_Process(double)` :235 (smooth-damps label), **`private void UpdateStarCount(int oldCount, int newCount)` :249**, `SetStarCountText(int)` :270, `RefreshVisibility()` :295.
- Gain VFX: in `UpdateStarCount` when `newCount > oldCount` it tweens HSV V 2→1 and instantiates `res://scenes/vfx/star_gain_vfx.tscn` (`_starGainVfxPath`, :147), `AddChildSafely` + `MoveChildSafely(node,0)` and positions at `Size/2`. **No VFX on spend** (just snaps label).
- To suppress the gain VFX: postfix `NStarCounter.UpdateStarCount` and, if `newCount > oldCount`, free `__instance.GetChild(0)` (the freshly added Node2D); or prefix-return-false (but then also re-implement the label/HSV update).

---

## 2. Void Form

- Card: `MegaCrit.Sts2.Core.Models.Cards.VoidForm` (`Models.Cards/VoidForm.cs`), 3 energy, Power, Rare. `OnPlay` (:22): `CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", ...)`, `PowerCmd.Apply<VoidFormPower>(ctx, Owner.Creature, 2, Owner.Creature, this)` (:25), then `PlayerCmd.EndTurn`.
- Power: `MegaCrit.Sts2.Core.Models.Powers.VoidFormPower : PowerModel` (`Models.Powers/VoidFormPower.cs`). Effect: first `Amount` cards each turn cost 0 energy and 0 stars (`TryModifyEnergyCostInCombatLate`, `TryModifyStarCost` :92).
  - field `private NVoidFormVfx? _vfx` (:21)
  - `public override Task AfterApplied(Creature? applier, CardModel? cardSource)` (:48) → `Vfx = NVoidFormVfx.Create(Owner)` (sync, returns `Task.CompletedTask`)
  - `public override Task AfterRemoved(Creature oldOwner)` (:54) → `Vfx?.SetActive(false)` (vfx is **not freed**)
  - `AfterCardPlayed` (:103) → `SetActive(false)` once free plays used up; `BeforeSideTurnStart` (:116) → `SetActive(true)` each turn.
- VFX node: `MegaCrit.Sts2.Core.Nodes.Vfx.Forms.NVoidFormVfx : NFormVfx` (`Nodes.Vfx.Forms/NVoidFormVfx.cs`), scene `res://scenes/vfx/forms/void/vfx_void_form_idle_vfx.tscn` (:117; also `vfx_void_form_spike_pivot.tscn`). Exports: `_swords` (Node2D[]), `_glow`, `_powerActiveParticles`, `_boneFollower` (NSpineSpriteBoneFollower, uses `_regentBoneName`), `_valueRamp`.
  - `public static NVoidFormVfx? Create(Creature target)` (:155): `NCombatRoom.Instance.GetCreatureNode(target)` → instantiate → `Initialize(target.Player)` → **`creatureNode.Visuals.AddFormVfx(vfx)`**.
  - `public override void SetActive(bool isActive)` (:197) toggles particles + value ramp (swords shrink/glow fade).
- Base: `NFormVfx : Node2D` (`Nodes.Vfx.Forms/NFormVfx.cs`): `Initialize(Player)` :71, `SetActive(bool)` :88. Siblings: `NDemonFormVfx`, `NEchoFormVfx`, `NReaperFormVfx`, `NSerpentFormVfx` (same pattern).
- Container: `MegaCrit.Sts2.Core.Nodes.Combat.NCreatureVisuals.FormVfxHolder` (`Control?`, `%FormVfx`, `NCreatureVisuals.cs:226/255`). In `research/regent_assets/scenes/creature_visuals/regent.tscn:243` `FormVfx` is the first child of the root, **before `Visuals` (SpineSprite) → drawn behind the character**.
  - `public void AddFormVfx(NFormVfx formVfx)` (`NCreatureVisuals.cs:274`): `FormVfxHolder.FreeChildren()` then add, position zero (only one form vfx at a time).
  - `public void RemoveFormVfx()` (:284): frees children; only called from `NCreature` death (`NCreature.cs:954`). Otherwise the vfx lives until the combat room is torn down.
- Generic power visuals (one-shot, not persistent): `NCreature.OnPowerIncreased` → `NPowerAppliedVfx`/`NPowerAppliedBuffVfx` into `NCombatRoom.Instance.CombatVfxContainer` (`NCreature.cs:800-838`), `OnPowerRemoved` → `NPowerRemovedVfx` (:841), `OnPowerFlashed` → `NPowerFlashVfx` (:854). Persistent per-power visuals exist only for the *Form* powers via `FormVfxHolder`.

### Harmony targets
- (a) Detect VoidForm applied to our player: **postfix `VoidFormPower.AfterApplied(Creature? applier, CardModel? cardSource)`** — `__instance.Owner` is the creature (`Owner.Player`, `Owner.Player.Character is Regent`). Not async, so postfix runs after the vfx exists. Removal: postfix `VoidFormPower.AfterRemoved(Creature oldOwner)`. Per-turn active state: postfix `NVoidFormVfx.SetActive(bool isActive)` (covers turn start + used-up + removal).
- (b) Hide/replace vanilla visual: **postfix `NVoidFormVfx.Create(Creature target)`** with `ref NVoidFormVfx __result` → `__result.Visible = false` (keeps power logic intact; `VoidFormPower.Vfx` getter checks `IsValid()`, so `QueueFree` is also safe). Alternative: postfix `NCreatureVisuals.AddFormVfx(NFormVfx formVfx)` and filter `formVfx is NVoidFormVfx`. Avoid prefix-skipping `AddFormVfx` (node would be orphaned/leaked).

---

## 3. Character select

- `MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect.NCharacterSelectScreen` (`Nodes.Screens.CharacterSelect/NCharacterSelectScreen.cs`), implements `ICharacterSelectButtonDelegate`.
- **`public void SelectCharacter(NCharacterSelectButton charSelectButton, CharacterModel characterModel)`** — line 812. Only caller: `NCharacterSelectButton.Select()` (`NCharacterSelectButton.cs:508`), which is guarded by `if (!_isSelected)` and is connected to `FocusEntered` (`:353`) — i.e. click/hover-focus/controller navigation. Re-clicking the already-selected button does nothing.
- What it does: plays `characterModel.CharacterSelectSfx` (not for random), screen shake, info panel tween (from x−300, 0.5s Expo Out), then **removes & `QueueFreeSafely` every child of `_bgContainer`** (`"AnimatedBg"` node, field :337, fetched :413) and, if not locked, **instantiates a fresh** `PreloadManager.Cache.GetScene(characterModel.CharacterSelectBg)` named `"{Id.Entry}_bg"` and adds it. No bg cross-fade tween — it's an instant swap. Then sets texts, `_lobby.SetLocalCharacter`, `_ascensionPanel.AnimIn()`, deselects other buttons.
- `CharacterModel.CharacterSelectBg` = `res://scenes/screens/char_select/char_select_bg_{id}.tscn` (`CharacterModel.cs:161`) → Regent: `char_select_bg_regent.tscn` (script `NRegentCharacterSelectBg`).
- Therefore **`NRegentCharacterSelectBg._Ready` runs every time Regent is selected** (new instance per selection; old one freed). The mod's existing `CharSelectBgPatch` (`mod/src/Patches/ScenePatches.cs:27`) therefore already fires on each Regent selection.
- Default on open: `OnSubmenuOpened()` (:571) resets all buttons (`Reset()` → `Deselect()`) and calls `_charButtonContainer.GetChild<NCharacterSelectButton>(0).Select()` (:589) → **Ironclad** (order from `ModelDb.AllCharacters`: Ironclad, Silent, Regent, Necrobinder, Defect; `ModelDb.cs:145`). Regent is never the default, except after a pending unlock animation (`PlayUnlockCharacterAnimation`, :610-632 → `button.Select()`).
- Recommended postfix:
  ```csharp
  [HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.SelectCharacter))]
  static void Postfix(NCharacterSelectButton charSelectButton, CharacterModel characterModel)
  { if (characterModel is Regent && !charSelectButton.IsLocked) ... }
  ```
  (Postfix runs after the bg has been added, so the bg `_Ready` has already executed.) `NCustomRunScreen.SelectCharacter` (`Nodes.Screens.CustomRun/NCustomRunScreen.cs:631`) is a separate implementation without a bg.
- `NCharacterSelectScreenBg` (`NCharacterSelectScreenBg.cs`) only rescales on window size change.

---

## 4. Star cost preview during drag/targeting

- No dedicated hook; use `CardModel.GetStarCostWithModifiers()` (`CardModel.cs:1556`) — includes temporary costs and `Hook.ModifyStarCost` (VoidForm, BrilliantScarf …) while the card is in a combat pile. `HasStarCostX` → returns current stars. `CanonicalStarCost < 0` / result `< 0` means no star cost. Excess-energy-as-stars (×2) is only applied in `SpendResources`/`HasEnoughResourcesFor`.
- Drag entry points: `NCardPlay` (abstract, `Nodes.Combat/NCardPlay.cs`) — `public abstract void Start()` (:191), concrete `NMouseCardPlay.Start()` (`NMouseCardPlay.cs:233`), `NControllerCardPlay.Start()` (`NControllerCardPlay.cs:177`). Card: `__instance.Holder.CardModel` (`NHandCardHolder` → `NCardHolder.CardModel`, `NCardHolder.cs:242`); `NCardPlay.Card` is protected (:167). End: `Cleanup(bool isFinished)` (:277, already patched by mod), `CancelPlayCard()` (:263), `TryPlayCard(Creature?)` (:193). Hover target: `NCardPlay.OnCreatureHover` (:288) / mod's `NTargetManager` patches.
- Also `CardModel.StarCostChanged` event (:1075) if a live preview should refresh.

---

## 5. PowerCmd (`MegaCrit.Sts2.Core.Commands.PowerCmd`, `Commands/PowerCmd.cs`)
- `static async Task<IReadOnlyList<T>> Apply<T>(PlayerChoiceContext, IEnumerable<Creature>? targets, decimal amount, Creature? applier, CardModel? cardSource, bool silent=false)` :36
- `static async Task<T?> Apply<T>(PlayerChoiceContext choiceContext, Creature target, decimal amount, Creature? applier, CardModel? cardSource, bool silent=false)` :71
- **`static async Task Apply(PlayerChoiceContext choiceContext, PowerModel power, Creature target, decimal amount, Creature? applier, CardModel? cardSource, bool silent=false)`** :105 — non-generic, easiest to patch; for a new instance: `power.ApplyInternal` → `await power.AfterApplied(...)` → `Hook.AfterPowerAmountChanged`. Stacking existing power goes to `ModifyAmount` (:219) instead (AfterApplied NOT called again).
- `static async Task<int> ModifyAmount(PlayerChoiceContext, PowerModel power, decimal offset, Creature? applier, CardModel? cardSource, bool silent=false)` :219
- `static async Task Remove<T>(Creature)` :282, `static async Task Remove(PowerModel? power)` :291 → `power.RemoveInternal()` → wait → `await power.AfterRemoved(power.Owner)`.
- Caveat: all of these are `async`; a Harmony postfix fires at the first `await`, not on completion. Prefer postfixing the power's own sync overrides (`VoidFormPower.AfterApplied/AfterRemoved`) or Creature events.
- Creature events (`Entities.Creatures/Creature.cs:335-341`): `PowerApplied (Action<PowerModel>)`, `PowerIncreased (Action<PowerModel,int,bool>)`, `PowerDecreased (Action<PowerModel,bool>)`, `PowerRemoved (Action<PowerModel>)`. Query: `Creature.Powers` :327, `HasPower<T>()` :561, `GetPower<T>()` :571.
- `PowerModel.ApplyInternal(Creature owner, decimal amount, bool silent)` (`Models/PowerModel.cs:572`, sync) and `RemoveInternal()` (:583, sync, fires `Removed` event) are also viable sync patch points.
