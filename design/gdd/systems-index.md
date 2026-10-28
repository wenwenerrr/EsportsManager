# Systems Index: 战队经理人

> **Status**: Draft (approved by user 2026-10-21; per-system GDDs pending)
> **Created**: 2026-10-21
> **Last Updated**: 2026-10-27
> **Source Concept**: design/game-brief.md (+ design/match-model-v4.md, design/numbers.md)

---

## Overview

经营一支电竞战队的模拟游戏：每个赛段分配行动，每年三项赛事和一个转会期，对手是同一套规则下运行的 7 支 AI 俱乐部。v3.4 之后的试玩和模拟显示策略空间坍缩，所以 v4 把"决定胜率的比赛模型"作为最底层，再在其上重做成长、经济、人才、联赛 AI 和转会，使整个联赛成为一个会演化的系统。★ 标记的六个系统在 `project.yaml` 的 `workflow_overrides.system_overrides` 中设为 `full`（8 节完整 GDD，评审加 `--review full`）；其余系统保持项目级 `minimal`（以 game-brief 为设计记录，不补 GDD）。**设计顺序以"两队比赛如何决定胜率"为起点**（用户决定 2026-10-21）。

---

## Systems Enumeration

| # | System Name | Category | Priority | Status | Design Doc | Depends On |
|---|-------------|----------|----------|--------|------------|------------|
| 1 | player-model 选手模型 | Core | MVP | Implemented (001, 008) | — (minimal) | — |
| 2 | team-state 战队状态（三角、默契、心情、压力） | Core | MVP | Implemented (002, 003) | — (minimal) | player-model |
| 3 | season-calendar 赛历和赛制 | Core | MVP | Implemented (004) | — (minimal) | — |
| 4 | ★ match 比赛 | Gameplay | MVP | In Review (rev 4, 2026-10-27 fixG write-back; replaces 004 power model) | design/gdd/match.md | player-model, team-state, season-calendar |
| 5 | ★ growth 成长（训练、专精、绝活） | Progression | MVP | In Review (2026-10-27 fixG write-back; full review + prototype sim done) | design/gdd/growth.md | player-model, team-state, match |
| 6 | segment-actions 赛段行动 | Gameplay | MVP | Implemented (003) | — (minimal) | team-state, growth |
| 7 | random-events 意外事件 | Narrative | MVP | Implemented (006) | — (minimal) | team-state, segment-actions |
| 8 | ★ economy 经济（收入、工资、赞助、工资指数） | Economy | MVP | In Review (2026-10-27 fixG write-back; full review + prototype sim done) | design/gdd/economy.md | player-model, segment-actions |
| 9 | ★ talent 人才（选手生成、潜力、青训） | Progression | MVP | In Review (2026-10-27 fixG write-back; full review + prototype sim done) | design/gdd/talent.md | player-model, economy |
| 10 | ★ league 联赛 AI（俱乐部决策、情报、预热） | Gameplay | MVP | In Review (2026-10-27 fixG write-back; full review + prototype sim done) (replaces 004 opponents) | design/gdd/league.md | match, growth, segment-actions, economy, talent |
| 11 | ★ transfer 转会（拍卖、选拔、挖角、甩卖） | Economy | MVP | In Review (2026-10-27 fixG write-back; full review + prototype sim done) (story 005) | design/gdd/transfer.md | economy, talent, league |
| 12 | year-review 破产和年终总结 | UI | MVP | Not Started (story 007) | — (minimal) | league, transfer |
| 13 | ui-screens 文字界面 | UI | MVP | Implemented (009, 004, 006) | — (minimal) | all |
| 14 | headless-simulator 自动模拟器与策略机器人 | Meta | MVP | Python prototype done (tools/sim/); C# port = story 014 | — (minimal) | match, growth, economy, league |
| 15 | avatars 程序生成头像 | UI | Vertical Slice | Not Started (story 010) | — (minimal) | player-model |
| 16 | patch 版本更新 | Gameplay | Full Vision | Not Started | — (GDD when scheduled) | match, transfer |

---

## Categories

| Category | Description |
|----------|-------------|
| **Core** | Player data, team state, calendar |
| **Gameplay** | Match, league AI, segment actions, patch |
| **Progression** | Growth, talent |
| **Economy** | Income, wages, transfers |
| **Narrative** | Random events |
| **UI** | Screens, avatars, year review |
| **Meta** | Simulator and strategy bots |

---

## Priority Tiers

| Tier | Definition | Target Milestone | Design Urgency |
|------|------------|------------------|----------------|
| **MVP** | Required for the core loop and for the anti-collapse checks | Playable v4 | Design FIRST |
| **Vertical Slice** | Faces | After v4 is stable | Design SECOND |
| **Alpha** | — | — | — |
| **Full Vision** | Yearly patch | After transfers | As needed |

---

## Dependency Map

### Foundation Layer
1. player-model, season-calendar — no dependencies (implemented)
2. team-state — depends on: player-model (implemented)

### Core Layer
1. ★ match — depends on: player-model, team-state, season-calendar. **The bottom design layer: what decides the win probability of two teams** (user decision 2026-10-21)
2. ★ growth — depends on: player-model, team-state, match (the stage mapping decides which dimensions matter)
3. segment-actions — depends on: team-state, growth (implemented)
4. ★ economy — depends on: player-model, segment-actions

### Feature Layer
1. random-events — depends on: team-state, segment-actions (implemented)
2. ★ talent — depends on: player-model, economy
3. ★ league — depends on: match, growth, segment-actions, economy, talent
4. ★ transfer — depends on: economy, talent, league
5. headless-simulator — depends on: match, growth, economy, league
6. patch — depends on: match, transfer

### Presentation Layer
1. year-review — depends on: league, transfer
2. ui-screens — depends on: all (implemented, extended per story)
3. avatars — depends on: player-model

---

## Recommended Design Order

Only the six ★ systems need a GDD.

| Order | System | Priority | Layer | Agent(s) | Est. Effort |
|-------|--------|----------|-------|----------|-------------|
| 1 | match | MVP | Core | game-designer, systems-designer | L |
| 2 | growth | MVP | Core | game-designer, systems-designer | M |
| 3 | economy | MVP | Core | economy-designer | M |
| 4 | talent | MVP | Feature | game-designer | M |
| 5 | league | MVP | Feature | game-designer, ai-programmer | L |
| 6 | transfer | MVP | Feature | economy-designer | M |

Implementation order differs: headless-simulator first (it validates every ★ GDD's numbers), then match, league (minimal), growth, economy/transfer.

---

## Circular Dependencies

- **economy <-> league**: the wage index needs league-wide total income; league AI needs economy formulas. Resolution: economy defines the formulas for *any one club* and writes the wage index as a function of a league aggregate that league supplies as an input; economy does not depend on league.
- **match <-> growth**: growth needs match's stage-to-dimension mapping; match needs the specialization and 绝活 effects. Resolution: match owns the mapping and defines the 绝活 effect hooks as an interface; growth owns how 绝活 are earned and lost.

---

## High-Risk Systems

| System | Risk Type | Risk Description | Mitigation |
|--------|-----------|-----------------|------------|
| league | Design | Most dependencies (5); AI could be exploitable or dull | Minimal AI first (phase 1), then simulator checks |
| match | Design | New model may still collapse to one best tactic | Headless simulator + anti-collapse checklist (match-model-v4 §14) |
| economy | Design | Money may stay non-binding | Simulator check 8; market-driven costs, not fines |
| growth | Design | Specialization may dominate or be a trap | Checklist 4 (specialist vs balanced overlap) |

---

## Progress Tracker

| Metric | Count |
|--------|-------|
| Total systems identified | 16 |
| ★ systems needing a GDD | 6 |
| Design docs started | 6 |
| Design docs reviewed | 6 (match full; others lean) |
| Design docs approved | 0 |
| MVP systems designed | 6/6 GDD systems |
| Vertical Slice systems designed | 0/0 |

---

## Next Steps

- [ ] `/design-system match` (first), then growth, economy, talent, league, transfer
- [ ] `/design-review design/gdd/<system>.md --review full` for each
- [ ] `/create-stories` after the six GDDs are reviewed (011/012/013/014 drafts shown in chat 2026-10-20; story 005 rewrite; 007 add league landscape)
