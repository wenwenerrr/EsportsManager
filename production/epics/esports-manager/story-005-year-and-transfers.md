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

- [ ] 年末（任务 004 的年度总决赛之后）进入转会期，结束后开始新的一年：赛历从第 1 赛段重来，赛事数据按年归档（赛历和三项赛事本身在任务 004）
- [ ] 任务 004 的对手俱乐部增加资金，每赛段按 §4.14 把资金转化为补强
- [ ] **成功的代价**（2026-10-19 用户决定，试玩两年后"第二年开始稳定夺冠和盈利"）：
  - 涨薪：续约要价随选手近期成绩和人气上涨，冠军队工资明显变高（§4.4 增加成绩项，数值放配置）
  - 挖角：转会期对手会报价挖你表现最好的选手，玩家选择加薪留人或出售
  - 对手补强：对手每年用奖金和资金补强，联赛整体水平逐年上升
- [ ] 对手买入 / 卖出选手、拍到新人时直接改其阵容，§4.14 的状态区间以新阵容为基础（任务 004 的 `form` 起伏保留）
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

- Depends on: Story 004, Story 006 (order changed 2026-10-19: events come before the year loop)
- Unlocks: Story 007
