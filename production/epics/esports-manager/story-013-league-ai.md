# Story 013: 联赛 AI 与预热

> **Epic**: 战队经理人 MVP
> **Status**: Ready
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-27

## Context

**GDD**: `design/gdd/league.md`、`design/gdd/talent.md`（系统设为 full，见 `project.yaml` system_overrides）
**Requirement**: v4 重设计（`design/gdd/systems-index.md`）

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.6.5f1 | **Risk**: HIGH
**Engine Notes**: 纯 C# 逻辑，注入随机源；界面沿用 UI Toolkit + ScreenStack

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] 7 支 AI 俱乐部与玩家使用同一数据结构和同一套公式（资金、工资、训练、心情、压力、默契、事件）；删除对手固定状态
- [ ] 风格（4 种）和性格（激进、节俭、适应）；开局按风格挑人（league F1），人才系统按 talent.md 生成选手池
- [ ] AI 每赛段行动分配、首发、选战术（风格偏好 + 针对）、情报度（不显示）、赛后换风格，规则按 league.md
- [ ] 预热：联赛自行运行一整年（012 前的转会期以"年度更替 + 补签"代替，005 完成后换成完整转会期），然后随机接手一支俱乐部并输入队名
- [ ] 随机源分流：AI 决策、AI 事件、人才、每个系列赛的随机源由主种子派生；只改玩家行动时 AI 的随机抽取序列不变
- [ ] 界面：接手画面、积分榜风格标签、赛前对手信息（人员、雷达图形状、最近 5 场战术）；**留截图**到 `production/qa/evidence/story-013-league/`
- [ ] league.md AC 1～12、talent.md AC 1～8 有对应测试并通过；预热 + 一年 < 1 秒


---

## Implementation Notes

- **2026-10-27**：规格改为 `design/gdd/league.md` rev 3、`talent.md` rev 3（简单规则 + 补漏洞、风格发牌与不回头规则、三份聘书 3–4 / 4–5 / 6–7 与注资、联赛新闻、带键随机数）。冠军疲劳的媒体义务在 economy.md，由 005 实现；013 先留接口。
- 现有 `OpponentClub` / `LeagueFactory` 换成通用俱乐部模型
- 对手的事件：二选一取默认，不弹窗

---

## Out of Scope

- 完整转会期（005）

---

## QA Test Cases

*以 GDD 的 Acceptance Criteria 为测试规格（已按 qa-lead 意见写成可测形式）*

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `Assets/Tests/EditMode/League/` 集成测试 + 接手 / 积分榜 / 赛前对手信息截图

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: 011, 012
- Unlocks: 005, 014
