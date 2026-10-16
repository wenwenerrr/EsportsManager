# Story 002: 战队三角和默契

> **Epic**: 战队经理人 MVP
> **Status**: In Progress
> **Layer**: Core
> **Type**: Logic
> **Estimate**: [fill before starting]
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-15

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 2（战队三角加默契）

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.6.5f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (Player goal & fail state + MVP feature 2), scoped to this story:*

- [ ] 战队有实力、人气、资金三项数值，外加默契（0–100）
- [ ] 实力由首发五人在各自分路上的分路评分汇总得出（汇总方式可配置）
- [ ] 人气由选手人气和战队自身积累的人气共同决定
- [ ] 每个赛段结算资金：收入（赞助、直播）减去支出（全队薪资）；赞助收入随人气和近期成绩上升
- [ ] 同一套首发一起训练或比赛，默契逐步上升；替换首发会扣默契（幅度可配置）
- [ ] 性格搭配表（配置）定义哪些性格合拍、哪些冲突，影响默契的涨跌速度
- [ ] 资金可以为负；是否破产由 Story 007 判定，本任务只保证数值正确

---

## Implementation Notes

- 纯 C# 逻辑，依赖 Story 001 的选手模型
- 所有系数放配置，不写死

---

## Out of Scope

- Story 003：行动如何改变这些数值
- Story 007：破产判定

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

*Governed by `qa.level`: at `qa.level: minimal` tests are **waived** (advisory), but a Visual/Feel or UI story's retained screenshot is not.*

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/Team/TeamTriangleTests.cs` (advisory at `qa.level: minimal`)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001
- Unlocks: Story 003
