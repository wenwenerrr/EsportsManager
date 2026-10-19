# Story 009: 文字界面（第一个可玩版本）

> **Epic**: 战队经理人 MVP
> **Status**: In Progress
> **Layer**: Presentation
> **Type**: UI
> **Estimate**: [fill before starting]
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-16

## Context

**GDD**: `design/game-brief.md` (MVP feature 3; Art & audio direction: 简洁的 2D 界面加选手卡片，第一版用文字和色块)
**Requirement**: Brief MVP feature 3（赛段行动分配）— the UI half; Build order step 1 "first playable"

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.6.5f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (Player goal & fail state + MVP feature 3), scoped to this story:*

- [ ] 主界面显示：战队三角（实力 / 人气 / 资金）、战队默契、行动点余量；资金可为负时显示负数
- [ ] 每名选手一张卡片：姓名、年龄、主分路、两个性格词（中文）、心情、压力、颜值、人气、薪资、合同剩余年限，以及主分路六维
- [ ] **潜力只显示估计区间**（用 `GetPotentialEstimate()`），任何位置都不得显示真实潜力
- [ ] 可以给每名选手分配行动（训练并选目标维度 / 直播 / 休息）、拉赞助；超出行动点或单人上限时界面给出提示并拒绝
- [ ] 商务弹窗：触发时弹出，显示选手、金额，可接受或拒绝；没有行动点时接受按钮不可用并说明原因
- [ ] 赛段结算后显示汇总：各项数值的变化（资金、人气、默契、心情、压力、训练收益）和商务 / 赞助结果
- [ ] **中文正常显示**：所有中文（性格词、行动名、维度名、选手名）渲染为字形，没有方框；使用带后备的中文字体并在 Unity 中实际验证
- [ ] 能在 Unity 中连续推进至少 4 个赛段不报错；使用固定种子可以复现
- [ ] 界面逻辑与游戏逻辑分离：界面只读取任务 003 的赛段状态并调用其入口方法，不复制公式
- [ ] **留下截图**（`production/qa/evidence/story-009-text-ui/`，用实际运行的游戏截取）：主界面、选手卡片、行动分配、商务弹窗、赛段结算各一张；截图经 `Read` 打开查看，对照以上标准检查文字是否被截断、是否有方框

---

## Implementation Notes

- UI 技术（UI Toolkit 或 UGUI）在 `/dev-story` 时决定；先查 `docs/engine-reference/unity/modules/ui.md` 和 `breaking-changes.md` 的 "6.3 → 6.6"（UXML 的 Factory/Traits 已移除，用 `[UxmlElement]`）
- 运行时配置通过 `[SerializeField] TextAsset` 交给各个加载器，不用 `Resources.Load`
- 运行时 asmdef（`EsportsManager.Runtime`）目前没有引用任何 UI 程序集，如用 uGUI / TextMeshPro 需要补引用；界面代码可以放单独的 asmdef
- 截图需要 `Assets/Scripts/ScreenshotOnArg.cs`，按 `.claude/docs/run-and-observe.md` 原样写入（`/dev-story` 会先征得同意）
- 文字和色块即可，不做美术
- 第一个场景和启动脚本也在本任务内（引导：读取配置、生成选手池、建立队伍、进入第一个赛段）；本任务可以用固定的演示数据启动游戏

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003：所有游戏逻辑与公式
- Story 004：比赛画面和战报（本任务只显示结算汇总）
- Story 005：赛季日历、转会期界面
- Story 007：破产画面和年终总结
- 存档

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

*Governed by `qa.level`: at `qa.level: minimal` tests are **waived** (advisory), but a Visual/Feel or UI story's retained screenshot is not.*

**Story Type**: UI
**Required evidence**: retained screenshot of each screen touched in `production/qa/evidence/story-009-text-ui/` (required — not waived at `qa.level: minimal`)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 003
- Unlocks: Story 004
