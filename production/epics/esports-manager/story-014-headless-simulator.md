# Story 014: 自动模拟器与平衡验证

> **Epic**: 战队经理人 MVP
> **Status**: Ready
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-27

## Context

**GDD**: 各 GDD 的"平衡验证"小节（系统设为 full，见 `project.yaml` system_overrides）
**Requirement**: v4 重设计（`design/gdd/systems-index.md`）

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.6.5f1 | **Risk**: HIGH
**Engine Notes**: 纯 C# 逻辑，注入随机源；界面沿用 UI Toolkit + ScreenStack

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] 无界面模拟入口（编辑器批处理），≥ 100 个种子 × 2 年，玩家俱乐部由策略机器人操作
- [ ] 8 种机器人：空闲、均衡补短板、前期专精、团战专精、后期专精、明星单核、纯商业、按对手选战术；规则写在本任务里并实现
- [ ] 输出报告到 `production/qa/sim/`：match BV1～BV5、growth GV1～GV3、economy EV1～EV3、talent TV1～TV2、league LV1～LV4、transfer XV1～XV3 的观测值和是否达标
- [ ] 同一种子结果可复现
- [ ] 把报告结果给用户看，未达标项列出原因和建议（不自动改数值）


---

## Implementation Notes

- **2026-10-27**：任务改为把 `tools/sim/sim5.py`（fixG）移植为 C# 无界面模拟器，机器人定义沿用原型；同一种子下关键指标与 Python 原型一致（容差在任务内定义）；并复测各文档标 * 的数值：BV1 / BV5（s 0.22、κ_s 0.025）、绝活 ΔG（s 0.22）、R_ref、每队 2 次挖角（XV2 / LV7）、8 名青训（TV1）、选拔 t_max 0.10（TV2）。结果交给用户看，不自动改数值。
- 可在 013 后先跑一个不含转会的版本，005 后跑完整版

---

## Out of Scope

- 自动调参

---

## QA Test Cases

*以 GDD 的 Acceptance Criteria 为测试规格（已按 qa-lead 意见写成可测形式）*

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/Simulation/` 冒烟测试 + `production/qa/sim/` 报告

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: 013（部分）、005（完整）
- Unlocks: 平衡调整
