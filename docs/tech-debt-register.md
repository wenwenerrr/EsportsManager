## Technical Debt Register
Last updated: 2026-10-16
Total items: 1 | Estimated total effort: S

| ID | Category | Description | Files | Effort | Impact | Priority | Status | Added | Sprint |
|----|----------|-------------|-------|--------|--------|----------|--------|-------|--------|
| TD-001 | Code Quality | Data file breaks `.claude/rules/data-files.md`: file is `PlayerConfig.json` (rule: lowercase `[system]_[name].json`) and keys are camelCase (rule: snake_case, bound through a private wire-format DTO). Also `playersPerLane = 5` is a code-side default — from production/epics/esports-manager/story-001-player-model.md | Assets/Data/PlayerConfig.json, Assets/Scripts/Players/PlayerConfig.cs, Assets/Scripts/Players/PlayerConfigLoader.cs | S | Low | — | Resolved 2026-10-16 | 2026-10-15 | Backlog |
