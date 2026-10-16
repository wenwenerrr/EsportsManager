# Story 004: 比赛模拟和战报

> **Epic**: 战队经理人 MVP
> **Status**: Ready
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: [fill before starting]
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 4（比赛模拟）

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.6.5f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (Player goal & fail state + MVP feature 4), scoped to this story:*

- [ ] 赛前选择 5 名首发及各自分路，并选一个战术倾向（入侵 / 发育 / 团战，克制关系可配置）
- [ ] 队伍战力 = 各首发分路评分 × 状态 + 默契加成 + 战术克制加成 + 场上指挥加成（首发中指挥能力最高者担任指挥）
- [ ] 状态：心情决定发挥上限，压力决定波动幅度
- [ ] 单局胜率由战力差换算，并限制在一个可配置区间内（初始 10%–90%），强队也会翻车
- [ ] BO 系列赛逐局模拟；同一种子结果完全相同
- [ ] 战报列出胜负的主要原因（至少影响最大的 2 个因素，例如"战术被克制""中单压力过高"）
- [ ] 比赛结果回写：胜负影响人气、默契和选手心情
- [ ] 界面显示战报（文字即可）

---

## Implementation Notes

- 本任务用配置里的固定对手；对手俱乐部系统在 Story 005
- 胜率公式、克制表、指挥加成全部放配置

---

## Out of Scope

- Story 005：赛程、排名、对手俱乐部
- 英雄 BP、实时操作（简报明确不做）

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

*Governed by `qa.level`: at `qa.level: minimal` tests are **waived** (advisory), but a Visual/Feel or UI story's retained screenshot is not.*

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/Match/MatchSimTests.cs` (advisory) + retained screenshot of a match report in `production/qa/evidence/` (player-observable — required)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 003
- Unlocks: Story 005
