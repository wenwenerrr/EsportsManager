# Story 006: 选手事件

> **Epic**: 战队经理人 MVP
> **Status**: Ready
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: [fill before starting]
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 6（选手事件）

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.6.5f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (Player goal & fail state + MVP feature 6), scoped to this story:*

- [ ] 事件表放在配置里，每个事件有：触发条件、文本、效果
- [ ] 触发条件可以引用性格、心情、压力、人气、默契和最近的比赛结果
- [ ] 每个赛段最多触发一个可配置的事件数量
- [ ] 事件效果修改属性后仍钳制在合法范围内
- [ ] 事件文本显示在界面上（梗的主要载体）
- [ ] 初始至少 8 个事件，覆盖正面、负面和选手之间的关系事件
- [ ] 同一种子下触发结果可复现

---

## Implementation Notes

- 事件文本写成不依赖 KPL 知识也能看懂；梗是加分项，不是理解门槛

---

## Out of Scope

- 带选项分支的复杂剧情事件（以后再加）

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

*Governed by `qa.level`: at `qa.level: minimal` tests are **waived** (advisory), but a Visual/Feel or UI story's retained screenshot is not.*

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/Events/PlayerEventTests.cs` (advisory) + retained screenshot of an event shown in-game in `production/qa/evidence/` (player-observable — required)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 005
- Unlocks: Story 007
