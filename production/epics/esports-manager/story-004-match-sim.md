# Story 004: 比赛模拟、赛历赛制、战报和积分榜

> **Epic**: 战队经理人 MVP
> **Status**: Complete
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: [fill before starting]
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-19

## Context

**GDD**: `design/game-brief.md` (formulas: `design/numbers.md` §4.8, §4.9, §4.10, §4.14, decisions in §6)
**Requirement**: Brief MVP feature 4（比赛模拟）— Build order step 2; playtest feedback 2026-10-19 "不知道训练得如何，也不知道俱乐部的水平如何"

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.6.5f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (MVP feature 4) and `design/numbers.md` v3.3, scoped to this story:*

- [ ] **对手**：联赛有 `opponent_count` 支虚构对手俱乐部（⚙ 初值 7，加玩家共 8 队），阵容由选手池生成；实力按 §4.9 计算，心情和压力固定为 50（§4.14）
- [ ] **赛历与赛制**（numbers.md §4.18，v3.4，2026-10-19 用户决定，取代"每赛段一个系列赛"）：一年 12 赛段；第 3、6 赛段结算后打春季赛、夏季赛（8 队单循环常规赛 BO3 + 前 4 名季后赛 BO5），第 12 赛段结算后打年度总决赛（按两季名次积分取前 4，单败，半决赛 BO5、决赛 BO7）；其余赛段不打比赛；赛事安排、轮次、BO、积分、名额全部配置；同一种子结果相同
- [ ] **赛前与季后赛前**：赛事前选首发和战术（常规赛全程使用），进入季后赛 / 总决赛前可以再调整一次
- [ ] **奖金**：赛事结束按最终名次发放（§4.18 奖金表，配置），取代每赢一个系列赛的 `win_prize`
- [ ] **首发和战术**：赛前可以在卡片上切换首发 / 替补（分路按主分路），默认沿用上一赛段；选一个战术倾向（入侵 / 发育 / 团战），克制关系和加成放配置；首发不满 5 人时不能结算，界面提示补齐
- [ ] **胜负**：按 §4.9 完整公式——有效评分（心情、压力、坚毅）→ 加权平均与短板惩罚 → 风格分 → 综合战力（含战术克制）→ 单局波动 → 胜率 clamp 到 [`p_min`, `p_max`]（⚙ 0.10 / 0.90）；逐局模拟，决胜局叠加果断 / 犹豫系数；全队坚毅系数取首发平均；同一种子结果相同
- [ ] **回写**：比赛场数、输球场数、胜负传给任务 003 的压力和心情计算；同场赢球的首发两两熟悉度 `+f_win`（§4.8）；近期战绩 = 最近一次赛事的系列赛胜率，用于俱乐部人气（任务 002）和拉赞助成功率。按每场比赛的实际参赛名单记录（不在结算时重新读取阵容）
- [ ] **执行顺序**：校验 → 行动 → 比赛 → 商务 → 心情与压力 → `SettleSegment` → 汇总；任何校验失败都不改动状态
- [ ] **战报**：常规赛每场一行比分和最关键的原因；季后赛 / 总决赛每个系列赛列出每局胜负，以及影响最大的 2～3 个原因（例如"中单压力过高""战术被克制""默契不足""对方实力更强"），用不需要 KPL 知识也能看懂的文字
- [ ] **积分榜**：每项赛事的常规赛积分榜（8 队系列赛胜负、小分、排名，玩家高亮）、季后赛对阵和最终名次；年度总决赛的资格积分榜；主界面显示"距离下一项赛事还有 N 个赛段"
- [ ] **界面**：先完成 TD-003 的界面栈；结算时先显示战报，再显示赛段汇总（含积分榜和名次变化）
- [ ] **截图**（`production/qa/evidence/story-004-match/`，用实际运行的游戏截取并打开检查）：首发和战术选择、战报、积分榜各一张

---

## Implementation Notes

- 配置：`Assets/Data/match_config.json`（对手数量、赛历、赛制、BO、积分、奖金表、战术克制表、§4.9 全部系数），遵守数据文件规范，复用严格检查器
- `TeamStrength` 现在直接读选手的心情 / 压力；对手需要一个"固定心情 / 压力"的重载（任务 003 留下的事项）
- 任务 003 的 `SegmentContext` 目前在结算时读取阵容；要改为按每场比赛快照参赛名单
- 战报原因：比较本队与对手在各项（技术分、默契、风格分、战术、压力波动）上的差距，取贡献最大的几项
- 写代码前查 `docs/engine-reference/unity/`（Unity 6.6 比训练数据新）

---

## Out of Scope

- Story 005：转会期、新一年的开始和归档、对手用资金补强
- Story 006：意外事件
- 英雄 BP、实时操作（简报明确不做）

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

*Governed by `qa.level`: at `qa.level: minimal` tests are **waived** (advisory), but a Visual/Feel or UI story's retained screenshot is not.*

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/Match/` NUnit tests (advisory) + retained screenshots of lineup/tactics, match report and standings in `production/qa/evidence/story-004-match/` (player-observable — required)

**Status**: [x] Created — NUnit tests in `Assets/Tests/EditMode/Match/` and `UI/`; screenshots in `production/qa/evidence/story-004-match/`

---

## Dependencies

- Depends on: Story 003, Story 009
- Unlocks: Story 006

---

## Completion Notes
**Completed**: 2026-10-19
**Criteria**: 12/12 passing. Not observed in play and not screenshotted: the player missing the playoffs, missing the finals, and the second-year banner (code written, not run).
**Deviations**: (1) Opponents are "mature clubs" (mood 80, familiarity 60–80, plus a per-segment form random walk) instead of the AC's fixed mood/stress 50 — user decision after the balance diagnostic, recorded in numbers.md v3.3/v3.4. (2) Power weights 4 / 0.7 / 1 (was 3 / 1 / 1), user decision. (3) The story was reshaped mid-way after the first playtest: matches only at the spring/summer/finals events (numbers.md §4.18), prize by placing instead of per series. (4) Touched earlier stories' code, configs and UI (002/003/009), all user-approved. (5) Normal play now uses a random seed per new game; debug/screenshot runs keep the fixed seed.
**Test Evidence**: Logic + UI — 668 EditMode tests pass (`test-results/editmode.xml`); screenshots `production/qa/evidence/story-004-match/01..10` and `layout-fix/` (several sizes incl. 3440x1440 and the MacBook proportions); user hand-played and confirmed prep screen, calendar/format, report and tables.
**Code Review**: Skipped (solo mode); unity-ui-specialist reviewed twice (stage B, event UI) — findings fixed (double-click skip, rules moved out of UI, scroll/ellipsis, culture-invariant text, error text in Chinese).
**Run result**: OBSERVED — see screenshots above; layout bugs found by the user in fullscreen on the MacBook (notch / safe area, shared-root layout leaking from the club-name screen) fixed and verified through the same screen flow a player uses.
**Tech debt**: TD-003 partly resolved (screen stack done; scroll lists + USS theme remain for Story 005), TD-004 (keyboard/gamepad focus).
**Carry-forward**: Balance problems from play-testing go to Story 006: doing nothing wins (stress recovers, mood high), training effect weak over a year, prize ≈21% of salaries vs 15% target, mood saturates at 100, bench mood falls low. Story 005: success costs (raises, poaching, opponent reinforcement), SeasonRankScore placeholder 50 in club popularity. Idea parked: version updates (frequency TBD). Also log: player-facing report `even_threshold` 0.05 was agent-chosen.
