# Story 011: 比赛模型 v4

> **Epic**: 战队经理人 MVP
> **Status**: In Progress
> **Layer**: Core
> **Type**: Logic
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-27

## Context

**GDD**: `design/gdd/match.md`（系统设为 full，见 `project.yaml` system_overrides）
**Requirement**: v4 重设计（`design/gdd/systems-index.md`）

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.6.5f1 | **Risk**: HIGH
**Engine Notes**: 纯 C# 逻辑，注入随机源；界面沿用 UI Toolkit + ScreenStack

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] 按 match.md 的 12 步求值顺序实现 F1～F14（阶段实力、中期默契、执行力、明星项、提前结束、基础分、战术匹配、克制、波动、胜率、决胜局），取代 `TeamStrength` 单一战力和对手 `form`
- [ ] `match_config.json` 按 match.md 的 Tuning Knobs 改写（阶段映射、权重、`p_floor` 0.15 等），严格校验覆盖 match.md AC 13 列出的全部非法配置
- [ ] 每局固定抽 3 个随机数（A 波动、B 波动、掷骰），`ε = σ(2u−1)`；系列赛规则、`bestOf` 1～9 奇数
- [ ] 绝活挂钩点接口（4 个，只改本队，合计钳制）；本任务中挂钩表为空，012 接入
- [ ] 战报数据：分项、战术对照、原因挑选（运气、势均力敌、提前结束）
- [ ] 赛前预估函数（我方三阶段、执行力、每阶段短板分路）
- [ ] 界面：赛前准备显示我方三阶段实力、执行力、短板；战报显示新原因和战术对照；**留截图**到 `production/qa/evidence/story-011-match-v4/`
- [ ] match.md AC 1～40 全部有对应单元测试并通过；`numbers.md` §4.9 标注"已被 design/gdd/match.md 取代"


---

## Implementation Notes

- **2026-10-27**：规格改为 `design/gdd/match.md` rev 4（三阶段克制循环、分阶段短板、默契相对值、明星门槛与上限、κ_s 0.025、s 0.22、无条件挂钩、AC 1～43）。已写的部分代码按 rev 4 调整。数值对照 `tools/sim/sim5.py`（fixG 设置）。
- 对手在 013 之前仍用现有对手俱乐部，但心情 / 压力读选手自己的值（不再固定 80 / 50），`form` 删除
- 现有 Match 代码重构而不是另起；保持 Tournament / Standings / 赛历不变
- 作者脚本（scratchpad v3.py）的结果可作为测试对照

---

## Out of Scope

- 对手信息（雷达图形状、最近战术）和 AI 选战术：013
- 绝活内容：012

---

## QA Test Cases

*以 GDD 的 Acceptance Criteria 为测试规格（已按 qa-lead 意见写成可测形式）*

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/Match/` 单元测试 + 赛前准备和战报的保留截图

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 004（Complete）
- Unlocks: 012, 013
