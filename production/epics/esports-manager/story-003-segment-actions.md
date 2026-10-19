# Story 003: 赛段行动逻辑（按 numbers.md v2 重写）

> **Epic**: 战队经理人 MVP
> **Status**: Complete
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: [fill before starting]
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-16

## Context

**GDD**: `design/game-brief.md` (formulas and tables: `design/numbers.md` §4.1, §4.6, §4.7, §4.11, §4.12, §4.16, plus the stream-heat mapping noted in Story 002)
**Requirement**: Brief MVP feature 3（赛段行动分配）— Build order step 1

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.6.5f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (Player goal & fail state + MVP feature 3) and `design/numbers.md`, scoped to this story (pure logic — the text UI is Story 009):*

- [ ] 行动点（§4.16）：每赛段全队有 `AP` 个共享行动点（配置）；一个点 = 给一名选手分配一个行动（训练并选目标维度 / 直播 / 休息）、一次拉赞助，或接受一次商务；同一选手每赛段最多 `max_per_player` 个点；同一行动可叠加（训练两次 = 强度 ×2）；超出行动点或超出单人上限时抛出明确错误；用不完不结转
- [ ] 训练（§4.1）：每个训练点对目标维度加 `Player.TrainingGain(...)` 的收益（钳制到 0–100）；压力上涨；该选手观察度增加（任务 008 的 `AddObservation`，数值取配置）
- [ ] 直播：每个直播点计一次直播；直播次数先乘性格直播系数（`stream_fans`）、再按 `stream_cap` 换算成 0–100 的热度，作为任务 002 `SettleSegment` 的输入；直播收入用任务 002 的 `LivestreamIncome`，在结算前计算；直播小幅降低压力
- [ ] 休息与压力（§4.7）：休息点大幅降低压力；压力公式的各项（训练、比赛、输球、商务、休息、直播）和性格压力系数都按配置；比赛和输球项现在由调用方传入（默认 0，任务 004 再接）；结果钳制 0–100，带 `R(0.1)` 随机因子
- [ ] 心情（§4.6）：各项（胜负、上场/替补、薪资满意度、个人相性、高压）按配置，乘性格系数和 `R(0.15)`，并向 50 缓慢回归；胜负由调用方传入（默认 0）；结果钳制 0–100
- [ ] 商务弹窗（§4.11）：每赛段每名选手按人气、分路评分、颜值（加权）和性格商务系数算触发概率（上限 `p_max`）；金额按人气、颜值和性格金额系数，带 `R(0.2)`；**接受**：消耗 1 个行动点（算在该选手身上），该选手人气热度和压力上升，俱乐部按分成比例获得收入；**不接**：无任何变化；没有行动点时不能接受
- [ ] 拉赞助（§4.12）：消耗 1 个行动点；成功率 = `clamp(WS(俱乐部人气, 近期成绩)/100 × z_max + U(−0.1, +0.1), 0.05, 0.9)`；成功后获得一份赞助合同（每赛段金额 `c0 + c1 × 俱乐部人气`，持续 `N` 个赛段），生效中的合同每赛段计入任务 002 的赞助收入，到期自动移除
- [ ] 赛段流程：一个入口方法，按固定顺序执行——校验分配 → 执行行动 → 抽取并处理商务 → 更新心情与压力 → 调用任务 002 的 `SettleSegment` → 返回本赛段汇总；任何校验失败都**不改动任何状态**
- [ ] 张力验证（固定种子的模拟测试）：连续 4 个赛段全部行动点用于训练，资金明显低于"只直播+商务"的对照组；连续 4 个赛段只直播和商务，球队实力低于"训练"组；两组差距用断言固定下来
- [ ] 配置：新配置 `Assets/Data/segment_config.json`，遵守数据文件规范（小写文件名、`snake_case`、DTO），复用任务 008 的严格检查器；所有数值放配置，有范围校验，缺键 / 错类型 / 无穷大报错

---

## Implementation Notes

- 纯 C#，建立在任务 001、008、002 之上（`Player`、`Team`、`SettleSegment`、`LivestreamIncome`、`TeamStrength`）；随机数通过注入的随机源，抽取顺序固定以保证确定性
- 代码放 `Assets/Scripts/Segment/`，命名空间 `EsportsManager.Segments`（注意：不要用与类名相同的命名空间）
- 本任务只做逻辑，没有界面；界面在任务 009。对外提供一个可读的"赛段状态"（行动点余量、待处理商务、本赛段汇总），供界面轮询或包装
- 比赛不在本任务：胜负、比赛场数、输球场数由调用方传入，默认 0
- 收入结构目标见 `design/numbers.md` §4.10；本任务给出行动点之后，要用张力验证和一次粗略的经济估算重新核对直播收入，必要时在报告里建议新数值（不要私自改 `team_config.json` 的直播数值）
- 写代码前查 `docs/engine-reference/unity/`（Unity 6.6 比训练数据新）

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 009：文字界面、中文字体、截图
- Story 004：比赛模拟（胜负、比赛场数的产生）
- Story 005：赛季日历、转会、拍卖
- Story 006：选手事件
- Story 007：破产判定、年终总结

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

*Governed by `qa.level`: at `qa.level: minimal` tests are **waived** (advisory), but a Visual/Feel or UI story's retained screenshot is not.*

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/Segment/` — NUnit tests (advisory at `qa.level: minimal`; the user has asked for tests anyway). No screenshot: this story has nothing player-visible (the UI is Story 009).

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001, Story 008, Story 002 (all Complete)
- Unlocks: Story 009

---

## Completion Notes
**Completed**: 2026-10-16
**Criteria**: 10/10 passing (no deferred items)
**Deviations**: (1) AC wording drifted behind later user design decisions recorded in `design/numbers.md` v3 / v3.1 / v3.2 — training raises all six main-lane dimensions (target ×1.5) rather than one dimension (AC2); streaming no longer lowers stress, `s_live` = 0 (AC3); sponsorship is capped at 3 attempts per segment and 3 concurrent contracts (AC7). Code follows the latest design. (2) User-approved balance edits to earlier-story files: `player_config.json` training base gain 2 → 5, `Player.cs` doc comments only, one shipped-value assertion in `PlayerConfigLoadingTests.cs`, and Story 002's Team code/config (`second_stream_factor`). (3) AC9 strength gap is thin (training 50.61 vs stream/business 49.79); the money half is clear (113k). The user chose not to tune stress parameters; all-train stress plateaus near 60 so rest is valuable but not forced — re-judge when playable.
**Test Evidence**: Logic: `Assets/Tests/EditMode/Segment/` (7 test files) — 197 new tests; whole suite 510 tests, 510 passed, 0 failed (`test-results/editmode.xml`, from the /dev-story run; /story-done did not re-run them). Waived at `qa.level: minimal`, written anyway at the user's request.
**Code Review**: Skipped (solo mode); unity-specialist reviewed, findings addressed.
**Run result**: N/A — pure logic layer, no player-visible surface (UI is Story 009)
**Carry-forward**: Story 009 uses `TryValidate`, `Preview`, `GetState` (LastSummary, Serial, free contract slots) — never `Player.Potential`, only `GetPotentialEstimate()`; CJK font setup is not covered by the engine reference. Story 004 must snapshot per-match participants in `SegmentContext` (stress/mood/familiarity currently use the lineup at Resolve time) and decide whether sponsorship should read the win rate before or after this segment's results. Story 005 must move sponsorship contracts out of `SegmentRunner` (they would be lost if a new runner is created per segment, and save/load would re-roll offers) and add a cancel/transfer-window path. Economy retune pending (stream 11% / business 12% / sponsorship 34% of salaries vs target 25 / 20 / 40).
