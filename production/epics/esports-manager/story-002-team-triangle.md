# Story 002: 战队三角和默契（按 numbers.md v2 重写）

> **Epic**: 战队经理人 MVP
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Estimate**: [fill before starting]
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-16

## Context

**GDD**: `design/game-brief.md` (formulas and tables: `design/numbers.md` §4.2, §4.3, §4.8, §4.9 step 1, §4.10)
**Requirement**: Brief MVP feature 2（战队三角加默契）

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.6.5f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (Player goal & fail state + MVP feature 2) and `design/numbers.md`, scoped to this story:*

- [ ] 阵容：战队有名单和首发（分路 → 选手），5 个分路各一个首发；首发必须在名单里，同一个人不能同时首发两个分路；换人、互换位置都有对应方法，非法操作抛出明确错误
- [ ] 实力（§4.9 第 1 步）：每名首发的**有效评分** = 分路评分 × 心情系数 × 压力系数（坚毅减轻压力的影响）；实力 = 各分路加权平均 − 短板惩罚；空位的有效评分为 0；这是纯函数，之后由任务 004 复用
- [ ] 两两熟悉度（§4.8）：按**选手 ID** 记录每一对选手的熟悉度 F；每个赛段两人同时首发 `+f_seg`（两人中有一人是温和则乘温和系数）；每赢一场两人同时在场 `+f_win`；不同时首发的赛段按 `f_decay` 衰减；封顶 `F_max`，不低于 0
- [ ] 默契：一对选手的默契 = `F × (1 + κ × H)`，限制在 0–100（H 为任务 008 的相性）；**战队默契 = 首发五人两两之间 10 对默契的平均值**
- [ ] 换人自然掉默契，没有额外的扣分规则：新首发与其他人的熟悉度是 0，所以战队默契自动下降；"换人再换回来"不会被重复扣分（有测试）
- [ ] 选手人气（§4.2）：每个赛段向目标值靠拢 `ΔPop = α × (目标 − 人气) × R(0.15)`；目标值由俱乐部人气、主分路评分、颜值、直播热度、近期胜率、商务热度加权得出（直播和商务热度由调用方传入，范围 0–100）；人气钳制在 0–100；没有直播和商务时人气会向较低的目标值下滑
- [ ] 俱乐部人气（§4.3）：同样向目标值靠拢 `ΔClubPop = β × (目标 − 俱乐部人气) × R(0.1)`，目标值由首发平均人气、近期胜率、赛季名次分加权得出
- [ ] 每赛段收支（§4.10）：收入 = 比赛奖金 + 直播收入 + 商务分成 + 赞助 + 事件收入；支出 = 全队年薪之和 ÷ **每年赛段数** + 转会费 + 事件支出。**每年赛段数由调用方传入**，不放进战队配置；金额用整数；资金可以为负，本任务不判定破产
- [ ] 近期成绩：战队只保留最近 N 场（配置）的胜负记录，用来算近期胜率；没有比赛记录时使用配置的默认胜率
- [ ] 配置规范化：新配置 `Assets/Data/team_config.json`，遵守 `.claude/rules/data-files.md`（小写文件名、`snake_case` 键、内部 DTO）；**复用任务 008 的严格检查器**，缺键、错类型、多余键、重复键、无穷大都报错；所有数值放配置并有范围校验；数值含义写在代码文档注释里

---

## Implementation Notes

- 纯 C#，依赖任务 008 的 `Player`（ID、性格、心情、压力、`CompatibilityWith`、`PairFamiliarityMultiplier`、`GetLaneScore`）；随机数通过注入的随机源
- 所有数值放配置（实力的分路权重、短板系数 λ、压力系数 κ_s、熟悉度参数、κ、人气目标值权重和 α、β、近期成绩窗口），不写死
- 代码放 `Assets/Scripts/Team/`，命名空间用 `EsportsManager.Teams`（`Team` 作为命名空间会和类名 `Team` 冲突）
- 心情和压力对**单局发挥的随机波动**的影响（§4.9 第 4 步）属于任务 004，不在本任务
- 本任务提供给后续任务使用的接口：首发平均心情和压力、战队默契、实力、近期胜率
- 写代码前查 `docs/engine-reference/unity/`（Unity 6.6 比训练数据新）

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003：训练、直播、商务、休息等行动本身
- Story 004：比赛模拟、综合战力、单局波动
- Story 005：赛季日历、转会、拍卖、对手俱乐部
- Story 006：选手事件
- Story 007：破产判定

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

*Governed by `qa.level`: at `qa.level: minimal` tests are **waived** (advisory), but a Visual/Feel or UI story's retained screenshot is not.*

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/Team/` — NUnit tests (advisory at `qa.level: minimal`; the user has asked for tests anyway)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (Complete), Story 008 (Complete)
- Unlocks: Story 003

---

## Completion Notes
**Completed**: 2026-10-16
**Criteria**: 10/10 passing (no deferred items)
**Deviations**: (1) Doc-comment-only edit to `Assets/Scripts/Players/Player.cs` (outside story scope, user-approved) to replace stale v1 trait multipliers. (2) Empty lineup slots count as 0 in strength and chemistry but are skipped in average mood/stress and starter popularity — Story 004 must decide whether a match can start with an empty slot (recorded in `design/numbers.md` §6).
**Test Evidence**: Logic: `Assets/Tests/EditMode/Team/` (TeamTests, TeamConfigTests, TeamTestFactory) — 91 new tests; whole suite 313 tests, 313 passed, 0 failed (`test-results/editmode.xml`, from the /dev-story run; /story-done did not re-run them). Waived at `qa.level: minimal`, written anyway at the user's request.
**Code Review**: Skipped (solo mode); unity-specialist review run, SHOULD-FIX items fixed.
**Run result**: N/A — pure data/logic layer, no player-visible surface (first UI is Story 003)
**Carry-forward**: income structure target (sponsorship 40 / stream 25 / business 20 / prize 15 % of salaries) and livestream retune once Story 003 fixes action points; Story 003 owns stream/business count → heat mapping; Story 004 needs a TeamStrength overload for opponents with fixed mood/stress; Story 005 owns the calendar (`segments_per_year`, `SegmentIndexInYear < S`, transfer window is not a segment).
