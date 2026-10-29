# Story 005: 年度循环、转会期与经济

> **Epic**: 战队经理人 MVP
> **Status**: In Progress
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: [fill before starting]
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-27 (rewritten for v4)

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

*2026-10-22 重写：依据 `design/gdd/transfer.md`、`design/gdd/economy.md`、`design/gdd/talent.md`（v4，三个系统设为 full）。原验收标准（对手资金按 §4.14 转化为补强）作废。*

- [ ] 年度总决赛后进入转会期，按 transfer.md 的固定顺序：年度更替 → 续约 → 挖角 → 三选一 → 拍卖会 / 选拔 / 挂牌 → 补签 → 新一年（赛历从第 1 赛段重来，赛事按年归档）
- [ ] 年度更替：年龄 +1、年龄衰退、合同 −1、绝活检查、自由选手退役、人才供应（talent.md F1）、工资指数（economy.md F3）
- [ ] 续约：要价按 economy.md F1（荣誉、绝活、工资指数）；界面显示年薪构成；AI 按 transfer.md 规则
- [ ] 挖角：AI 与玩家对等发出报价；被挖方选"接受转会费"或"加薪留人"；限制按 transfer.md
- [ ] 三选一：拍卖 / 全国选拔（talent.md F2）/ 出售（挂牌）；AI 按规则选择
- [ ] 拍卖：**多轮公开竞价**（加价 / 退出 / 自动跟价到上限），AI 心理价位按 transfer.md F3，加价幅度 F5；成交金额去向按 transfer.md
- [ ] 补签；名单 5～8 人；少于 5 人不能结束转会期
- [ ] 经济：所有俱乐部同一套收支；AI 资金为负时赛季中甩卖、卖到 5 人仍为负时联盟补助；玩家资金 < 0 破产（画面在 007）
- [ ] 预热年（013）改用完整转会期
- [ ] transfer.md AC 1～11、economy.md AC 1～9 有对应测试并通过
- [ ] 界面：转会期步骤进度、续约、挖角弹窗、拍卖竞价面板、补签；**留截图**到 `production/qa/evidence/story-005-transfers/`

---

## Implementation Notes

- **2026-10-27**：规格改为 `design/gdd/transfer.md` rev 3、`economy.md` rev 3（重谈 1.2 与拒绝后挂牌、挖角每队 ≤ 2 次且先挖卫冕冠军、拍卖 2+1 与 2 段工资储备、冠军疲劳、负反馈工资指数 R_ref、收入数值）。
- 选手换选手的交易第一版不做（用户决定 2026-10-22）
- 解决 TD-003 的剩余部分：滚动列表和共享 USS 主题（转会市场需要）

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
**Required evidence**: `Assets/Tests/EditMode/Transfer/` 集成测试 + 转会期各界面截图

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 011, 012, 013
- Unlocks: Story 007
