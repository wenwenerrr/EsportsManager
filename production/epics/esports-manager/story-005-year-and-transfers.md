# Story 005: 年度循环和转会期

> **Epic**: 战队经理人 MVP
> **Status**: Ready
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: [fill before starting]
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 5（年度循环和转会期）

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.6.5f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (Player goal & fail state + MVP feature 5), scoped to this story:*

- [ ] 一年 = 4 个赛季 + 1 个转会期；每个赛季由若干赛段和比赛组成（数量可配置），赛季结束有排名
- [ ] 至少 5 支对手俱乐部由配置生成，各有资金、阵容和实力
- [ ] 转会期三选一：拍卖 / 全国选拔 / 出售或交易
- [ ] 拍卖：拍品为青训或成熟选手；对手俱乐部 AI 会出价；玩家支付的金额进入卖方俱乐部资金，卖方实力随资金提升
- [ ] 拍卖时玩家不出手，选手归出价最高的对手俱乐部
- [ ] 全国选拔：花费低于拍卖，消耗本次转会期；结果随机，天赋选手概率随队伍综合数值上升（公式可配置）；同一种子可复现
- [ ] 出售：获得资金，选手转入买方俱乐部
- [ ] 转会期结算年龄成长（调用 Story 001 的成长逻辑）

---

## Implementation Notes

- 对手俱乐部 AI 保持简单：按资金和缺口出价，不做复杂策略
- "资金 → 实力"的换算放配置

---

## Out of Scope

- Story 007：年终总结界面
- 历史模式、真实战队（简报明确不做）

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

*Governed by `qa.level`: at `qa.level: minimal` tests are **waived** (advisory), but a Visual/Feel or UI story's retained screenshot is not.*

**Story Type**: Integration
**Required evidence**: `Assets/Tests/EditMode/Season/YearAndTransferTests.cs` (advisory) + retained screenshot of the transfer screen in `production/qa/evidence/` (player-observable — required)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 004
- Unlocks: Story 006
