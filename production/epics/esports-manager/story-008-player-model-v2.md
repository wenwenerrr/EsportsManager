# Story 008: 选手模型升级（按 numbers.md）

> **Epic**: 战队经理人 MVP
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: [fill before starting]
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-16

## Context

**GDD**: `design/game-brief.md` (formulas and tables: `design/numbers.md` §2, §3, §4.1, §4.4)
**Requirement**: Brief MVP feature 1（虚构选手池和选手属性）— revised by `design/numbers.md`; supersedes parts of Story 001

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.6.5f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (Player goal & fail state + MVP feature 1) and `design/numbers.md`, scoped to this story:*

- [ ] 性格：十组对立性格由配置定义（numbers.md §3.1）；每名选手恰好有 2 个性格词，来自**不同的组**；生成的选手池里不会出现同组两个词
- [ ] 性格效果：效果表放配置（§3.2 v2，倍率均在 ±5% 以内）；提供"取某项效果倍率"的方法，结果为两个性格词倍率之积，未列出的为 1；配置里引用了不存在的性格词**或不存在的效果键**时校验报错（效果键白名单）
- [ ] 性格相性 `H(i,j)`：按相性表（§3.3，无序配对）把两人各 2 个词的 4 种组合相加，限制在 [-1, +1]；未列出的组合为 0；**两人中有一人是圆滑时，负值项乘圆滑系数**；提供"这一对的熟悉度系数"（两人中有一人是温和即乘温和系数），供任务 002 使用
- [ ] 潜力（§4.15）：新增属性，0–100，**对玩家隐藏**；提供估计区间（中心含生成时的随机偏差，半宽随观察度缩小但不低于 `u_min`）；观察度 0–100 可增加、可清零；提供调整潜力的方法（钳制 0–100，供任务 006 的开窍 / 低谷事件使用）；潜力 ≥ 配置阈值（初值 85）判为天赋选手
- [ ] 生成（§4.13）：维度按潜力比例生成——主分路 = 潜力 × U(0.6, 0.95)，非主分路 = 潜力 × U(0.35, 0.75)；每名选手有**稳定唯一的 ID**（供任务 002 按"每一对选手"记录熟悉度，以后存档也用它）
- [ ] 合同年限：新增剩余年限（整数，≥0），每过一年减 1，提供"是否到期"
- [ ] 训练收益函数（§4.1 v2）：`T_base × 强度 × max(1 − 维度/潜力, ρ) × 性格训练系数 × 年龄系数 × (1 − k × 压力/100)`；**潜力是软上限**——超过潜力后收益保持在正常的 ρ（初值 0.15），不为 0；年龄越大系数越小；结果确定（无随机）
- [ ] 过一年：不再自动成长；≥ 衰退起始年龄（配置，初值 27）的选手各维度按年龄分段衰退，不低于 0；年龄 +1
- [ ] 年薪公式（§4.4）：由主分路评分、人气、潜力分和性格要价系数算出，随机项通过注入的随机源；**不设工资帽**（不钳制到 S_max）；生成的选手池使用该公式给出初始年薪
- [ ] 配置规范化（解决 TD-001）：选手配置改名为 `player_config.json`，键名用 `snake_case`，通过内部转换类（DTO）读入；键名缺失或拼错时报错，不再悄悄变成 0；所有数值范围有校验，**非有限值（NaN / 无穷大）报错**；数值写成字符串等错误类型的行为由测试固定下来

---

## Implementation Notes

- 在任务 001 的基础上修改（`Assets/Scripts/Players/`、`Assets/Data/`、`Assets/Tests/EditMode/Players/`），仍是纯 C# 逻辑，随机数通过注入的随机源
- 所有数值放配置（性格组、效果表、相性表、训练系数、年龄系数、衰退曲线、年薪范围和权重），不写死
- 数据文件遵守 `.claude/rules/data-files.md`：小写文件名 `[系统]_[名称].json`、snake_case 键、内部 DTO 对接 `JsonUtility`；数值含义写在代码里的文档注释中
- 金额用整数（元）
- **删除任务 002 的旧代码**（用户已同意，开发时仍需列出具体文件再确认一次）：`Assets/Scripts/Team/`、`Assets/Data/team_config.json`、`Assets/Tests/EditMode/Team/` 及它们的 `.meta`。旧代码在提交 `218fa89` 中可找回；任务 002 将按 numbers.md 重写
- 更新任务 001 里因设计变更而改变的测试（性格、过一年）；其余测试保持通过
- 写代码前查 `docs/engine-reference/unity/`（Unity 6.6 比训练数据新）

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002（重写）：战队层面数值——两两熟悉度、默契、人气目标值、收支
- Story 003：训练、直播、休息等行动本身（本任务只提供训练收益函数）
- Story 005：身价、起拍价、拍卖、全国选拔、续约流程

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

*Governed by `qa.level`: at `qa.level: minimal` tests are **waived** (advisory), but a Visual/Feel or UI story's retained screenshot is not.*

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/Players/` — updated `PlayerModelTests.cs` plus new test classes as needed (advisory at `qa.level: minimal`; the user has asked for tests anyway)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (Complete)
- Unlocks: Story 002 (to be rewritten), then Story 003

---

## Completion Notes
**Completed**: 2026-10-16
**Criteria**: 10/10 passing (no deferred items)
**Deviations**: ADVISORY — (1) "potential hidden from the player" is a convention only: `Player.Potential` is public (doc comment says UI must not show it); UI must use `GetPotentialEstimate()`. (2) `Assets/Scripts/Players/JsonShapeChecker.cs` (251 lines) is outside the story's file scope — a strict pre-parse that rejects wrong types, missing/unknown/duplicate keys and nulls, going beyond "pin the behaviour with tests".
**Test Evidence**: Logic: `Assets/Tests/EditMode/Players/` (PlayerModelTests, PersonalityTests, TrainingAndAgingTests, PlayerConfigLoadingTests, TestConfigFactory) — 222 tests, 222 passed, 0 failed (`test-results/editmode.xml`, from the /dev-story run; /story-done did not re-run them). Waived at `qa.level: minimal`, written anyway at the user's request.
**Code Review**: Skipped (solo mode); unity-specialist reviewed twice, findings addressed.
**Run result**: N/A — pure data/logic layer, no player-visible surface (first UI is Story 003)
**Also**: resolved TD-001; deleted Story 002's old WIP files (user-approved; recoverable from commit 218fa89).
