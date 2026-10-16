# Story 003: 赛段行动分配和文字界面

> **Epic**: 战队经理人 MVP
> **Status**: Ready
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: [fill before starting]
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 3（赛段行动分配）— Build order step 1, first playable

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.6.5f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (Player goal & fail state + MVP feature 3), scoped to this story:*

- [ ] 每个赛段有固定的行动点数（可配置），可分配给：训练、直播、商务、休息
- [ ] 训练：提升受训选手在本分路的六维，同时增加压力
- [ ] 直播提升人气，商务提升资金，两者都小幅降低压力；休息大幅降低压力
- [ ] 心情随行动变化（例如连续高强度训练会降低心情）
- [ ] 张力验证（固定种子模拟）：连续 4 个赛段只训练，资金明显下滑
- [ ] 张力验证（固定种子模拟）：连续 4 个赛段只直播和商务，实力相对没有训练的基线下降
- [ ] 文字界面：显示三角数值、每名选手的心情和压力、可分配的行动；确认后显示本赛段结算结果
- [ ] 能在 Unity 中运行，连续推进多个赛段不报错

---

## Implementation Notes

- 逻辑和界面分离：结算逻辑可在 EditMode 测试，界面只负责显示和输入
- UI 技术（UI Toolkit 或 UGUI）在 `/dev-story` 时决定；写之前查 `docs/engine-reference/unity/modules/ui.md`
- 文字和色块即可，不做美术

---

## Out of Scope

- Story 004：比赛
- Story 005：赛季和转会期的时间结构（本任务只需"下一个赛段"）

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

*Governed by `qa.level`: at `qa.level: minimal` tests are **waived** (advisory), but a Visual/Feel or UI story's retained screenshot is not.*

**Story Type**: Integration
**Required evidence**: `Assets/Tests/EditMode/Actions/SegmentActionTests.cs` (advisory) + retained screenshot of the text UI in `production/qa/evidence/` (player-observable — required)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002
- Unlocks: Story 004
