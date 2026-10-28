# v4 whole-system prototype (Python)

Prototype of the six v4 systems (match, growth, economy, talent, league, transfer), used to validate balance before C# implementation.

- `sim5.py`: simulator; current settings = fixG (2026-10-27). Defaults reproduce the base run.
- `run_batch5.py`, `ablate5.py`, `run_fixG.py`: batch runs (write to results5/, not committed).
- `analyze5.py`, `compare5g.py`: Balance Validation tables.
- `verify5.py`: checks GDD formula examples / ACs.
- `cyc2.py`: match-only cycle reference.

Run from this folder: `python3 run_fixG.py`. The C# headless simulator (story 014) must reproduce these metrics.
