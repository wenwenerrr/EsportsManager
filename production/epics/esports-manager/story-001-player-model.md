# Story 001: 选手数据模型和虚构选手池

> **Epic**: 战队经理人 MVP
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: [fill before starting]
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-15

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 1（虚构选手池和选手属性）

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.6.5f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (Player goal & fail state + MVP feature 1), scoped to this story:*

- [ ] 选手拥有：姓名、年龄、颜值、人气、主分路、心情、压力、性格、薪资，以及分路能力雷达
- [ ] 五个分路各有 6 个维度，与简报中的表格一致；维度定义来自配置数据，代码里不写死维度名
- [ ] 每名选手对任一分路都能算出"分路评分"（该分路六维的加权值，权重可配置）
- [ ] 打非主分路时，分路评分乘以一个可配置的折扣系数（< 1）
- [ ] 心情、压力、颜值、人气以及各维度都被钳制在 0–100 之间
- [ ] 性格是一组有限的类型（初始 4–6 种），由配置定义
- [ ] 虚构选手池由配置生成，至少 25 人（每个分路 ≥ 5 人）；同一随机种子生成的结果完全相同
- [ ] 年龄成长：调用"过一年"时，按年龄段曲线调整六维（曲线可配置；年轻选手上升，老将下降）

---

## Implementation Notes

- 纯 C# 数据和逻辑，不依赖 MonoBehaviour，方便 EditMode 测试
- 所有数值（权重、折扣、成长曲线、生成范围）放在配置里（ScriptableObject 或 JSON，在 `/dev-story` 时定）
- 随机数通过注入的随机源获取，测试时传固定种子
- 写代码前查 `docs/engine-reference/unity/`（Unity 6.6 比训练数据新）

---

## Out of Scope

- Story 002：战队层面的数值（实力、人气、经济、默契）
- Story 006：性格触发的事件

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

*Governed by `qa.level`: at `qa.level: minimal` tests are **waived** (advisory), but a Visual/Feel or UI story's retained screenshot is not.*

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/Players/PlayerModelTests.cs` (advisory at `qa.level: minimal`)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: None
- Unlocks: Story 002

---

## Completion Notes
**Completed**: 2026-10-15
**Criteria**: 8/8 passing (no deferred items)
**Deviations**: ADVISORY — `Assets/Data/PlayerConfig.json` name and camelCase keys differ from `.claude/rules/data-files.md` (snake_case, `[system]_[name].json`, wire-format DTO); `playersPerLane = 5` default sits in code. Logged as TD-001 in `docs/tech-debt-register.md`.
**Test Evidence**: Logic: `Assets/Tests/EditMode/Players/PlayerModelTests.cs` — 31 tests, 31 passed, 0 failed (`test-results/editmode.xml`, from the /dev-story run; /story-done did not re-run them). Waived at `qa.level: minimal`, written anyway at the user's request.
**Code Review**: Skipped (solo mode); unity-specialist review run informally — no blocking findings, SHOULD-FIX items fixed.
**Run result**: N/A — pure data/logic layer, no player-visible surface (first UI is Story 003)
