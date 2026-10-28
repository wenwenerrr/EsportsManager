# Story 012: 成长：专精与绝活

> **Epic**: 战队经理人 MVP
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-27

## Context

**GDD**: `design/gdd/growth.md`（系统设为 full，见 `project.yaml` system_overrides）
**Requirement**: v4 重设计（`design/gdd/systems-index.md`）

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.6.5f1 | **Risk**: HIGH
**Engine Notes**: 纯 C# 逻辑，注入随机源；界面沿用 UI Toolkit + ScreenStack

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] 训练公式改为 growth.md F1～F4（整体余量、溢出、专精系数），`T_base` 10；取代六维同涨 + 目标 ×1.5
- [ ] 选手新增 `focus_E/M/L`；赛段末尾按 F3 更新；新人初始专精按 talent.md
- [ ] 8 个绝活放配置（`player_config.json` 新增 `specialty` 节），获得 / 失去规则与检查时机按 growth.md；配置校验（未知分路、维度、挂钩点）
- [ ] 首发的绝活汇总为 match.md 的挂钩点值传给比赛
- [ ] 界面：选手卡片专精条和绝活标签；训练时显示维度所属阶段和"离绝活还差 X"（不暴露潜力）；结算显示获得 / 失去绝活；**留截图**到 `production/qa/evidence/story-012-growth/`
- [ ] growth.md AC 1～15 有对应测试并通过；`numbers.md` §4.1 标注被取代的部分


---

## Implementation Notes

- **2026-10-27**：规格改为 `design/gdd/growth.md` rev 3（练一个阶段 + 重点维度、天花板 潜力+10、心情 / 压力影响训练、专精每赛段按点数更新、16 个绝活、门槛 76/73）。数值对照 `tools/sim/sim5.py`。
- AI 选手同一规则（013 调用）
- 现有存档 / 状态里没有专精字段的选手，按 talent.md 规则补初值

---

## Out of Scope

- 二级绝活、领悟绝活事件

---

## QA Test Cases

*以 GDD 的 Acceptance Criteria 为测试规格（已按 qa-lead 意见写成可测形式）*

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/Growth/` 单元测试 + 选手卡片 / 训练 / 结算截图

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: 011
- Unlocks: 013
