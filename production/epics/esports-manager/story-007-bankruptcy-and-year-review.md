# Story 007: 破产判定和年终总结

> **Epic**: 战队经理人 MVP
> **Status**: Ready
> **Layer**: Presentation
> **Type**: UI
> **Estimate**: [fill before starting]
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 7（破产判定和年终总结）

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.6.5f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (Player goal & fail state + MVP feature 7), scoped to this story:*

- [ ] 资金归零（< 0）时判定破产，显示破产画面，本局结束
- [ ] 年终总结按四条路线分别评价：竞技（春季赛、夏季赛、年度总决赛的名次）、商业（资金和赞助）、人气（粉丝数）、成长（选手能力提升）
- [ ] 每条路线给一个等级（如 S / A / B / C），阈值可配置
- [ ] 没有"总分"或"唯一正确路线"——四项并列展示
- [ ] 看完总结后可以继续下一年
- [ ] （2026-10-22 v4 补充）年终总结增加"联赛格局"：各队风格、谁进步最快、谁买了谁（数据来自 league / transfer）
- [ ] （2026-10-22 v4 补充）成长路线的评价计入专精和绝活
- [ ] 破产画面和年终总结画面都能在游戏中实际看到

---

## Implementation Notes

- 等级判定是纯逻辑，可单独测试；界面只负责展示

---

## Out of Scope

- 存档、多年历史统计

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

*Governed by `qa.level`: at `qa.level: minimal` tests are **waived** (advisory), but a Visual/Feel or UI story's retained screenshot is not.*

**Story Type**: UI
**Required evidence**: retained screenshots of the bankruptcy screen and the year-review screen in `production/qa/evidence/` (required)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 005
- Unlocks: None
