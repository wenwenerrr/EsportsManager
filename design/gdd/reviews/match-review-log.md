# Review Log: design/gdd/match.md

## Review — 2026-10-22 — Verdict: MAJOR REVISION NEEDED
Scope signal: L
Specialists: game-designer, systems-designer, qa-lead, gameplay-programmer (numeric probe), creative-director (senior)
Blocking items: 9 | Recommended: 14
Summary: The three-phase skeleton (phase strengths, per-phase short-board penalty, chemistry on mid, report built from G components) was judged sound. The tactic "read" layer failed: the worked example showed invade strictly dominant for A and a pure Nash equilibrium; the opponent's tactic was worth ~0.05 p; the model tilted toward early teams (0.56–0.65 at equal rating); the specialty-hook contract wrote to shared scalars with no evaluation order; ACs were not independently testable. Note: the review ran in the authoring session (specialists were independent subagents).
Prior verdict resolved: First review
Findings:
- [BLOCKING] Formulas/完整示例: prose contradicted the table (invade strictly dominant for A)
- [BLOCKING] Core Rules step 5: additive W_k made the opponent's tactic nearly irrelevant (~0.05 p)
- [BLOCKING] Core Rules step 6: early-end depended only on ΔE; Duel double-counted; early-only Duel → early tilt 0.56–0.65
- [BLOCKING] Core Rules 战报: the report could not name a wrong tactic
- [BLOCKING] Specialty hooks: SpecialtyFlat had no source; hooks wrote shared scalars; clamps incomplete
- [BLOCKING] Core Rules: evaluation order undefined (decider, phase_flat, command raw vs effective)
- [BLOCKING] Acceptance Criteria: AC19 fixture missing / ε=0 impossible; F9 and hooks untested
- [BLOCKING] Acceptance Criteria 25–28: undefined bots/metrics; AC25 near-always true
- [BLOCKING] Acceptance Criteria: config validation branches, bestOf=1, RNG order not specified
- [RECOMMENDED] Formulas F5/F11: command nearly worthless (+0.002 p); chemistry ≈ +2 rating
- [RECOMMENDED] Formulas F13: p_min/p_max should be one symmetric knob; saturation at |G|≈1.735k
- [RECOMMENDED] Formulas F7: f_early non-monotonic when one side leads early and late
- [RECOMMENDED] Formulas: wrong stated ranges (ΔE ±130 not ±40), missing variable tables (F2/F8/F9/F11)
- [RECOMMENDED] Tuning Knobs: missing interactions (sb↔w_Lmin, star counted 3×, B/T/exec one degree of freedom)
- [RECOMMENDED] Interactions: AI tactic policy and opponent info undefined
- [RECOMMENDED] Player Fantasy: "strong teams get targeted" has no mechanism here (belongs to league)
- [RECOMMENDED] UI/AC: preview function, state machine, symmetry property, UI ACs missing
Revision (same day, user decisions): depth moved to long-term building; tactic = light matchup fit with own-tactic G swing <= 3; star term in E and L counting only the excess over the mean lead; early-end only when E and L leaders differ (sb 0.15, w_Lmin 0.6); command also reduces variance; hooks own-team only with a 12-step evaluation order; p_floor 0.15; ACs rewritten (43) plus a separate Balance Validation section; no match minigame. Status set to In Review; awaiting re-review.
Reviewed-Content-Hash: not recorded — the reviewed pre-revision text was overwritten by the same-day revision, so a byte-identical skip must not apply to the revised document.
