# Epic: 战队经理人 MVP

> **Source**: `design/game-brief.md` (workflow: minimal — implicit epic synthesized by `/create-stories`)
> **Engine**: Unity 6000.6.5f1 | **Risk**: HIGH (`docs/engine-reference/unity/VERSION.md`)

**Goal:** 经营一支电竞战队，每个赛段在训练、运营和选手状态之间做取舍。冠军、流量还是生意，成功由你来定义。

**Scope:** the brief's 7 MVP features. **Ordering:** the brief's Build order.

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | 选手数据模型和虚构选手池 | Logic | Complete | N/A (minimal) |
| 002 | 战队三角和默契 | Logic | Complete | N/A (minimal) |
| 003 | 赛段行动逻辑 | Logic | Complete | N/A (minimal) |
| 004 | 比赛模拟、战报和积分榜 | Logic | Complete | N/A (minimal) |
| 005 | 年度循环、转会期与经济（v4 重写） | Integration | Ready | N/A (minimal) |
| 006 | 意外事件 | Logic | Complete | N/A (minimal) |
| 007 | 破产判定和年终总结 | UI | Ready | N/A (minimal) |
| 008 | 选手模型升级（按 numbers.md） | Logic | Complete | N/A (minimal) |
| 009 | 文字界面（第一个可玩版本） | UI | Complete | N/A (minimal) |
| 010 | 程序生成选手头像 | UI | Ready | N/A (minimal) |
| 011 | 比赛模型 v4 | Logic | In Progress | N/A (minimal) |
| 012 | 成长：专精与绝活 | Logic | Ready | N/A (minimal) |
| 013 | 联赛 AI 与预热 | Integration | Ready | N/A (minimal) |
| 014 | 自动模拟器与平衡验证 | Logic | Ready | N/A (minimal) |

**v4 实现顺序**（2026-10-22）：011 → 012 → 013 → 005 → 014 → 007 → 010
