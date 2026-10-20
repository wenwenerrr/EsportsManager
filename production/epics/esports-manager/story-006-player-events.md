# Story 006: 意外事件（行动事件 + 状态事件）

> **Epic**: 战队经理人 MVP
> **Status**: Complete
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: [fill before starting]
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-20

## Context

**GDD**: `design/game-brief.md` (formulas: `design/numbers.md` §4.17, potential changes §4.15)
**Requirement**: Brief MVP feature 6（意外事件）— Build order step 4

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.6.5f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (MVP feature 6) and `design/numbers.md` §4.17, scoped to this story:*

- [ ] 事件表全部放在配置里（`Assets/Data/event_config.json`，遵守数据文件规范，复用严格检查器）：每个事件有触发行动或条件、好 / 坏、权重、文本、效果
- [ ] 行动事件：每个行动点按 `p_event` 判定是否触发；好坏概率按 §4.17 公式（心情、压力、性格修正，钳制在上下限内）；每赛段最多 `max_action_events` 个；所有数值可配置
- [ ] 效果能改变本次行动的结果（训练收益、直播收入、休息减压、拉赞助成功率）和属性（心情、压力、人气、俱乐部人气、默契、潜力、资金），结果钳制在合法范围内
- [ ] 状态事件：每赛段最多 `max_state_events` 个；条件可以引用性格、心情、压力、相性、默契和最近的比赛结果
- [ ] 初版至少 16 个事件：每种行动（训练 / 直播 / 休息 / 商务 / 拉赞助）至少 1 好 1 坏，另加至少 4 个状态事件（含开窍和低谷）；文本不需要 KPL 知识也能看懂
- [ ] 同一种子下触发结果可以复现；事件判定接在任务 003 的赛段流程里，任何校验失败都不改动状态
- [ ] 结算界面显示本赛段发生的事件卡片（标题、文本、效果）；**留截图**（`production/qa/evidence/story-006-events/`，用实际运行的游戏截取并打开检查）

---

## Implementation Notes

- 事件文本写成不依赖 KPL 知识也能看懂；梗是加分项，不是理解门槛
- 潜力变化走任务 008 的 `AdjustPotential`（或新增），界面仍然不显示真实潜力
- 资金类效果计入 §4.10 的事件收入 / 事件支出，出现在结算汇总里
- **（2026-10-20 改）平衡不在本任务做**：用户认为逐项调数值是"哪里不对改哪里"，决定先做结构性重设计（`design/match-model-v4.md`：对手风格与克制、专精绝活、整个联赛作为会成长的系统）；本任务只做事件，不改现有平衡数值。以下原记录保留备查——**（2026-10-19 用户决定并入本任务）"躺平"不应是好策略**：试玩中一个赛季什么都不做也夺冠——不安排行动时压力自然下降、心情偏高，比赛状态反而最好；训练 3 个赛段的成长抵不过训练压力。本任务一并调整并用固定种子的模拟验证：长期不训练技术缓慢退步（或状态需要训练和比赛保持）；训练收益看得见（提高成长或降低训练压力，先给用户看方案再改数值）；事件让"不管不顾"随机受罚（懈怠、粉丝不满、赞助商撤资等）
- （2026-10-19 用户决定）用事件代替"让钱重新紧张"的新机制：事件表里要有足量的扣钱事件（违约金、罚款、设备损坏、选手医疗费等）和少量意外收入，让盈利不再稳定
- 界面切换先完成 TD-003 的界面栈（若任务 004 还没做）

---

## Out of Scope

- 多步剧情分支（以后再加）；**简单的二选一事件在本任务内**（2026-10-20 用户同意）：少量事件给玩家两个选项，各有即时效果和可选的后续影响
- 选手头像（任务 010）

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

*Governed by `qa.level`: at `qa.level: minimal` tests are **waived** (advisory), but a Visual/Feel or UI story's retained screenshot is not.*

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/Events/` NUnit tests (advisory) + retained screenshot of event cards in the settlement screen in `production/qa/evidence/story-006-events/` (player-observable — required)

**Status**: [x] Created — `Assets/Tests/EditMode/Events/EventTests.cs`, `Assets/Tests/EditMode/UI/Story006EventUiTests.cs`; screenshots 01–04 in `production/qa/evidence/story-006-events/`

---

## Dependencies

- Depends on: Story 004 (recent match results in state-event conditions)
- Unlocks: Story 005

---

## Completion Notes
**Completed**: 2026-10-20
**Criteria**: 7/7 verified (钱变紧的体感未达到——起始资金高、年终总决赛奖金大；属数值平衡，留给 v4 重设计 `design/match-model-v4.md`，不算本任务缺陷)
**Deviations**: 40 个事件（24 行动 / 11 状态 / 5 二选一）；二选一事件在范围内（2026-10-20 用户同意）；平衡不在本任务做
**Test Evidence**: tests present (708 passed at last run); screenshots `production/qa/evidence/story-006-events/01-04`; user playtest confirmed events trigger and the choice/leave flow works
**Code Review**: Skipped (solo)
**Open items**: 开窍时潜力已封顶仍显示"潜力上升"；多人效果显示平均值；事件卡无行动标签；无"请假中"卡片截图
