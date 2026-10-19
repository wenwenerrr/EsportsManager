# Story 010: 程序生成选手头像

> **Epic**: 战队经理人 MVP
> **Status**: Ready
> **Layer**: Presentation
> **Type**: UI
> **Estimate**: [fill before starting]
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-19 (added after the first playtest; user-approved)

## Context

**GDD**: `design/game-brief.md` (Art & audio direction: 选手头像由程序生成)
**Requirement**: Playtest feedback 2026-10-19 — "加入选手的脸"; not one of the original 7 MVP features

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.6.5f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] 头像由 4～6 类部件组合：脸型、发型、发色、眼睛、表情、背景色；部件列表和权重放在配置里
- [ ] 同一名选手的头像固定不变：由选手 ID 和种子决定，重新打开游戏后一致
- [ ] 颜值影响部件选择：颜值越高，"好看部件"的权重越高；权重可配置
- [ ] 部件用简单的几何色块绘制（UI Toolkit 的矢量绘制或纯色块），不需要美术素材
- [ ] 选手卡片、商务弹窗、结算界面都显示头像，布局不挤压现有文字
- [ ] 生成逻辑是纯 C#，可单独测试；界面只负责绘制
- [ ] **留截图**（`production/qa/evidence/story-010-avatars/`，用实际运行的游戏截取并打开检查）：主界面卡片、商务弹窗、结算各一张

---

## Implementation Notes

- 生成器按"选手 ID + 种子"派生自己的随机源，不打乱游戏主随机序列（不影响现有种子的可复现性）
- 写代码前查 `docs/engine-reference/unity/`（UI Toolkit 矢量绘制 API 在 Unity 6.6 下要核实）

---

## Out of Scope

- 手绘立绘、动画表情
- 头像随年龄或事件变化（以后再加）

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

*Governed by `qa.level`: at `qa.level: minimal` tests are **waived** (advisory), but a Visual/Feel or UI story's retained screenshot is not.*

**Story Type**: UI
**Required evidence**: retained screenshots in `production/qa/evidence/story-010-avatars/` (required)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 009
- Unlocks: None (scheduled after Story 004 and Story 006)
