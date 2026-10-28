"""Re-verify the revised GDD formula examples / ACs with sim5's functions, plus match-only BV1-3/5, GV1, GV5."""
import sys, os, math, random, statistics, copy
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sim5
from sim5 import *

OUT = []


def chk(tag, got, exp, tol=0.005):
    ok = all(abs(g - e) <= tol for g, e in zip(got, exp)) if isinstance(got, (list, tuple)) else abs(got - exp) <= tol
    OUT.append((tag, ok, got, exp))


def fmt(x):
    if isinstance(x, (list, tuple)):
        return '[' + ', '.join('%.4f' % v for v in x) + ']'
    return '%.4f' % x


# ------------------------------------------------------------------ helpers to build a side from phase ratings
def side_of(rE, rM, rL, chem=70, cmd=70, stress=0, res=1.0, dec=(1, 1, 1, 1, 1), hk=None, decider=False):
    parts = [(rE[i], rM[i], rL[i], cmd if i == 0 else 0.0, res, dec[i], stress) for i in range(5)]
    return side_from(parts, chem, hk or NEUTRAL_HK, dec=decider)


AE = [66, 70, 66, 64, 66]; AM = [60, 62, 60, 58, 62]; AL = [56, 56, 57, 56, 57]
BE = [58, 60, 58, 56, 58]; BM = [62, 62, 63, 60, 62]; BL = [70, 68, 70, 72, 68]
A = side_of(AE, AM, AL)
B = side_of(BE, BM, BL)

# ---- match AC15/16 (F1)
class FakeP:
    pass


def vprime(v, mood, stress, resolute=False, main=True, clever=False):
    res = 0.95 if resolute else 1.0
    f = (P['m0'] + (1 - P['m0']) * mood / 100.0) * (1 - P['kappa_s'] * stress / 100.0 * res)
    if not main:
        f *= 0.8 * (1.03 if clever else 1.0)
    return v * f


chk('match AC15 v\'', [vprime(70, 80, 40), vprime(70, 80, 40, True), vprime(70, 80, 40, main=False), vprime(70, 80, 40, main=False, clever=True)],
    [67.914, 67.983, 54.331, 55.961])
chk('match AC16 worst v\'', vprime(100, 0, 100, main=False), 72.2)
# ---- AC18 / 19 / 20 / 21 / 22
S = side_of(AE, [60] * 5, [60] * 5, chem=50)
chk('match AC18 S_E', S.S['E'], 66.296)
lam0 = P['lamE']
P['lamE'] = 0.0; s0 = side_of(AE, [60] * 5, [60] * 5).S['E']
P['lamE'] = 0.5; s5 = side_of(AE, [60] * 5, [60] * 5).S['E']
P['lamE'] = lam0
chk('match AC18 lam 0 / 0.5', [s0, s5], [66.702, 65.351])
chk('match AC19 all zero', side_of([0] * 5, [0] * 5, [0] * 5, chem=50).S['E'], 0.0)
chk('match AC20 chem 0/70/100 mid add', [side_of(AE, [60] * 5, AL, chem=c).S['M'] - 60 for c in (0, 70, 100)], [-5, 2, 5])
ex = lambda chem, cmd, add=0.0: clamp(P['e0'] + P['e1'] * chem / 100 + P['e2'] * cmd / 100 + add, 0.3, 1.0)
chk('match AC21 exec', [ex(70, 70), ex(0, 0), ex(100, 100), ex(70, 70, .5), ex(70, 70, .1)], [0.79, 0.3, 1.0, 1.0, 0.89])
chk('match AC22 cmd', [min(100, max(80, 78 * 1.05)), min(100, 98 * 1.05)], [81.9, 100])


# ---- AC23 star terms
def stars(lead, ssA=1.0):
    bA = side_of([60 + x for x in lead], [60] * 5, [60] * 5, hk=({'E': 0, 'M': 0, 'L': 0}, {'E': ssA, 'L': 1}, 0, 1))
    bB = side_of([60] * 5, [60] * 5, [60] * 5)
    lead_ = lead
    m = sum(lead_) / 5
    sa = min(P['star_cap'], max(0, max(lead_) - max(0, m) - P['st0']) * ssA)
    sb = min(P['star_cap'], max(0, -min(lead_) - max(0, -m) - P['st0']))
    return sa, sb, star_diff(bA, bB, 'E')


chk('match AC23 star', [stars([8, 10, 8, 8, 8])[0], stars([20, 0, 0, 0, 0])[0], stars([30, 0, 0, 0, 0])[0],
                        stars([10, -5, -5, -5, -5])[0], stars([10, -5, -5, -5, -5])[1], stars([20, 0, 0, 0, 0], 1.5)[0]],
    [0, 12, 15, 6, 0, 15])
# ---- AC24-28 full example
d, f, base = core(A, B)
chk('match AC24 Delta', [d['E'], d['M'], d['L']], [8.447, -1.673, -13.260], 0.002)
chk('match AC25 f', [f['E'], f['M'], f['L']], [0.9415, 1.0, 0.7043], 0.0005)
chk('match AC26 Base', base, -1.041, 0.002)
fa = clamp(P['a'] * A.ex * d['E'], -1, 1); fb = clamp(P['a'] * B.ex * (-(-d['L'])) if False else P['a'] * B.ex * (-d['L']), -1, 1)
chk('match AC27 Fit', [fa, fb], [0.667, 1.0])
exA, exB = 0.79, 0.5
cnt = lambda tA, tB: (P['C'] * exA if (tA, tB) in BEATS else (-P['C'] * exB if (tB, tA) in BEATS else 0.0))
chk('match AC28 counter', [cnt('invade', 'develop'), cnt('develop', 'invade'), cnt('teamfight', 'teamfight')], [0.395, -0.25, 0.0])
# continuity (AC26 second half)
def base_of(dE, dM, dL):
    dd = {'E': dE, 'M': dM, 'L': dL}
    ff = {'E': 1.0, 'M': 1.0, 'L': 1.0}
    for X, Y in CYC:
        if dd[X] * dd[Y] < 0:
            ff[Y] = max(P['w_min'], 1 - P['s_cyc'] * abs(dd[X]) / 10)
    return (P['BE'] * ff['E'] * dE + P['BM'] * ff['M'] * dM + P['BL'] * ff['L'] * dL) / (P['BE'] + P['BM'] + P['BL'])
chk('match AC26 continuity |dBase|', abs(base_of(30, -10, 0.01) - base_of(30, -10, -0.01)), 0.0, 0.01)
f25 = []
dd = {'E': 100, 'M': 5, 'L': -10}
for X, Y in CYC:
    pass
fL = max(P['w_min'], 1 - P['s_cyc'] * 100 / 10)
chk('match AC25 fL floor', fL, 0.4)
# ---- AC29 budget (exec 1, random hooks)
rr = random.Random(7)
worst = 0.0
for _ in range(1000):
    hkA = ({k: rr.uniform(-5, 5) for k in PH}, {'E': rr.uniform(.5, 2), 'L': rr.uniform(.5, 2)}, 1.0, rr.uniform(.7, 1.3))
    hkB = ({k: rr.uniform(-5, 5) for k in PH}, {'E': rr.uniform(.5, 2), 'L': rr.uniform(.5, 2)}, 1.0, rr.uniform(.7, 1.3))
    sA = side_of([rr.uniform(30, 90) for _ in range(5)], [rr.uniform(30, 90) for _ in range(5)], [rr.uniform(30, 90) for _ in range(5)], chem=100, cmd=100, hk=hkA)
    sB = side_of([rr.uniform(30, 90) for _ in range(5)], [rr.uniform(30, 90) for _ in range(5)], [rr.uniform(30, 90) for _ in range(5)], chem=100, cmd=100, hk=hkB)
    cr = core(sA, sB)
    for tb in TACTICS:
        gs = [gtac(cr, sA, sB, ta, tb) for ta in TACTICS]
        worst = max(worst, max(gs) - min(gs))
OUT.append(('match AC29 max own-tactic G swing (<= 3.0)', worst <= 3.0 + 1e-9, worst, 3.0))
# ---- AC30 sigma
sig = lambda st, cmd, res=1.0, sc=1.0: (P['g0'] + P['g1'] * st / 100 * res) * (1 - P['ccmd'] * cmd / 100) * sc
chk('match AC30 sigma', [sig(0, 0), sig(100, 0), sig(0, 100) / sig(0, 0), sig(40, 70, 1.0, 0.8)], [2.0, 8.0, 0.7, 2.781])
chk('match F11 example sigma', sig(40, 70), 3.476)
# ---- AC31 9 combos
exp_tab = [[0.509, 0.472, 0.469], [0.497, 0.459, 0.420], [0.445, 0.445, 0.406]]
got = []
cr = core(A, B)
for i, ta in enumerate(TACTICS):
    for j, tb in enumerate(TACTICS):
        got.append(pwin(gtac(cr, A, B, ta, tb)))
chk('match AC31 p0 table (row A inv/tf/dev x col B inv/tf/dev)', got, [x for r in exp_tab for x in r], 0.002)
OUT[-1] = OUT[-1][:2] + (got, [x for r in exp_tab for x in r])
# ---- AC32
chk('match AC32 p(G)', [pwin(0), pwin(8), pwin(-0.979), pwin(100), pwin(-100), pwin(1e6)], [0.5, 0.7311, 0.4694, 0.85, 0.15, 0.85], 0.0005)
chk('match F12 example G', gtac(cr, A, B, 'invade', 'develop'), -0.979, 0.002)
# ---- AC33 decider
dec = (1.03, 1.03, 1.03, 0.97, 0.97)
chk('match AC33 decider mean', sum(dec) / 5, 1.006, 1e-9)
Ad = side_of(AE, AM, AL, dec=dec, decider=True)
chk('match AC33 S_E decider = recomputed with v*1.006', Ad.S['E'], side_of([x * 1.006 for x in AE], AM, AL).S['E'], 1e-9)
# ---- AC36/37 hooks
hk = hooks_of({'initiator'})
Ah = side_of(AE, AM, AL, hk=({'E': 0, 'M': 3, 'L': 0}, {'E': 1, 'L': 1}, 0, 1))
chk('match AC36 phase_flat M +3 -> S_A,M', Ah.S['M'], 64.236, 0.002)
# ---- full-example S
chk('match example S_A', [A.S[k] for k in PH], [66.296, 61.236, 56.322], 0.002)
chk('match example S_B', [B.S[k] for k in PH], [57.849, 62.909, 69.583], 0.002)

# ------------------------------------------------------------------ growth ACs
lg = League(1)
rg = random.Random(3)


def mkp(lane='jungle', age=21, pot=65, mood=60, stress=30, focus=0.5, dims=50.0, traits=('loyal', 'reserved')):
    p = gen_player(lg, rg, lane, age, age, 60)
    p.age = age
    p.pot = pot
    p.mood = mood
    p.stress = stress
    p.traits = traits
    p.tm = {k: 1.0 for k in TKEYS}
    p.radar[lane] = [dims] * 6
    p.focus = {'E': focus, 'M': focus, 'L': focus}
    p.specs = set()
    p.spec_src = {}
    return p


p = mkp()
dl = point_deltas(p, 'E', DIMS['jungle'].index('gold_per_min'), 1.0)
b = P['T_base'] * 1.0 * 1.04 * 0.85 * 1.3
chk('growth AC1 b', b, 2.298)
chk('growth AC2 mood_t / stress_t', [P['mt0'] + P['mt1'] * 0, P['mt0'] + P['mt1'] * 1, 1 - P['k_s']], [0.8, 1.2, 0.5])
chk('growth AC3 head pot65 dims 50/70/75/80', [clamp((75 - d) / 25, 0, 1) for d in (50, 70, 75, 80)], [1, .2, 0, 0])
chk('growth AC4 deltas gpm/kda/kc/other', [dl[DIMS['jungle'].index('gold_per_min')], dl[DIMS['jungle'].index('kda')],
                                          dl[DIMS['jungle'].index('kill_contribution')], dl[DIMS['jungle'].index('teamfight_participation')]],
    [3.447, 2.298, 2.298, 0.230])
q = mkp(); q.radar['jungle'] = [74.5] * 6
apply_deltas(q, [3.0] * 6)
chk('growth AC5 cap clamp', q.radar['jungle'][0], 75.0)
p6 = mkp()
dlc = point_deltas(p6, 'C', None, 1.0)
chk('growth AC6 command point: cmd +b*head (spec 1), others x0.1', [dlc[CMD], dlc[0]], [2 * 1.04 * 0.85 * 1.0, 2 * 1.04 * 0.85 * 0.1])
mu = P['mu']
f = {'E': .2, 'M': .2, 'L': .2}
for _ in range(2):
    f = {k: (1 - mu) * f[k] + (mu if k == 'E' else 0) for k in PH}
chk('growth AC8 focus 2 pts E', [f['E'], f['M'], f['L']], [0.440, 0.140, 0.140])
chk('growth AC11 relearn 0.29 -> ', (1 - mu) * 0.29 + mu, 0.406)
chk('growth AC12 0.55 one other pt', (1 - mu) * 0.55, 0.460)
chk('growth F4 interval checks', [P['unlock_f'] - (mu + (1 - mu) * P['lose_f']), (1 - mu) * P['unlock_f'] - P['lose_f']], [0.136, 0.160], 0.002)
x = 0.4395
seq = []
for _ in range(2):
    for __ in range(2):
        x = (1 - mu) * x + mu
    seq.append(x)
y = seq[-1]
x2 = 0.725; seq2 = []
for _ in range(3):
    x2 *= (1 - mu) ** 2
    seq2.append(x2)
chk('growth F3 example 0.608/0.725', seq, [0.608, 0.725])
chk('growth F3 example 0.508/0.356/0.249', seq2, [0.508, 0.356, 0.249])
# order dependence of focus within one segment (AC7 claims order-independent result)
fa_ = {'E': .3, 'M': .3, 'L': .3}
def appl(f, k):
    return {kk: (1 - mu) * f[kk] + (mu if kk == k else 0) for kk in PH}
o1 = appl(appl(fa_, 'E'), 'M'); o2 = appl(appl(fa_, 'M'), 'E')
OUT.append(('growth AC7 focus order-independence (E then M vs M then E)', abs(o1['E'] - o2['E']) < 1e-9, [o1['E'], o2['E']], 'equal'))
# phase-cost equality (growth Overview claim) with focus dim
def phase_gain(lane, k, focus_dim=True):
    p = mkp(lane=lane, focus=0.0, dims=50.0, pot=90)
    fd = PIDX[lane][k][0] if focus_dim else None
    dl = point_deltas(p, k, fd, 1.0)
    return mean(dl[i] for i in PIDX[lane][k])
pg = {(l, k): phase_gain(l, k) for l in LANES for k in PH}
pg0 = {(l, k): phase_gain(l, k, False) for l in LANES for k in PH}
OUT.append(('growth "equal phase cost": phase-rating gain per point WITH focus dim (1-dim vs 3-dim phases)',
            max(pg.values()) - min(pg.values()) < 1e-6, [min(pg.values()), max(pg.values())], 'equal'))
OUT.append(('growth "equal phase cost": WITHOUT focus dim', max(pg0.values()) - min(pg0.values()) < 1e-6,
            [min(pg0.values()), max(pg0.values())], 'equal'))
# GV1 calibration (21y, pot 65, mood 60, stress 30, start 0.775*pot, one phase, 6 or 12 pts/yr; noise 1)
def calib(pot, pts_per_year, lane, k, use_focus, years=3, per_seg=1):
    p = mkp(lane=lane, age=21, pot=pot, focus=0.2, dims=0.775 * pot)
    init_focus(p)
    out = []
    for y in range(years):
        r0 = praw(p, k)
        segs = pts_per_year // per_seg
        for s in range(segs):
            fd = (SPEC_DIM.get((lane, k)) if SPEC_DIM.get((lane, k)) is not None else lowest_dim(p, k)) if use_focus else None
            acc = [0.0] * 6
            for _ in range(per_seg):
                dl = point_deltas(p, k, fd, 1.0)
                acc = [a + b for a, b in zip(acc, dl)]
            apply_deltas(p, acc)
            for _ in range(per_seg):
                p.focus = {kk: (1 - mu) * p.focus[kk] + (mu if kk == k else 0) for kk in PH}
        out.append(praw(p, k) - r0)
        p.age += 1
    return out
cal = {}
for lane in LANES:
    for k in PH:
        for uf in (True, False):
            cal[(lane, k, uf)] = calib(65, 6, lane, k, uf)
v1 = [v[0] for v in cal.values()]
GV1 = dict(min=min(v1), max=max(v1), mean=mean(v1),
           jungleE_focus=cal[('jungle', 'E', True)], topE_focus=cal[('top', 'E', True)], adcL_focus=cal[('adc', 'L', True)],
           jungleE_nofocus=cal[('jungle', 'E', False)])
cal12 = calib(65, 12, 'jungle', 'E', True, years=2, per_seg=2)
cal80 = calib(80, 6, 'jungle', 'E', True, years=2)
GV1['12pts_jungleE'] = cal12
GV1['pot80_jungleE'] = cal80

# ------------------------------------------------------------------ economy ACs
lg2 = League(2)
pe = mkp(lane='mid', age=21, pot=75)
pe.bias = 0.0; pe.obs = 0.0; pe.pop = 60
w = DIMW['mid']
pe.radar['mid'] = [70.0] * 6
pe.honor = 80
pe.specs = {'tempo_mid'}
chk('economy AC1 salary (wan)', market_salary(pe, 1.2) / 1e4, 24.77, 0.01)
pe.honor = 0; pe.specs = set()
chk('economy AC1 base salary (wan)', market_salary(pe, 1.0) / 1e4, 14.04, 0.01)
wi = lambda w0, R: clamp(w0 * (1 + P['kappa_wi'] * (R - 1)), P['WI_min'], P['WI_max'])
chk('economy AC3 WI', [wi(1, 1.06), wi(1, 2.0), wi(1, 0.3), wi(1, 1.0), wi(1, 0.9)], [1.03, 1.3, 0.8, 1.0, 0.95])
pv = mkp(lane='mid', age=21, pot=80); pv.bias = 0; pv.obs = 0; pv.radar['mid'] = [75.0] * 6; pv.specs = {'tempo_mid'}
chk('economy AC4 value (wan)', value_of(pv) / 1e4, 49.95, 0.01)
chk('economy AC5 year factor 22y/30y 3 yrs', [year_factor(22, 3), year_factor(30, 3)], [1.12, 0.92])


def sat_of(sal, mk):
    x = (sal - mk) / mk
    return clamp(math.copysign(max(0, abs(x) - 0.1), x), -0.5, 0.2)


chk('economy AC6 satisfaction', [sat_of(10, 10.5), sat_of(10, 13), sat_of(10, 25)], [0.0, -0.131, -0.5], 0.001)
chk('economy AC8 self-rescue result (wan)', -10 + 8 * .6 + 12 * .6, 2.0)

# ------------------------------------------------------------------ talent ACs
def talent_mean(pop, n=2000, seed=11):
    r = random.Random(seed)
    mu_ = P['y0'] + P['y1'] * pop / 100
    return mean(clamp(r.gauss(mu_, P['sig_pot']), 40, 84) for _ in range(n))
m40 = talent_mean(40)
OUT.append(('talent AC1 mean pot @pop40 in [55.2,57.2]', 55.2 <= m40 <= 57.2, m40, '55.2-57.2'))
d = talent_mean(70, 20000, 3) - talent_mean(30, 20000, 4)
chk('talent AC2 mean diff pop30 vs 70', d, 5.69, 0.5)
# AC5: talented jungler gpm 82, E avg highest -> has jungle_king at generation?
pt = mkp(lane='jungle', age=18, pot=90, dims=70)
pt.radar['jungle'][DIMS['jungle'].index('gold_per_min')] = 82
pt.radar['jungle'][DIMS['jungle'].index('kda')] = 80
pt.radar['jungle'][DIMS['jungle'].index('kill_contribution')] = 80
init_focus(pt); pt.specs = set(); pt.spec_src = {}
check_specs(pt, 'gen')
OUT.append(('talent AC5 talented jungler (gpm 82, E best) owns jungle_king at generation', 'jungle_king' in pt.specs,
            'focus_E=%.2f specs=%s' % (pt.focus['E'], sorted(pt.specs)), 'jungle_king'))
tp = lambda need: clamp(P['t0'] + P['t1'] * need, P['t0'], P['t_max'])
chk('talent AC6 scout talent p', [tp(1), tp(0.5), tp(0)], [0.25, 0.15, 0.05])
chk('talent F1 example mu @pop40', P['y0'] + P['y1'] * .4, 56)

# ------------------------------------------------------------------ transfer ACs
chk('transfer AC1 decline 26/27/29/31', [decline(26), decline(27), decline(29), decline(31)], [0, 1, 2, 4])
mb = 400000 * (0.9 + 0.5 * 1) * (1 + 0.2 * 0.5)
chk('transfer AC5 / F4 max bid (wan)', mb / 1e4, 61.6, 0.01)
st59 = round100(max(5000, 0.05 * 590000)); st58 = round100(max(5000, 0.05 * 580000))
OUT.append(('transfer AC5 exit at 59 / raise at 58', (590000 + st59 > mb) and (580000 + st58 <= mb), [st59, st58], 'exit,raise'))
# AC6: run the open auction with two AIs (limits 40/30, pop A > B)
def lot_sim(start, limits):
    order = sorted(limits, key=lambda x: -x[1])
    cur = start; high = None; active = {n for n, _, _ in limits}
    while True:
        raised = False
        for n, pop, lim in order:
            if n not in active or n == high:
                continue
            step = round100(max(P['step_min'], P['step_pct'] * cur))
            if cur + step <= lim:
                cur += step; high = n; raised = True
            else:
                active.discard(n)
        if not raised:
            break
    return high, cur
wnr, price = lot_sim(200000, [('A', 60, 400000), ('B', 50, 300000)])
OUT.append(('transfer AC6 A wins at 28.6-31.5 wan', wnr == 'A' and 286000 <= price <= 315000, [price / 1e4], 'A, 28.6-31.5'))
wnr7, price7 = lot_sim(200000, [('player', 70, 350000), ('B', 50, 400000)])
OUT.append(('transfer AC7 player auto-follow to 35 vs B 40 -> B wins', wnr7 == 'B', [price7 / 1e4], 'B'))
chk('transfer F2 examples (wan)', [33 * 0.4, 30 * 0.65 * (0.85 + 0.1 * 2)], [13.2, 20.475], 0.06)
chk('transfer F3 step at 30 wan / 6 wan', [round100(max(5000, 0.05 * 300000)), round100(max(5000, 0.05 * 60000))], [15000, 5000], 0.5)
chk('transfer AC2 bench renew threshold', [1.6 >= P['bench_renew'], 1.4 >= P['bench_renew']], [1, 0])
chk('league F2 floor/target @W 4.75 wan frugal 0/0.5/1 (wan)',
    [(3 + 3 * f) * 4.75 for f in (0, .5, 1)] + [(3 + 3 * f) * 4.75 + (6 + 6 * f) * 4.75 for f in (0, .5, 1)],
    [14.3, 21.4, 28.5, 42.8, 64.1, 85.5], 0.06)
chk('league AC4 rest thresholds aggr 0/1 (normal, pre-event)', [55 + 0, 55 + 15, 55 - 15, 70], [55, 70, 40, 70])
OUT.append(('league F2 example: aggr 0.2 -> 58, pre-event 46', abs((55 + 3) - 58) < 1e-9 and abs((58 - 15 * 0.8) - 46) < 1e-9,
            [58, 58 - 12], [58, 46]))

# ------------------------------------------------------------------ match-only BV (match.md generator)
def gen_team(rnd, style, bias, mean_=60.0):
    rs = {}
    for k in PH:
        off = 0.0 if style is None else (2 * bias / 3 if k == style else -bias / 3)
        rs[k] = [mean_ + off + rnd.gauss(0, 4) for _ in LANES]
    return side_of(rs['E'], rs['M'], rs['L'])


def eq(Aa, Bb):
    return eq_value(Aa, Bb)


def bv1(bias=9, n=1000, seed=1):
    rnd = random.Random(seed)
    res = {}
    swings = []
    for s1, s2 in [('E', 'L'), ('M', 'E'), ('L', 'M'), ('E', None), ('M', None), ('L', None)]:
        vs = []
        for _ in range(n):
            Aa, Bb = gen_team(rnd, s1, bias), gen_team(rnd, s2, bias)
            v, M = eq(Aa, Bb)
            vs.append(v)
            for j in range(3):
                col = [M[i][j] for i in range(3)]
                swings.append(max(col) - min(col))
        res['%sv%s' % (s1, s2 or 'bal')] = mean(vs)
    return res, swings


BV1, SW = bv1()
BV1_12, _ = bv1(12)
BV1_b0, SW0 = bv1(0)
# author's cyc2 reference on the same generator
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
try:
    import cyc2
    ref = cyc2.pairs(cyc2.mk(s=.35, wmin=.4, st0=4, B={'E': 1, 'M': 1.1, 'L': 1}), True, bias=9, n=1000, seed=1)
except Exception as e:
    ref = str(e)
budget_p = pwin(1.5) - pwin(-1.5)


def bv3():
    rnd = random.Random(5)
    out = {}
    for gap in (3, 5, 10):
        ps, ps_same = [], []
        for _ in range(1000):
            Bt = {k: [60 + rnd.gauss(0, 4) for _ in LANES] for k in PH}
            At = {k: [x + gap for x in Bt[k]] for k in PH}
            Aa, Bb = side_of(At['E'], At['M'], At['L']), side_of(Bt['E'], Bt['M'], Bt['L'])
            v, M = eq(Aa, Bb)
            ps.append(v)
            ps_same.append(mean(M[i][i] for i in range(3)))
        out[gap] = (mean(ps), mean(ps_same))
    return out


BV3 = bv3()
# BV5: same lineup stress 50 -> 20 vs identical opponent at stress 50
def side_stress(stress, rs):
    f = (P['m0'] + (1 - P['m0']) * 0.7) * (1 - P['kappa_s'] * stress / 100.0)
    return side_from([(rs['E'][i] * f, rs['M'][i] * f, rs['L'][i] * f, 70 * f if i == 0 else 0, 1.0, 1.0, stress) for i in range(5)], 70, NEUTRAL_HK)
rnd = random.Random(9)
d5 = []
for _ in range(1000):
    rs = {k: [65 + rnd.gauss(0, 4) for _ in LANES] for k in PH}
    B50 = side_stress(50, rs)
    A20, A50 = side_stress(20, rs), side_stress(50, rs)
    d5.append(eq(A20, B50)[0] - eq(A50, B50)[0])
BV5 = (mean(d5), max(d5))

# GV5: each specialty's dG on the match.md example fixture (A gets the specialty), A invade vs B develop, and eq value
GV5 = {}
G0 = gtac(core(A, B), A, B, 'invade', 'develop')
V0 = eq(A, B)[0]
for sid in SPECS:
    hk = hooks_of({sid})
    As = side_from([(AE[i], AM[i], AL[i], 70 if i == 0 else 0, 1.0, 1.0, 0) for i in range(5)], 70, hk)
    G1 = gtac(core(As, B), As, B, 'invade', 'develop')
    # star-scale specialties only matter if a star exists: also evaluate on a fixture where A has one lane +20 ahead in E/L
    GV5[sid] = (G1 - G0, eq(As, B)[0] - V0)
# star-scale sensitivity with a star lane present
AEs = [66, 86, 66, 64, 66]
Astar = side_of(AEs, AM, AL)
for sid in ('solo_king', 'late_king'):
    hk = hooks_of({sid})
    As = side_from([(AEs[i], AM[i], AL[i], 70 if i == 0 else 0, 1.0, 1.0, 0) for i in range(5)], 70, hk)
    GV5[sid + '@star'] = (gtac(core(As, B), As, B, 'invade', 'develop') - gtac(core(Astar, B), Astar, B, 'invade', 'develop'), None)

if __name__ == '__main__':
    print('### AC / formula-example re-verification')
    print('| check | result | got | expected |')
    print('|---|---|---|---|')
    for tag, ok, got, exp in OUT:
        g = fmt(got) if isinstance(got, (int, float, list, tuple)) and not isinstance(got, bool) else str(got)
        e = fmt(exp) if isinstance(exp, (int, float, list, tuple)) and not isinstance(exp, bool) else str(exp)
        print('| %s | %s | %s | %s |' % (tag, 'PASS' if ok else 'MISMATCH', g, e))
    print()
    print('### match-only BV (match.md generator, seed 1, 1000 pairs/group, maximin equilibrium)')
    print('BV1 b=9 :', {k: round(v, 3) for k, v in BV1.items()})
    print('BV1 b=12:', {k: round(v, 3) for k, v in BV1_12.items()})
    print('author cyc2 reference (b=9):', ref)
    print('BV2 own-tactic p swing: mean %.4f, max %.4f (budget-equivalent %.4f); b=0 mean %.4f' % (mean(SW), max(SW), budget_p, mean(SW0)))
    print('BV3 gap 3/5/10: eq p %s; same-tactic p %s' % ([round(BV3[g][0], 3) for g in (3, 5, 10)], [round(BV3[g][1], 3) for g in (3, 5, 10)]))
    print('BV5 stress 50->20 single-game p0 gain: mean %.4f max %.4f' % BV5)
    print('GV1 (21y pot65 mood60 stress30, 6 pts/yr one phase, noise 1): year-1 phase gain over all lane x phase x focus: min %.2f max %.2f mean %.2f' % (GV1['min'], GV1['max'], GV1['mean']))
    for k in ('jungleE_focus', 'topE_focus', 'adcL_focus', 'jungleE_nofocus', '12pts_jungleE', 'pot80_jungleE'):
        print('   %s: %s' % (k, [round(x, 2) for x in GV1[k]]))
    print('GV5 specialty dG on fixture (A invade vs B develop) and d(eq p):')
    for k, (g, v) in GV5.items():
        print('   %-14s dG %.3f  d_eq_p %s' % (k, g, ('%.4f' % v) if v is not None else '-'))
