#!/usr/bin/env python3
"""sim5 -- whole-system prototype of the REVISED v4 GDDs (2026-10-23).

match.md rev 3, growth.md rev 2, economy.md rev 2, talent.md rev 2, league.md rev 2, transfer.md rev 2,
plus the numbers.md v3.4 rules those docs keep (mood, stress, popularity, familiarity, business, events, calendar, prizes).
Scratchpad only: reads Assets/Data/event_config.json read-only; never writes the repo.
Readings of ambiguous text are tagged AMB-n (listed in the report).
"""
import math, random, hashlib, json, copy, collections, statistics, sys, time

REPO = '/Users/jjjingwen/claude-code-game-studios'
EVCFG = json.load(open(REPO + '/Assets/Data/event_config.json'))

P = dict(
    # ---- match.md rev 3
    lamE=0.15, lamM=0.5, lamL=0.15, chi=1.0, e0=0.3, e1=0.4, e2=0.3, eta=0.3, st0=4.0, star_cap=15.0,
    s_cyc=0.35, w_min=0.4, BE=1.0, BM=1.1, BL=1.0, a=0.1, F=1.0, C=0.5, g0=2.0, g1=6.0, ccmd=0.3, k=8.0, pf=0.15,
    m0=0.95, kappa_s=0.05,
    # ---- growth.md rev 2
    T_base=2.0, k_s=0.5, mt0=0.8, mt1=0.4, phi=0.6, mu=0.163, cap_margin=10.0, span=25.0, bonus=0.5, s_other=0.1,
    noise=0.15, unlock_v=80.0, lose_v=77.0, unlock_f=0.55, lose_f=0.30,
    # ---- economy.md rev 2
    S_min=20000.0, S_max=200000.0, h=0.4, s_spec=0.15, V_base=500000.0, v_s=0.25, kappa_wi=0.5, WI_min=0.8, WI_max=1.3,
    dead=0.1, sat_lo=-0.5, sat_hi=0.2, r_ratio=1.4, fire_sale=0.6, repay=0.5, debt_line=0.25,
    l_base=2800.0, l_pop=60.0, c0=6500.0, c1=100.0, z0=0.25, z1=0.5,
    # ---- talent.md rev 2
    p_talent=0.05, y0=50.0, y1=15.0, sig_pot=10.0, n_youth=5, n_free=3, pool_cap=35, scout_cost=80000.0,
    t0=0.05, t1=0.2, t_max=0.25,
    # ---- league.md rev 2
    rest_base=55.0, rest_aggr=15.0, rest_pre=15.0, cover_p=0.25, chase_v=74.0, chase_f=0.45, p_style_noise=0.2,
    p_switch=0.5, style_lock=3, style_w=0.5, star_w=1.5, age_pen=3.0, inject_segs=6.0, star_lead=8.0, lineup_hyst=3.0,
    # ---- transfer.md rev 2
    bench_renew=1.5, fee_lo=1.0, fee_hi=1.3, retain_mult=1.1, ai_accept_fee=1.2, n_lots_youth=3, n_lots_free=2,
    step_min=5000.0, step_pct=0.05, unsold_mult=0.8,
    # ---- numbers.md v3.4 (kept)
    AP=8, max_pp=2, s_train=4.0, s_matchst=2.0, s_loss=3.0, s_biz=4.0, s_rest=15.0, s_recover=4.0,
    m_result=8.0, m_play=4.0, m_salary=10.0, m_chem=5.0, m_stress=6.0, m_gamma=0.05,
    f_seg=5.0, f_win=2.0, f_decay=1.0, f_kappa=0.5,
    # ---- prototype switches (defaults = the documented rules; only ablations change them)
    cyc_on=True, tgt_a=6.0, tgt_b=6.0, ai_maint_stream=0, ai_renew_mode='floor', ai_fill_mode='floor',
    style_noreturn=False, label_star_lead=8.0, offer_idx=((1, 2), (3, 4), (5, 6)), cap_after=False, inject_on=True,
    ai_sp_fill=False, scout_need_mode='doc', style_noreturn_years=0, style_prefer_rare=False, stress_t_mode='linear',
    k_th=1.2, th_s=50.0, focus_redistribute=False, focus_counts=False, jk_E=2.5, lk_L=1.5, idle_sponsor=False,
    window_share=0.0, ai_renew_age_test=False,
    spend_reserve_W=0.0, reneg_autolist=False, ai_reneg_zero=False, cyc_nostar=False, open_sal=1.0, ai_fire_min=5,
    poach_use_floor=False, poach_honor=False, carry_on=True,
    final_prize_mult=1.0, bot_renew_zero=False, inject_weak=0.0, style_noreturn_star=False,
    fat_on=False, fat_stress=3.0, fat_biz=0.5, fat_ap=1.0, fat_split=0.5, sp_dur=4, biz_mult=1.0, R_ref=1.0,
    poach_champ_first=False, spec_table='base', specG_flat=None,
)

LANES = ('top', 'jungle', 'mid', 'adc', 'support')
LI = {l: i for i, l in enumerate(LANES)}
DIMS = {
    'top': ('damage_taken_per_death', 'damage_share', 'engage', 'teamfight_participation', 'kda', 'command'),
    'jungle': ('gold_per_min', 'gold_conversion', 'teamfight_participation', 'kda', 'kill_contribution', 'command'),
    'mid': ('damage_share', 'teamfight_participation', 'gold_conversion', 'kda', 'cc_duration', 'command'),
    'adc': ('farming', 'gold_to_damage', 'kda', 'teamfight_participation', 'survival', 'command'),
    'support': ('teamfight_participation', 'damage_taken_per_death', 'cc_duration', 'engage', 'healing', 'command'),
}
DIMW = {'top': (1, 1, 1.5, 1, 1, .5), 'jungle': (1, 1, 1.5, 1, 1, 1), 'mid': (1.5, 1, 1, 1, 1, .5),
        'adc': (1.5, 1.5, 1, 1, 1, .5), 'support': (1.5, 1, 1, 1, 1, 1)}
WSUM = {l: float(sum(DIMW[l])) for l in LANES}
PH = ('E', 'M', 'L')
PHASE_DIMS = {
    'top': {'E': ['kda'], 'M': ['damage_taken_per_death', 'engage', 'teamfight_participation'], 'L': ['damage_share']},
    'jungle': {'E': ['gold_per_min', 'kda', 'kill_contribution'], 'M': ['teamfight_participation'], 'L': ['gold_conversion']},
    'mid': {'E': ['kda'], 'M': ['teamfight_participation', 'cc_duration'], 'L': ['damage_share', 'gold_conversion']},
    'adc': {'E': ['kda'], 'M': ['teamfight_participation'], 'L': ['farming', 'gold_to_damage', 'survival']},
    'support': {'E': ['teamfight_participation'], 'M': ['damage_taken_per_death', 'cc_duration', 'engage'], 'L': ['healing']},
}
PIDX = {l: {k: tuple(DIMS[l].index(d) for d in PHASE_DIMS[l][k]) for k in PH} for l in LANES}
DPH = {l: tuple(next((k for k in PH if i in PIDX[l][k]), None) for i in range(6)) for l in LANES}
CMD = 5
LW = {'E': (1, 1.5, 1.2, 1, 1), 'M': (1.2, 1, 1, 1, 1.3), 'L': (1, 1, 1.2, 1.6, 1)}
LWS = {k: float(sum(v)) for k, v in LW.items()}
TACTICS = ('invade', 'teamfight', 'develop')
FOCUS = {'invade': 'E', 'teamfight': 'M', 'develop': 'L'}
TAC_OF_PHASE = {'E': 'invade', 'M': 'teamfight', 'L': 'develop'}
BEATS = {('invade', 'develop'), ('develop', 'teamfight'), ('teamfight', 'invade')}
CYC = (('E', 'L'), ('M', 'E'), ('L', 'M'))  # X beats Y
COUNTER_PHASE = {'E': 'M', 'M': 'L', 'L': 'E'}  # phase that beats phase X
STYLE_PHASE = {'invade': 'E', 'teamfight': 'M', 'late': 'L'}
STYLE_OF_PHASE = {'E': 'invade', 'M': 'teamfight', 'L': 'late'}
PRE = (2, 5, 11)  # 0-based segments followed by an event

# growth.md rev 2 specialty table: sid -> (lane->dim or '*', phase, hooks)
SPECS = {
    'jungle_king': ({'jungle': 'gold_per_min'}, 'E', dict(flat={'E': 2.5})),
    'solo_king': ({'top': 'kda'}, 'E', dict(flat={'E': 1.5}, ss={'E': 1.5})),
    'initiator': ({'top': 'engage', 'support': 'engage'}, 'M', dict(flat={'M': 2.5})),
    'iron_wall': ({'support': 'damage_taken_per_death'}, 'M', dict(flat={'M': 1.5}, sg=0.9)),
    'tempo_mid': ({'mid': 'teamfight_participation'}, 'M', dict(flat={'M': 1.5}, exa=0.1)),
    'late_king': ({'adc': 'farming'}, 'L', dict(flat={'L': 1.5}, ss={'L': 1.5})),
    'nanny': ({'support': 'healing'}, 'L', dict(flat={'L': 2.0}, sg=0.9)),
    'brain': ('*', None, dict(flat={'M': 1.0}, exa=0.15, sg=0.85)),
}
SPECS_BASE = copy.deepcopy(SPECS)
SPECS_G = {
    'solo_king': ({'top': 'kda'}, 'E', dict(flat={'E': 1.5}, ss={'E': 1.5})),
    'top_engage': ({'top': 'engage'}, 'M', dict(flat={'M': 2.5})),
    'top_late': ({'top': 'damage_share'}, 'L', dict(flat={'L': 2.0})),
    'jungle_king': ({'jungle': 'gold_per_min'}, 'E', dict(flat={'E': 1.8})),
    'jungle_support': ({'jungle': 'teamfight_participation'}, 'M', dict(flat={'M': 2.0}, exa=0.05)),
    'jungle_ops': ({'jungle': 'gold_conversion'}, 'L', dict(flat={'L': 2.0})),
    'mid_lane': ({'mid': 'kda'}, 'E', dict(flat={'E': 2.0})),
    'tempo_mid': ({'mid': 'teamfight_participation'}, 'M', dict(flat={'M': 1.5}, exa=0.1)),
    'mid_mage': ({'mid': 'damage_share'}, 'L', dict(flat={'L': 1.5}, ss={'L': 1.3})),
    'adc_lane': ({'adc': 'kda'}, 'E', dict(flat={'E': 2.0})),
    'adc_reaper': ({'adc': 'teamfight_participation'}, 'M', dict(flat={'M': 2.0})),
    'late_king': ({'adc': 'farming'}, 'L', dict(flat={'L': 2.5})),
    'sup_roam': ({'support': 'teamfight_participation'}, 'E', dict(flat={'E': 2.0})),
    'iron_wall': ({'support': 'damage_taken_per_death'}, 'M', dict(flat={'M': 1.5}, sg=0.9)),
    'nanny': ({'support': 'healing'}, 'L', dict(flat={'L': 2.0}, sg=0.9)),
    'brain': ('*', None, dict(flat={'M': 1.0}, exa=0.15, sg=0.85)),
}
LANE_SPECS = {l: [] for l in LANES}
for _sid, (_lanes, _ph, _hk) in SPECS.items():
    for _l in LANES:
        if _lanes == '*':
            LANE_SPECS[_l].append((_sid, CMD, None))
        elif _l in _lanes:
            LANE_SPECS[_l].append((_sid, DIMS[_l].index(_lanes[_l]), _ph))
SPEC_DIM = {}
for _l in LANES:
    for _sid, _di, _ph in LANE_SPECS[_l]:
        if _ph:
            SPEC_DIM.setdefault((_l, _ph), _di)

TRAIT_GROUPS = (('loyal', 'selfish'), ('reserved', 'lively'), ('earnest', 'slick'), ('diligent', 'lazy'),
                ('assertive', 'gentle'), ('optimistic', 'pessimistic'), ('resolute', 'fragile'),
                ('idealistic', 'pragmatic'), ('decisive', 'hesitant'), ('clever', 'honest'))
TE = collections.defaultdict(dict)
for t, k, m in [('loyal', 'renewal_ask_price', .95), ('selfish', 'ask_price', 1.05), ('selfish', 'bench_mood_drop', 1.03),
                ('reserved', 'stream_fans', .95), ('reserved', 'stress_gain', .97), ('lively', 'stream_fans', 1.05),
                ('lively', 'business_rate', 1.03), ('earnest', 'training_gain', 1.03), ('earnest', 'business_rate', .97),
                ('slick', 'business_amount', 1.03), ('slick', 'negative_compat_impact', .95), ('diligent', 'training_gain', 1.05),
                ('lazy', 'training_gain', .95), ('lazy', 'stress_gain', .97), ('assertive', 'command', 1.05),
                ('gentle', 'familiarity_gain', 1.05), ('optimistic', 'negative_mood_change', .95),
                ('pessimistic', 'negative_mood_change', 1.05), ('resolute', 'stress_variance_impact', .95),
                ('fragile', 'loss_mood_drop', 1.05), ('idealistic', 'win_mood_gain', 1.03),
                ('idealistic', 'salary_satisfaction_weight', .97), ('pragmatic', 'salary_satisfaction_weight', 1.05),
                ('decisive', 'decider_performance', 1.03), ('hesitant', 'decider_performance', .97),
                ('clever', 'training_gain', 1.03), ('clever', 'off_lane_discount', 1.03), ('honest', 'ask_price', .97),
                ('honest', 'mood_change_magnitude', .97)]:
    TE[t][k] = m
TKEYS = sorted({k for d in TE.values() for k in d})
HP = {}
for a_, b_, v_ in [('assertive', 'gentle', .5), ('assertive', 'fragile', .5), ('loyal', 'loyal', .5), ('optimistic', 'pessimistic', .3),
                   ('diligent', 'diligent', .3), ('lively', 'lively', .3), ('reserved', 'lively', .3), ('clever', 'honest', .3),
                   ('assertive', 'assertive', -.5), ('selfish', 'loyal', -.5), ('selfish', 'selfish', -.5), ('diligent', 'lazy', -.5),
                   ('idealistic', 'pragmatic', -.3), ('decisive', 'hesitant', -.3), ('earnest', 'slick', -.3),
                   ('pessimistic', 'pessimistic', -.3)]:
    HP[(a_, b_)] = v_
    HP[(b_, a_)] = v_
EV_TRAIT = {m['trait']: m['value'] for m in EVCFG['trait_modifiers']}
GC = EVCFG['good_chance']
EVENTS = EVCFG['events']
ACTION_EV = collections.defaultdict(list)
for e in EVENTS:
    if e['kind'] == 'action':
        ACTION_EV[(e['action'], e['tone'])].append(e)
STATE_EV = [e for e in EVENTS if e['kind'] == 'state']
CHOICE_EV = [e for e in EVENTS if e['kind'] == 'choice']
CHOICE_KIND = {'choice_home_leave': 'leave', 'choice_extra_stream': 'money', 'choice_variety_show': 'money',
               'choice_equipment_upgrade': 'spend', 'choice_teammate_conflict': 'conflict'}
SPLIT_PRIZE = [80000, 50000, 30000, 30000, 10000, 10000, 10000, 10000]
FINAL_PRIZE = [200000, 120000, 60000, 60000, 0, 0, 0, 0]
FINAL_PTS = [10, 7, 5, 5, 3, 2, 1, 0]
HONOR_SPLIT = [70, 50, 30, 30, 0, 0, 0, 0]
HONOR_FINAL = [100, 80, 60, 60, 0, 0, 0, 0]


def clamp(x, lo, hi):
    return lo if x < lo else (hi if x > hi else x)


def mean(xs):
    xs = list(xs)
    return sum(xs) / len(xs) if xs else 0.0


def rng_for(seed, *keys):
    """keyed RNG (league.md 随机数): one independent stream per (purpose, keys)."""
    h = hashlib.blake2b(repr((seed,) + keys).encode(), digest_size=8).digest()
    return random.Random(int.from_bytes(h, 'little'))


def round100(x):
    return float(int(x / 100.0 + 0.5) * 100)


def age_f(age):
    return 1.2 if age <= 19 else 1.0 if age <= 22 else 0.8 if age <= 25 else 0.5 if age <= 28 else 0.3


def decline(age):
    return 0 if age < 27 else 1 if age <= 28 else 2 if age <= 30 else 4


# ============================================================ players
class Player:
    __slots__ = ('id', 'age', 'lane', 'radar', 'pot', 'bias', 'obs', 'traits', 'tm', 'looks', 'pop', 'mood', 'stress',
                 'salary', 'contract', 'focus', 'specs', 'spec_src', 'honor', 'honor_year', 'wants', 'reneg_block',
                 'retained', 'joined', 'youth_year', 'carry', 'lock_year', 'origin', 'snap', 'autolist')


_TM_CACHE = {}


def trait_mults(traits):
    tm = _TM_CACHE.get(traits)
    if tm is None:
        tm = {k: TE[traits[0]].get(k, 1.0) * TE[traits[1]].get(k, 1.0) for k in TKEYS}
        _TM_CACHE[traits] = tm
    return tm


def r_main(p, lane=None):
    l = lane or p.lane
    v = p.radar[l]
    w = DIMW[l]
    s = (v[0] * w[0] + v[1] * w[1] + v[2] * w[2] + v[3] * w[3] + v[4] * w[4] + v[5] * w[5]) / WSUM[l]
    if l != p.lane:
        s *= 0.8 * p.tm['off_lane_discount']
    return s


def praw(p, k, lane=None):
    l = lane or p.lane
    v = p.radar[l]
    ix = PIDX[l][k]
    return sum(v[i] for i in ix) / len(ix)


def pot_est(p):
    return p.pot + p.bias * (1 - p.obs / 100.0)


def cap_of(p):
    return min(100.0, p.pot + P['cap_margin'])


def check_specs(p, src='train'):
    """growth.md F4 (main lane only)."""
    l = p.lane
    v = p.radar[l]
    for sid, di, ph in LANE_SPECS[l]:
        d = v[di]
        if sid in p.specs:
            if d < P['lose_v'] or (ph is not None and p.focus[ph] < P['lose_f']):
                p.specs.discard(sid)
                p.spec_src.pop(sid, None)
        elif d >= P['unlock_v'] and (ph is None or p.focus[ph] >= P['unlock_f']):
            p.specs.add(sid)
            p.spec_src[sid] = src


def init_focus(p):
    best = max(PH, key=lambda k: praw(p, k))  # ties E > M > L (iteration order)
    p.focus = {k: (0.4 if k == best else 0.2) for k in PH}


def gen_player(lg, rng, lane, age_lo, age_hi, mu_pot, talent_p=None, origin='gen'):
    """talent.md rev 2 generation."""
    p = Player()
    lg.next_pid += 1
    p.id = lg.next_pid
    tp = P['p_talent'] if talent_p is None else talent_p
    if rng.random() < tp:
        p.pot = rng.uniform(85, 95)
    else:
        p.pot = clamp(rng.gauss(mu_pot, P['sig_pot']), 40, 84)
    p.radar = {}
    for l in LANES:
        lo, hi = (0.6, 0.95) if l == lane else (0.35, 0.75)
        p.radar[l] = [p.pot * rng.uniform(lo, hi) for _ in range(6)]
    p.lane = lane
    p.age = rng.randint(age_lo, age_hi)
    g1, g2 = rng.sample(range(10), 2)
    p.traits = (TRAIT_GROUPS[g1][rng.randrange(2)], TRAIT_GROUPS[g2][rng.randrange(2)])
    p.tm = trait_mults(p.traits)
    p.looks = rng.uniform(30, 90)
    p.pop = rng.uniform(5, 60)
    p.mood = rng.uniform(50, 80)
    p.stress = rng.uniform(10, 40)
    p.contract = rng.randint(1, 3)
    p.bias = rng.uniform(-10, 10)
    p.obs = 0.0
    p.salary = 0
    init_focus(p)
    p.specs = set()
    p.spec_src = {}
    check_specs(p, 'gen')
    p.honor = 0.0
    p.honor_year = 0.0
    p.wants = False
    p.reneg_block = -1
    p.retained = False
    p.joined = -9
    p.youth_year = None
    p.carry = False
    p.lock_year = -9
    p.origin = origin
    p.snap = None
    p.autolist = -9
    return p


# ============================================================ economy formulas (economy.md rev 2)
def market_salary(p, wi, renewal=False):
    ws = (r_main(p) + 0.8 * p.pop + 0.3 * pot_est(p)) / 2.1
    base = P['S_min'] + (P['S_max'] - P['S_min']) * ws / 100.0
    bonus = 1 + P['h'] * p.honor / 100.0 + P['s_spec'] * len(p.specs)
    ask = p.tm['ask_price'] * (p.tm['renewal_ask_price'] if renewal else 1.0)
    return base * bonus * ask * wi


def year_factor(age, years):
    if age <= 23:
        return (1.0, 1.06, 1.12)[years - 1]
    if age <= 27:
        return 1.0
    return (1.0, 0.96, 0.92)[years - 1]


def sign_salary(lg, p, years, kind, renewal=False):
    r = rng_for(lg.seed, 'sal', p.id, lg.year, kind).uniform(0.9, 1.1)
    return float(round(market_salary(p, lg.WI, renewal) * year_factor(p.age, years) * r))


def value_of(p):
    youth = clamp((32 - p.age) * 8, 0, 100)
    ws = (r_main(p) + pot_est(p) + 0.6 * youth) / 2.6
    return P['V_base'] * ws / 100.0 * (1 + P['v_s'] * len(p.specs))


def satisfaction(p, wi):
    m = market_salary(p, wi)
    if m <= 0:
        return 0.0
    x = (p.salary - m) / m
    dz = math.copysign(max(0.0, abs(x) - P['dead']), x)
    return clamp(dz, P['sat_lo'], P['sat_hi'])


def wage_total(c):
    return sum(p.salary for p in c.roster)


def wseg(c):
    return wage_total(c) / 12.0


def floor_of(c, bill=None):
    W = (wage_total(c) if bill is None else bill) / 12.0
    return (3 + 3 * c.frugal + c.off_cash) * W


def target_of(c, bill=None):
    W = (wage_total(c) if bill is None else bill) / 12.0
    return floor_of(c, bill) + (P['tgt_a'] + P['tgt_b'] * c.frugal) * W


def biz_prob(p):
    ws = (1.5 * p.pop + 0.5 * r_main(p) + 1.0 * p.looks) / 3.0
    return clamp(ws / 100.0 * 0.4 * p.tm['business_rate'], 0, 0.4)


def biz_amount(p, r):
    return round((5000 + 100 * p.pop + 80 * p.looks) * p.tm['business_amount'] * r * P['biz_mult'])


def fat_strength(c, g):
    return max([s_ for a_, b_, s_ in c.fat if a_ <= g <= b_] or [0.0])


def oblig(c, b):
    n = getattr(c, '_oblig', 0)
    if not n:
        return
    for p in sorted(starters(c), key=lambda p: -p.pop):
        while n > 0 and b.can(p):
            b.add('stream', p)
            n -= 1
        if n <= 0:
            break


# ============================================================ clubs / league
class Club:
    def __init__(self, cid):
        self.id = cid
        self.ctrl = 'ai'
        self.bot = 'ai'
        self.is_player = False
        self.style = 'invade'
        self.star_lane = None
        self.star_pid = None
        self.star_phase = None
        self.aggr = self.frugal = self.adapt = 0.5
        self.off_rest = self.off_cash = self.off_renew = self.off_bid = 0.0
        self.funds = 0.0
        self.pop = 50.0
        self.roster = []
        self.lineup = {}
        self.fam = {}
        self.contracts = []
        self.recent = []
        self.rank_score = 50.0
        self.tac_hist = []
        self.last_tac = 'invade'
        self.debt = 0.0
        self.loan_taken = 0.0
        self.bankrupt = None
        self.events_since_switch = 99
        self.style_hist = []
        self.need_lanes = set()
        self.year_events = []
        self.ys = None
        self.xs = collections.defaultdict(float)
        self.fund_trace = []
        self.fat = []


class League:
    def __init__(self, seed):
        self.seed = seed
        self.clubs = []
        self.free = []
        self.next_pid = 0
        self.year = 0
        self.seg = 0
        self.WI = 1.0
        self.log = collections.defaultdict(lambda: collections.defaultdict(float))
        self.ename = ''
        self.samples = None
        self.champs = []
        self._sc = None


def new_ys():
    return collections.defaultdict(float)


def starters(c):
    return [c.lineup[l] for l in LANES if l in c.lineup]


def lineup_tuple(c):
    return tuple((c.lineup[l], l) for l in LANES)


def pair_key(a, b):
    return (a.id, b.id) if a.id < b.id else (b.id, a.id)


_HC = {}


def compat(a, b):
    key = (a.traits, b.traits)
    h = _HC.get(key)
    if h is None:
        na, nb = a.tm['negative_compat_impact'], b.tm['negative_compat_impact']
        nf = nb if abs(nb - 1) > abs(na - 1) else na
        s = 0.0
        for x in a.traits:
            for y in b.traits:
                v = HP.get((x, y), 0.0)
                s += v * nf if v < 0 else v
        h = clamp(s, -1.0, 1.0)
        _HC[key] = h
    return h


def fam_mult(a, b):
    fa, fb = a.tm['familiarity_gain'], b.tm['familiarity_gain']
    return fb if abs(fb - 1) > abs(fa - 1) else fa


def chemistry(c, lu=None):
    lu = lu or lineup_tuple(c)
    ps = [x[0] for x in lu]
    tot = 0.0
    for i in range(5):
        for j in range(i + 1, 5):
            a, b = ps[i], ps[j]
            f = c.fam.get(pair_key(a, b), 0.0)
            tot += clamp(f * (1 + P['f_kappa'] * compat(a, b)), 0, 100)
    return tot / 10.0


def winrate(c):
    if not c.recent:
        return 50.0
    return 100.0 * sum(c.recent) / len(c.recent)


def eff_rating(p):
    res = p.tm['stress_variance_impact']
    f = (P['m0'] + (1 - P['m0']) * p.mood / 100.0) * (1 - P['kappa_s'] * p.stress / 100.0 * res)
    return f * (praw(p, 'E') + praw(p, 'M') + praw(p, 'L')) / 3.0


def pick_lineup(lg, c):
    """league.md AI rule 1 (used by every club, AMB-1): best effective main-lane player per lane, keep the current
    starter unless beaten by > 3; empty lanes -> best off-lane bench player."""
    while len(c.roster) < 5:
        emergency_sign(lg, c)
    new = {}
    used = set()
    for l in LANES:
        cands = [p for p in c.roster if p.lane == l]
        if not cands:
            continue
        best = max(cands, key=eff_rating)
        cur = c.lineup.get(l)
        if cur is not None and cur is not best and any(q is cur for q in cands) and \
                eff_rating(best) - eff_rating(cur) <= P['lineup_hyst']:
            best = cur
        new[l] = best
        used.add(best.id)
    c.need_lanes = set()
    for l in LANES:
        if l not in new:
            cands = [p for p in c.roster if p.id not in used]
            b = max(cands, key=lambda p: r_main(p, l))
            new[l] = b
            used.add(b.id)
            c.need_lanes.add(l)
    c.lineup = new


def emergency_sign(lg, c):
    have = {p.lane for p in c.roster}
    if not lg.free:
        mu = P['y0'] + P['y1'] * mean(x.pop for x in lg.clubs) / 100.0
        r = rng_for(lg.seed, 'emg', lg.year, lg.next_pid)
        lg.free.append(gen_player(lg, r, r.choice(LANES), 23, 23, mu, origin='emg'))
    cands = [p for p in lg.free if p.lane not in have] or lg.free
    p = max(cands, key=r_main)
    lg.free.remove(p)
    sign(lg, c, p, 2, 'emg')
    lg.log[lg.year]['emergency'] += 1


def sign(lg, c, p, years, kind, salary=None):
    p.salary = salary if salary is not None else sign_salary(lg, p, years, kind)
    p.contract = years
    p.obs = 0.0
    p.retained = False
    p.wants = False
    p.joined = lg.year + 0.5
    c.roster.append(p)


def remove_from(c, p):
    c.roster.remove(p)
    for l in list(c.lineup):
        if c.lineup[l] is p:
            del c.lineup[l]


def release(lg, c, p):
    remove_from(c, p)
    p.contract = 0
    p.honor = 0.0  # economy F2: free agents have honor 0 (AMB-2)
    p.wants = False
    lg.free.append(p)


def prune_fam(c):
    ids = {p.id for p in c.roster}
    c.fam = {k: v for k, v in c.fam.items() if k[0] in ids and k[1] in ids}


def weakest_lane(c):
    """lane with the lowest starter main-lane rating; a lane without a main-lane player counts as weakest."""
    def key(l):
        p = c.lineup.get(l)
        if p is None or p.lane != l:
            return -1.0
        return r_main(p)
    return min(LANES, key=key)


def need_of(c, lane):
    n = sum(1 for p in c.roster if p.lane == lane)
    if n == 0 or lane == weakest_lane(c):
        return 1.0
    if n == 1:
        return 0.5
    return 0.0


def sole_main(c, p):
    return sum(1 for q in c.roster if q.lane == p.lane) <= 1


# ============================================================ match model (match.md rev 3)
class Side:
    __slots__ = ('S', 'r', 'ex', 'sig', 'ss', 'cmd')


_HK = {}


def hooks_of(specs):
    key = frozenset(specs)
    hk = _HK.get(key)
    if hk is None:
        flat = {'E': 0.0, 'M': 0.0, 'L': 0.0}
        ss = {'E': 1.0, 'L': 1.0}
        exa = 0.0
        sg = 1.0
        for sid in key:
            h = SPECS[sid][2]
            for k, v in h.get('flat', {}).items():
                flat[k] += v
            for k, v in h.get('ss', {}).items():
                ss[k] *= v
            exa += h.get('exa', 0.0)
            sg *= h.get('sg', 1.0)
        flat = {k: clamp(v, -5, 5) for k, v in flat.items()}
        ss = {k: clamp(v, 0.5, 2.0) for k, v in ss.items()}
        hk = (flat, ss, exa, clamp(sg, 0.7, 1.3))
        _HK[key] = hk
    return hk


NEUTRAL_HK = ({'E': 0.0, 'M': 0.0, 'L': 0.0}, {'E': 1.0, 'L': 1.0}, 0.0, 1.0)


def lineup_specs(lu):
    s = set()
    for p, l in lu:
        if l == p.lane:
            s |= p.specs
    return s


def parts_of(p, lane):
    res = p.tm['stress_variance_impact']
    f = (P['m0'] + (1 - P['m0']) * p.mood / 100.0) * (1 - P['kappa_s'] * p.stress / 100.0 * res)
    if lane != p.lane:
        f *= 0.8 * p.tm['off_lane_discount']
    v = p.radar[lane]
    ix = PIDX[lane]
    return (f * sum(v[j] for j in ix['E']) / len(ix['E']), f * sum(v[j] for j in ix['M']) / len(ix['M']),
            f * sum(v[j] for j in ix['L']) / len(ix['L']), f * v[CMD] * p.tm['command'], res,
            p.tm['decider_performance'], p.stress)


def side_from(parts, chem, hk, dec=False):
    flat, ss, exa, sg = hk
    dm = sum(x[5] for x in parts) / 5.0 if dec else 1.0
    lam = {'E': P['lamE'], 'M': P['lamM'], 'L': P['lamL']}
    sd = Side()
    sd.r = {}
    sd.S = {}
    for kk, idx in (('E', 0), ('M', 1), ('L', 2)):
        r = [x[idx] * dm for x in parts]
        w = LW[kk]
        ws = (w[0] * r[0] + w[1] * r[1] + w[2] * r[2] + w[3] * r[3] + w[4] * r[4]) / LWS[kk]
        s = ws - lam[kk] * (ws - min(r))
        if kk == 'M':
            s += P['chi'] * (chem - 50.0) / 10.0
        sd.S[kk] = s + flat[kk]
        sd.r[kk] = r
    cmd = min(100.0, max(x[3] for x in parts))
    sd.cmd = cmd
    sd.ex = clamp(P['e0'] + P['e1'] * chem / 100.0 + P['e2'] * cmd / 100.0 + exa, 0.3, 1.0)
    avs = sum(x[6] for x in parts) / 5.0
    avr = sum(x[4] for x in parts) / 5.0
    sd.sig = (P['g0'] + P['g1'] * avs / 100.0 * avr) * (1 - P['ccmd'] * cmd / 100.0) * sg
    sd.ss = ss
    return sd


def star_diff(A, B, k):
    ra, rb = A.r[k], B.r[k]
    lead = [ra[i] - rb[i] for i in range(5)]
    m = sum(lead) / 5.0
    sa = min(P['star_cap'], max(0.0, max(lead) - max(0.0, m) - P['st0']) * A.ss[k])
    sb = min(P['star_cap'], max(0.0, -min(lead) - max(0.0, -m) - P['st0']) * B.ss[k])
    return sa - sb


def core(A, B):
    d = {k: A.S[k] - B.S[k] for k in PH}
    d['E'] += P['eta'] * star_diff(A, B, 'E')
    d['L'] += P['eta'] * star_diff(A, B, 'L')
    f = {'E': 1.0, 'M': 1.0, 'L': 1.0}
    if P['cyc_on'] and P['s_cyc'] > 0:
        for X, Y in CYC:
            dX = (A.S[X] - B.S[X]) if P['cyc_nostar'] else d[X]
            if dX * d[Y] < 0:
                f[Y] = max(P['w_min'], 1 - P['s_cyc'] * abs(dX) / 10.0)
    bs = P['BE'] + P['BM'] + P['BL']
    base = (P['BE'] * f['E'] * d['E'] + P['BM'] * f['M'] * d['M'] + P['BL'] * f['L'] * d['L']) / bs
    return d, f, base


def gtac(cr, A, B, tA, tB):
    d, f, base = cr
    fa = clamp(P['a'] * A.ex * d[FOCUS[tA]], -P['F'], P['F'])
    fb = clamp(P['a'] * B.ex * (-d[FOCUS[tB]]), -P['F'], P['F'])
    if (tA, tB) in BEATS:
        cn = P['C'] * A.ex
    elif (tB, tA) in BEATS:
        cn = -P['C'] * B.ex
    else:
        cn = 0.0
    return base + fa - fb + cn


def pwin(G):
    x = -G / P['k']
    if x > 700:
        v = 0.0
    elif x < -700:
        v = 1.0
    else:
        v = 1.0 / (1.0 + math.exp(x))
    return clamp(v, P['pf'], 1 - P['pf'])


def series_prob(p, bo):
    need = bo // 2 + 1
    q = 1 - p
    return sum(math.comb(need - 1 + k, k) * p ** need * q ** k for k in range(need))


def eq_value(A, B):
    cr = core(A, B)
    M = [[pwin(gtac(cr, A, B, x, y)) for y in TACTICS] for x in TACTICS]
    va = max(min(r) for r in M)
    vb = min(max(M[i][j] for i in range(3)) for j in range(3))
    return (va + vb) / 2.0, M


def play_series(A, Ad, B, Bd, tA, tB, bo, rng):
    gn = gtac(core(A, B), A, B, tA, tB)
    gd = gtac(core(Ad, Bd), Ad, Bd, tA, tB)
    need = bo // 2 + 1
    wa = wb = 0
    while wa < need and wb < need:
        dec = (wa == need - 1 and wb == need - 1)
        sA, sB, g = (Ad, Bd, gd) if dec else (A, B, gn)
        u1, u2, u3 = rng.random(), rng.random(), rng.random()
        G = g + sA.sig * (2 * u1 - 1) - sB.sig * (2 * u2 - 1)
        if u3 < pwin(G):
            wa += 1
        else:
            wb += 1
    return wa, wb


def club_sides(c):
    lu = lineup_tuple(c)
    ch = chemistry(c, lu)
    parts = [parts_of(p, l) for p, l in lu]
    hk = hooks_of(lineup_specs(lu))
    return side_from(parts, ch, hk), side_from(parts, ch, hk, dec=True), lu, ch


def radar5(S):
    return {k: 5.0 * round(S[k] / 5.0) for k in PH}


def seg_sides(lg):
    key = (lg.year, lg.seg)
    if lg._sc is None or lg._sc[0] != key:
        lg._sc = (key, {c.id: club_sides(c)[0] for c in lg.clubs})
    return lg._sc[1]


def club_power(c):
    sd = club_sides(c)[0]
    return mean(sd.S.values()), sd


# ============================================================ growth (growth.md rev 2)
def point_deltas(p, tgt, fdim, noise, mult=1.0, add=0.0):
    """deltas of one training point on p's main-lane dims, from the current (segment-start) values."""
    l = p.lane
    v = p.radar[l]
    cap = cap_of(p)
    spec = 1.0 if tgt == 'C' else 1 + P['phi'] * p.focus[tgt]
    mood_t = P['mt0'] + P['mt1'] * p.mood / 100.0
    if P['stress_t_mode'] == 'thresh':
        stress_t = 1 - P['k_th'] * max(0.0, p.stress - P['th_s']) / 100.0
    else:
        stress_t = 1 - P['k_s'] * p.stress / 100.0
    b = P['T_base'] * age_f(p.age) * p.tm['training_gain'] * mood_t * stress_t * spec * noise
    b = b * mult + add
    out = [0.0] * 6
    dph = DPH[l]
    redis = P['focus_redistribute'] and tgt != 'C' and fdim is not None
    nk = len(PIDX[l][tgt]) if tgt != 'C' else 1
    for d in range(6):
        hd = (cap - v[d]) / P['span']
        hd = 0.0 if hd < 0 else (1.0 if hd > 1 else hd)
        if tgt == 'C':
            w = 1.0 if d == CMD else P['s_other']
        elif d == fdim:
            w = (1 + P['bonus'] * (nk - 1) / nk) if redis else 1 + P['bonus']
        elif dph[d] == tgt:
            w = (1 - P['bonus'] / nk) if redis else 1.0
        else:
            w = P['s_other']
        out[d] = b * hd * w
    return out


def apply_deltas(p, acc):
    v = p.radar[p.lane]
    cap = cap_of(p)
    for d in range(6):
        if acc[d] > 0 and v[d] < cap:
            v[d] = min(cap, v[d] + acc[d])


def lowest_dim(p, k):
    l = p.lane
    return min(PIDX[l][k], key=lambda i: p.radar[l][i])


def near_cap(p, di):
    return cap_of(p) - p.radar[p.lane][di] < 5.0  # UI "接近上限" marker (AMB-3)


def spec_or_lowest(p, k):
    sd = SPEC_DIM.get((p.lane, k))
    if sd is not None and not near_cap(p, sd):
        return sd
    return lowest_dim(p, k)


def weakest_phase(p):
    return min(PH, key=lambda k: praw(p, k))


def phase_head(p, k):
    cap = cap_of(p)
    v = p.radar[p.lane]
    return mean(clamp((cap - v[i]) / P['span'], 0, 1) for i in PIDX[p.lane][k])


# ============================================================ events (event_config.json)
def good_chance(mood, stress, tmod):
    return clamp(GC['base'] + GC['mood_weight'] * (mood - GC['neutral']) - GC['stress_weight'] * (stress - GC['neutral']) + tmod,
                 GC['min'], GC['max'])


def ev_trait_mod(p):
    return sum(EV_TRAIT.get(t, 0.0) for t in p.traits)


def pick_weighted(cands, rng):
    tot = sum(e['weight'] for e in cands)
    r = rng.uniform(0, tot)
    acc = 0.0
    for i, e in enumerate(cands):
        acc += e['weight']
        if r < acc:
            return i
    return len(cands) - 1


def roll_eff(f, rng):
    v = rng.uniform(f['min'], f['max']) if f['min'] < f['max'] else f['min']
    if f['type'] == 'funds':
        v = round(v)
    return (f['type'], f['target'], v)


def holds(cond, c, a, b):
    t = cond['type']
    v = cond['value']
    if t == 'stress_above':
        return a is not None and a.stress > v
    if t == 'stress_below':
        return a is not None and a.stress < v
    if t == 'mood_above':
        return a is not None and a.mood > v
    if t == 'mood_below':
        return a is not None and a.mood < v
    if t == 'trait_has':
        return a is not None and cond['trait'] in a.traits
    if t == 'pair_bad_compat':
        return a is not None and b is not None and compat(a, b) <= v
    if t == 'recent_win_rate_above':
        return len(c.recent) > 0 and winrate(c) > v
    if t == 'recent_loss_streak':
        n = int(v)
        return len(c.recent) >= n and not any(c.recent[-n:])
    if t == 'chemistry_below':
        return chemistry(c) < v
    if t == 'club_popularity_at_least':
        return c.pop >= v
    return False


def subjects(e, c):
    s = e['subject']
    st = starters(c)
    if s == 'team':
        return [(None, None)] if all(holds(x, c, None, None) for x in e['conditions']) else []
    if s == 'player':
        pool = [(p, None) for p in c.roster]
    elif s == 'starter':
        pool = [(p, None) for p in st]
    else:
        pool = [(st[i], st[j]) for i in range(5) for j in range(i + 1, 5)]
    return [(a, b) for a, b in pool if all(holds(x, c, a, b) for x in e['conditions'])]


def eff_players(tgt, c, a, b):
    if tgt == 'subject':
        return [p for p in (a, b) if p is not None and p in c.roster]
    if tgt == 'roster':
        return list(c.roster)
    if tgt == 'starters':
        return starters(c)
    return []


def apply_personal(effs, c, a, b):
    for typ, tgt, v in effs:
        if typ not in ('mood', 'stress', 'potential'):
            continue
        for p in eff_players(tgt, c, a, b):
            if typ == 'mood':
                p.mood = clamp(p.mood + (v * p.tm['negative_mood_change'] if v < 0 else v), 0, 100)
            elif typ == 'stress':
                p.stress = clamp(p.stress + (v * p.tm['stress_gain'] if v > 0 else v), 0, 100)
            else:
                p.pot = clamp(p.pot + v, 0, 100)


def apply_after(effs, c, a, b):
    for typ, tgt, v in effs:
        if typ == 'player_popularity':
            for p in eff_players(tgt, c, a, b):
                p.pop = clamp(p.pop + v, 0, 100)
        elif typ == 'club_popularity':
            c.pop = clamp(c.pop + v, 0, 100)
        elif typ == 'familiarity':
            pairs = []
            if tgt == 'subject' and a is not None and b is not None:
                pairs = [(a, b)]
            elif tgt == 'starters':
                st = starters(c)
                pairs = [(st[i], st[j]) for i in range(5) for j in range(i + 1, 5)]
            for x, y in pairs:
                if x in c.roster and y in c.roster:
                    k = pair_key(x, y)
                    nv = clamp(c.fam.get(k, 0.0) + v, 0, 100)
                    if nv <= 0:
                        c.fam.pop(k, None)
                    else:
                        c.fam[k] = nv


def funds_of(effs):
    return sum(v for typ, tgt, v in effs if typ == 'funds')


# ============================================================ planning
class Plan:
    __slots__ = ('asg', 'acc', 'sp', 'cap', 'pts', 'blocked')

    def __init__(self, blocked):
        self.asg = []
        self.acc = []
        self.sp = 0
        self.cap = collections.Counter()
        self.pts = P['AP']
        self.blocked = blocked

    def can(self, p):
        return self.pts > 0 and self.cap[p.id] < P['max_pp'] and p.id not in self.blocked

    def add(self, act, p, tgt=None, fd=None):
        self.asg.append((act, p, tgt, fd))
        self.cap[p.id] += 1
        self.pts -= 1

    def accept(self, o):
        self.acc.append(o)
        self.cap[o[0].id] += 1
        self.pts -= 1

    def sponsor(self, n):
        n = max(0, min(n, self.pts))
        self.sp += n
        self.pts -= n


def rest_th(c, pre):
    th = P['rest_base'] + P['rest_aggr'] * c.aggr + c.off_rest
    if pre:
        th -= P['rest_pre'] * (1 - c.aggr)
    return th


def team_weakest_phase(lg, c):
    S = seg_sides(lg)[c.id].S
    return min(PH, key=lambda k: S[k])


def ai_train_target(lg, c, p, rd):
    best = None
    for sid, di, ph in LANE_SPECS[p.lane]:
        if ph is None or sid in p.specs:
            continue
        d = p.radar[p.lane][di]
        if d >= P['chase_v'] and p.focus[ph] >= P['chase_f']:
            if best is None or d > best[0]:
                best = (d, ph, di)
    if best is not None:
        lg.log[lg.year]['chase'] += 1
        if cap_of(p) < P['unlock_v']:
            lg.log[lg.year]['chase_futile'] += 1
        return best[1], best[2]
    if rd.random() < P['cover_p']:
        return weakest_phase(p), None
    if c.style == 'star':
        if p.lane == c.star_lane:
            return c.star_phase or 'E', None
        return team_weakest_phase(lg, c), None
    return STYLE_PHASE[c.style], None


def plan_ai(lg, c, seg, offers, rd, blocked):
    """league.md rev 2, AI per-segment decisions 1-7."""
    b = Plan(blocked)
    oblig(c, b)
    st = starters(c)
    th = rest_th(c, seg in PRE)
    resting = set()
    for p in sorted(st, key=lambda p: -p.stress):
        n = 2 if p.stress > th + 20 else (1 if p.stress > th else 0)
        for _ in range(n):
            if b.can(p):
                b.add('rest', p)
                resting.add(p.id)
    fl = floor_of(c)
    free_slots = 3 - len(c.contracts)
    if free_slots > 0:
        b.sponsor(free_slots if P['ai_sp_fill'] else min(free_slots, 2 if c.funds < fl else 1))
    if c.funds < fl:
        for p in sorted([p for p in c.roster if p.id not in resting and p.id not in blocked], key=lambda p: -p.pop)[:2]:
            if b.can(p):
                b.add('stream', p)
        for o in offers:
            if o[0].stress < 70 and b.can(o[0]):
                b.accept(o)
    else:
        for o in offers:
            if o[0].stress < 60 and b.can(o[0]) and rd.random() < c.frugal:
                b.accept(o)
        if P['ai_maint_stream'] and c.funds < target_of(c):
            bench = sorted([p for p in c.roster if not any(p is q for q in st) and p.id not in blocked], key=lambda p: -p.pop)
            for p in bench[:P['ai_maint_stream']]:
                if b.can(p):
                    b.add('stream', p)
    prospect = None
    if not resting and b.pts >= 1:  # AMB-4: reserve the prospect point before starters absorb all points
        cands = [p for p in c.roster if not any(p is q for q in st) and p.age <= 21 and p.id not in blocked]
        if cands:
            prospect = max(cands, key=pot_est)
            if not b.can(prospect):
                prospect = None
    reserve = 1 if prospect is not None else 0
    cmd_trainer = None
    if seg % 3 == 0 and max(p.radar[p.lane][CMD] for p in st) < 60:
        cmd_trainer = max(st, key=lambda p: p.radar[p.lane][CMD])
    tl = []
    for p in st:
        if p.id in resting or p.id in blocked:
            continue
        tgt, fd = ai_train_target(lg, c, p, rd)
        tl.append((age_f(p.age) * phase_head(p, tgt), p, tgt, fd))
    tl.sort(key=lambda x: -x[0])
    for g, p, tgt, fd in tl:
        while b.can(p) and b.pts > reserve:
            if p is cmd_trainer:
                b.add('train', p, 'C', None)
                cmd_trainer = None
            else:
                b.add('train', p, tgt, fd)
    if prospect is not None and b.can(prospect):
        tgt, fd = ai_train_target(lg, c, prospect, rd)
        b.add('train', prospect, tgt, fd)
    return b


# ---------------- player bots
SENSIBLE = ('balanced', 'early', 'mid', 'late', 'star', 'counter', 'greedy', 'prerest', 'contract', 'scout', 'kda', 'bidup', 'norest')


def maint(lg, c, b, offers, rest_thr=60.0):
    st = starters(c)
    resting = set()
    for p in sorted(st, key=lambda p: -p.stress):
        n = 2 if p.stress > rest_thr + 20 else (1 if p.stress > rest_thr else 0)
        for _ in range(n):
            if b.can(p):
                b.add('rest', p)
                resting.add(p.id)
    free = 3 - len(c.contracts)
    if free > 0:
        b.sponsor(min(free, 3))
    W = wseg(c)
    if c.funds < 4 * W:
        for o in offers:
            if o[0].stress < 70 and b.can(o[0]):
                b.accept(o)
        n = 2 if c.funds >= 2 * W else 4
        cands = sorted([p for p in c.roster if p.id not in resting and p.id not in b.blocked],
                       key=lambda p: (any(p is q for q in st), -p.pop))
        for p in cands[:n]:
            if b.can(p):
                b.add('stream', p)
    else:
        for o in offers:
            if not any(o[0] is q for q in st) and b.can(o[0]):
                b.accept(o)
    return resting


def counter_phase(lg, c):
    sides = seg_sides(lg)
    others = [o for o in lg.clubs if o is not c]
    top = sorted(others, key=lambda o: -mean(sides[o.id].S.values()))[:4]
    med = {k: statistics.median(sides[o.id].S[k] for o in lg.clubs) for k in PH}
    cnt = collections.Counter()
    for o in top:
        S = sides[o.id].S
        cnt[max(PH, key=lambda k: S[k] - med[k])] += 1
    dom = max(PH, key=lambda k: cnt[k])
    return COUNTER_PHASE[dom]


def train_rest(lg, c, b, resting, chooser):
    st = starters(c)
    for p in sorted(st, key=r_main):
        if p.id in resting:
            continue
        while b.can(p):
            tgt, fd = chooser(p)
            b.add('train', p, tgt, fd)
    for p in sorted([p for p in c.roster if not any(p is q for q in st)], key=lambda p: p.age):
        while b.can(p):
            k = weakest_phase(p)
            b.add('train', p, k, lowest_dim(p, k))


def plan_bot(lg, c, seg, offers, rd, blocked):
    bot = c.ctrl
    b = Plan(blocked)
    oblig(c, b)
    if bot == 'idle':
        if P['idle_sponsor'] and 3 - len(c.contracts) > 0:
            b.sponsor(3 - len(c.contracts))
        return b
    st = starters(c)
    if bot == 'business':
        for p in sorted(st, key=lambda p: -p.stress):
            if p.stress > 70 and b.can(p):
                b.add('rest', p)
        free = 3 - len(c.contracts)
        if free > 0:
            b.sponsor(min(3, free))
        for o in offers:
            if b.can(o[0]) and o[0].stress < 75:
                b.accept(o)
        order = sorted(c.roster, key=lambda p: -p.pop)
        for _ in range(2):
            for p in order:
                if b.can(p) and p.stress < 85:
                    b.add('stream', p)
        return b
    if bot == 'greedy':
        return plan_greedy(lg, c, seg, offers, b)
    if bot == 'prerest' and seg in PRE:
        for p in sorted(st, key=lambda p: -p.stress):
            n = 2 if p.stress > 35 else (1 if p.stress > 20 else 0)
            for _ in range(n):
                if b.can(p):
                    b.add('rest', p)
    resting = maint(lg, c, b, offers, rest_thr=(101.0 if bot == 'norest' else 60.0))
    resting |= {a[1].id for a in b.asg if a[0] == 'rest'}
    if bot in ('balanced', 'prerest', 'norest', 'contract', 'scout', 'bidup'):
        def ch(p):
            k = weakest_phase(p)
            return k, lowest_dim(p, k)
    elif bot in ('early', 'mid', 'late'):
        kk = {'early': 'E', 'mid': 'M', 'late': 'L'}[bot]

        def ch(p):
            return kk, spec_or_lowest(p, kk)
    elif bot == 'kda':
        def ch(p):
            l = p.lane
            fd = DIMS[l].index('kda') if 'kda' in DIMS[l] else PIDX[l]['E'][0]
            if DPH[l][fd] != 'E':
                fd = PIDX[l]['E'][0]
            return 'E', fd
    elif bot == 'counter':
        kk = counter_phase(lg, c)

        def ch(p):
            return kk, spec_or_lowest(p, kk)
    elif bot == 'star':
        if getattr(c, 'bot_star', None) is None:
            c.bot_star = pick_star_lane(lg, c)
        sl = c.bot_star
        sp = c.lineup.get(sl)
        sph = 'L' if sl == 'adc' else ('E' if sl in ('jungle', 'top') else ('L' if praw(sp, 'L') >= praw(sp, 'E') else 'E'))

        def ch(p):
            if p is sp and p.lane == sl:
                return sph, spec_or_lowest(p, sph)
            k = weakest_phase(p)
            return k, lowest_dim(p, k)
    else:
        raise ValueError(bot)
    train_rest(lg, c, b, resting, ch)
    return b


def pick_star_lane(lg, c):
    best, bl = -1e9, 'adc'
    for l in ('jungle', 'mid', 'adc'):
        p = c.lineup.get(l)
        if p is None:
            continue
        others = [o.lineup[l] for o in lg.clubs if o is not c and l in o.lineup]
        d = r_main(p, l) - mean(r_main(q, l) for q in others)
        if d > best:
            best, bl = d, l
    return bl


def plan_greedy(lg, c, seg, offers, b):
    """each training point -> (starter, phase/command) that most raises mean p0 vs the 7 opponents."""
    resting = maint(lg, c, b, offers)
    sides = seg_sides(lg)
    lu = lineup_tuple(c)
    chem = chemistry(c, lu)
    hk = hooks_of(lineup_specs(lu))
    parts = [parts_of(p, l) for p, l in lu]
    opps = [(sides[o.id], o.tac_hist[-1] if o.tac_hist else 'invade') for o in lg.clubs if o is not c]
    tac = c.last_tac

    def obj(prts):
        A = side_from(prts, chem, hk)
        return sum(pwin(gtac(core(A, B), A, B, tac, tb)) for B, tb in opps) / len(opps)

    add = {p.id: [0.0] * 6 for p, l in lu}
    cur = obj(parts)
    while b.pts > 0:
        best = None
        for i, (p, l) in enumerate(lu):
            if p.id in resting or l != p.lane or not b.can(p):
                continue
            orig = p.radar[p.lane]
            cap = cap_of(p)
            for tgt in ('E', 'M', 'L', 'C'):
                fd = lowest_dim(p, tgt) if tgt != 'C' else None
                dl = point_deltas(p, tgt, fd, 1.0)
                virt = [min(cap, orig[d] + add[p.id][d] + dl[d]) if orig[d] < cap else orig[d] for d in range(6)]
                p.radar[p.lane] = virt
                np_ = list(parts)
                np_[i] = parts_of(p, l)
                p.radar[p.lane] = orig
                g = obj(np_) - cur
                if best is None or g > best[0]:
                    best = (g, i, tgt, fd, dl, np_[i])
        if best is None:
            break
        g, i, tgt, fd, dl, newpart = best
        p, l = lu[i]
        for d in range(6):
            add[p.id][d] += dl[d]
        parts[i] = newpart
        cur += g
        b.add('train', p, tgt, fd)
    st = starters(c)
    for p in sorted([p for p in c.roster if not any(p is q for q in st)], key=lambda p: p.age):
        while b.can(p):
            k = weakest_phase(p)
            b.add('train', p, k, lowest_dim(p, k))
    return b


# ---------------- choice events (league.md F5 for AI; bots: same rule with bot thresholds)
def decide_choice(lg, c, e, rolled, a, seg):
    kind = CHOICE_KIND.get(e['id'])
    if c.ctrl == 'idle':
        return e['default_option']
    is_ai = c.ctrl == 'ai'
    if is_ai:
        fl = floor_of(c)
        th = rest_th(c, False)
    else:
        fl = 4 * wseg(c)
        th = 60.0
    if c.ctrl == 'business':
        if kind == 'money':
            return 0 if (a is not None and a.stress < 70) else 1
        if kind == 'leave':
            return 0 if (a is not None and a.stress > 75 and seg not in PRE) else 1
        if kind == 'spend':
            return 1
        return 0
    if kind == 'leave':
        return 0 if (a is not None and a.stress > th and seg not in PRE) else 1
    if kind == 'money':
        return 0 if (c.funds < fl and a is not None and a.stress < 60) else 1
    if kind == 'spend':
        cost = -funds_of(rolled[0])
        lim = fl if is_ai else 8 * wseg(c)
        return 0 if c.funds - cost >= lim else 1
    if kind == 'conflict':
        return 0
    return e['default_option']


# ============================================================ segment (numbers.md 4.x + growth rev 2 + economy rev 2)
def run_segment(lg, c, seg):
    yr = lg.year
    sd = lg.seed
    ys = c.ys
    pick_lineup(lg, c)
    roster = list(c.roster)
    st = starters(c)
    stid = {p.id for p in st}
    rb = rng_for(sd, 'biz', c.id, yr, seg)
    g_ = yr * 12 + seg
    fz = fat_strength(c, g_) if P['fat_on'] else 0.0
    c._oblig = 0
    if fz:
        xx = P['fat_ap'] * fz
        c._oblig = int(xx) + (1 if (xx - int(xx)) >= 0.5 - 1e-9 and g_ % 2 == 0 else 0)
        ys['fat_segs'] += 1
    offers = []
    for p in roster:
        pr = biz_prob(p)
        if fz and p.id in stid:
            pr = min(1.0, pr * (1 + P['fat_biz'] * fz))
        if rb.random() < pr:
            offers.append((p, biz_amount(p, rb.uniform(0.8, 1.2))))
    rev = rng_for(sd, 'ev', c.id, yr, seg)
    choices = []
    blocked = set()
    if rev.random() < EVCFG['choice_events']['p_choice']:
        cands = [(e, subjects(e, c)) for e in CHOICE_EV]
        cands = [(e, s) for e, s in cands if s]
        if cands:
            k = pick_weighted([e for e, s in cands], rev)
            e, subj = cands[k]
            a, bb = (None, None) if e['subject'] == 'team' else subj[rev.randrange(len(subj))]
            rolled = [[roll_eff(f, rev) for f in o['effects']] for o in e['options']]
            opt = decide_choice(lg, c, e, rolled, a, seg)
            effs = rolled[opt]
            if a is not None and any(t == 'on_leave' for t, tg, v in effs):
                blocked.add(a.id)
            choices.append((effs, a, bb))
            ys['choice_%s_%d' % (CHOICE_KIND.get(e['id'], '?'), opt)] += 1
    rd = rng_for(sd, 'aidec', c.id, yr, seg)
    if c.ctrl == 'ai':
        plan = plan_ai(lg, c, seg, offers, rd, blocked)
    else:
        plan = plan_bot(lg, c, seg, offers, rd, blocked)
    points = [(a[0], a[1]) for a in plan.asg] + [('business', o[0]) for o in plan.acc] + [('sponsor', None)] * plan.sp
    mods = [None] * len(points)
    nev = 0
    act_evs = []
    for i, (act, p) in enumerate(points):
        if nev >= EVCFG['action_events']['max_per_segment']:
            break
        if not (rev.random() < EVCFG['action_events']['p_event']):
            continue
        if act == 'sponsor':
            pg = good_chance(mean(x.mood for x in st), mean(x.stress for x in st), 0.0)
        else:
            pg = good_chance(p.mood, p.stress, ev_trait_mod(p))
        tone = 'good' if rev.random() < pg else 'bad'
        cands = ACTION_EV.get((act, tone), [])
        if not cands:
            continue
        e = cands[pick_weighted(cands, rev)]
        effs = [roll_eff(f, rev) for f in e['effects']]
        mods[i] = effs
        act_evs.append((effs, p, None))
        nev += 1
        ys['ev_' + tone] += 1

    def mod(i, typ, default):
        if mods[i] is None:
            return default
        v = default
        for t, tg, x in mods[i]:
            if t == typ:
                v = (v * x) if typ.endswith('multiplier') else (v + x)
        return v

    # ---- training (growth rev 2: all points from segment-start values, merged; focus per point in plan order)
    rn = rng_for(sd, 'noise', c.id, yr, seg)
    tpn = collections.Counter()
    acc = {}
    tpts = []
    for i, (act, p, tgt, fd) in enumerate(plan.asg):
        if act != 'train':
            continue
        nz = rn.uniform(1 - P['noise'], 1 + P['noise'])
        dl = point_deltas(p, tgt, fd, nz, mod(i, 'train_multiplier', 1.0), mod(i, 'train_add', 0.0))
        a_ = acc.setdefault(p.id, [0.0] * 6)
        for d in range(6):
            a_[d] += dl[d]
        tpts.append((p, tgt))
        tpn[p.id] += 1
        ys['train_' + str(tgt)] += 1
    for p in roster:
        if p.id in acc:
            apply_deltas(p, acc[p.id])
            p.obs = min(100.0, p.obs + 3 * tpn[p.id])
    ys['train_pts'] += sum(tpn.values())
    # ---- sponsorship (economy rev 2 F8: no results term)
    rsp = rng_for(sd, 'sp', c.id, yr, seg)
    base_i = len(plan.asg) + len(plan.acc)
    for j in range(plan.sp):
        i = base_i + j
        ys['sp_att'] += 1
        if mods[i] is not None and any(t == 'void_action' for t, tg, x in mods[i]):
            continue
        noise = rsp.uniform(-0.1, 0.1)
        ch = clamp(P['z0'] + P['z1'] * c.pop / 100.0 + noise, 0.05, 0.8)
        addv = mod(i, 'sponsor_chance_add', 0.0)
        if addv:
            ch = clamp(ch + addv, 0.05, 0.8)
        if rsp.random() < ch and len(c.contracts) < 3:
            c.contracts.append([round(P['c0'] + P['c1'] * c.pop), P['sp_dur']])
            ys['sp_succ'] += 1
    # ---- business
    biz_income = 0.0
    bizp = collections.Counter()
    for j, (p, amt) in enumerate(plan.acc):
        i = len(plan.asg) + j
        share = round(amt * 0.7)
        extra = round(share * (mod(i, 'business_multiplier', 1.0) - 1))
        biz_income += max(0, share + extra)
        bizp[p.id] += 1
    # ---- mood & stress (segment-start values)
    rms = rng_for(sd, 'ms', c.id, yr, seg)
    restp = collections.defaultdict(float)
    streamp = collections.Counter()
    stream_mods = collections.defaultdict(list)
    for i, (act, p, tgt, fd) in enumerate(plan.asg):
        if act == 'rest':
            restp[p.id] += mod(i, 'rest_multiplier', 1.0)
            ys['rest_pts'] += 1
        elif act == 'stream':
            stream_mods[p.id].append(mod(i, 'stream_multiplier', 1.0))
            streamp[p.id] += 1
    newv = []
    for p in roster:
        sR = rms.uniform(0.9, 1.1)
        mR = rms.uniform(0.85, 1.15)
        raw = P['s_train'] * tpn[p.id] + P['s_biz'] * bizp[p.id] - P['s_rest'] * restp[p.id] + (P['fat_stress'] * fz if p.id in stid else 0.0)
        dS = raw * p.tm['stress_gain'] * sR - P['s_recover']
        neg = p.tm['negative_mood_change']
        if p.id in stid:
            play = P['m_play']
        else:
            play = -P['m_play'] * p.tm['bench_mood_drop'] * neg
        sat = satisfaction(p, lg.WI)
        sal = P['m_salary'] * sat * p.tm['salary_satisfaction_weight']
        if sal < 0:
            sal *= neg
        others = [q for q in st if q is not p]
        cm = P['m_chem'] * mean(compat(p, q) for q in others)
        if cm < 0:
            cm *= neg
        hs = -P['m_stress'] * neg if p.stress > 70 else 0.0
        br = (play + sal + cm + hs) * p.tm['mood_change_magnitude']
        dM = br * mR + P['m_gamma'] * (50 - p.mood)
        newv.append((p, clamp(p.stress + dS, 0, 100), clamp(p.mood + dM, 0, 100)))
        if p.id in stid:
            ys['sal_sat'] += sat
            ys['sal_sat_n'] += 1
    for p, s_, m_ in newv:
        p.stress = s_
        p.mood = m_
        p.obs = min(100.0, p.obs + 5)
    allev = list(choices) + act_evs
    for effs, a, bb in allev:
        apply_personal(effs, c, a, bb)
    if rev.random() < EVCFG['state_attempt_chance']:
        cands = [(e, subjects(e, c)) for e in STATE_EV]
        cands = [(e, s) for e, s in cands if s]
        while cands:
            k = pick_weighted([e for e, s in cands], rev)
            e, subj = cands.pop(k)
            if not (rev.random() < e['chance']):
                continue
            a, bb = (None, None) if e['subject'] == 'team' else subj[rev.randrange(len(subj))]
            effs = [roll_eff(f, rev) for f in e['effects']]
            apply_personal(effs, c, a, bb)
            allev.append((effs, a, bb))
            ys['state_' + e['id']] += 1
            break
    live = 0.0
    for p in roster:
        n = streamp[p.id]
        if not n:
            continue
        base = P['l_base'] + P['l_pop'] * p.pop
        inc = (1 + 0.5 * (n - 1)) * base
        for kk, m in enumerate(stream_mods[p.id]):
            marg = base if kk == 0 else base * 0.5
            inc += marg * (m - 1)
        live += max(0.0, round(inc))
    together = set()
    for i in range(5):
        for j in range(i + 1, 5):
            a, bb = st[i], st[j]
            k = pair_key(a, bb)
            together.add(k)
            c.fam[k] = clamp(c.fam.get(k, 0.0) + P['f_seg'] * fam_mult(a, bb), 0, 100)
    for k in list(c.fam):
        if k not in together:
            v = c.fam[k] - P['f_decay']
            if v <= 0:
                del c.fam[k]
            else:
                c.fam[k] = v
    rp = rng_for(sd, 'pop', c.id, yr, seg)
    wr = winrate(c)
    spop = mean(p.pop for p in st)
    eC = (spop + wr + 0.5 * c.rank_score) / 2.5
    dC = 0.2 * (eC - c.pop) * rp.uniform(0.9, 1.1)
    dps = []
    for p in roster:
        sh = min(100.0, streamp[p.id] * p.tm['stream_fans'] / 2.0 * 100)
        bh = min(100.0, bizp[p.id] * 100.0)
        e = (c.pop + r_main(p) + 0.8 * p.looks + 1.5 * sh + wr + 0.8 * bh) / 6.1
        dps.append(0.25 * (e - p.pop) * rp.uniform(0.85, 1.15))
    c.pop = clamp(c.pop + dC, 0, 100)
    for p, d in zip(roster, dps):
        p.pop = clamp(p.pop + d, 0, 100)
    spons = sum(ct[0] for ct in c.contracts)
    wages = wage_total(c) / 12.0
    ev_in = sum(v for effs, a, bb in allev for t, tg, v in effs if t == 'funds' and v > 0)
    ev_out = -sum(v for effs, a, bb in allev for t, tg, v in effs if t == 'funds' and v < 0)
    c.funds += live + biz_income + spons + ev_in - wages - ev_out
    ys['inc_live'] += live
    ys['inc_biz'] += biz_income
    ys['inc_sp'] += spons
    ys['inc_ev'] += ev_in
    ys['exp_ev'] += ev_out
    ys['wages'] += wages
    ys['income'] += live + biz_income + spons + ev_in
    for ct in c.contracts:
        ct[1] -= 1
    c.contracts = [ct for ct in c.contracts if ct[1] > 0]
    for effs, a, bb in allev:
        apply_after(effs, c, a, bb)
    mu = P['mu']
    if P['focus_counts']:
        cnt = collections.defaultdict(collections.Counter)
        for p, tgt in tpts:
            cnt[p.id][tgt] += 1
        for p in roster:
            if p.id in cnt:
                n = sum(cnt[p.id].values())
                keep = (1 - mu) ** n
                for k in PH:
                    p.focus[k] = keep * p.focus[k] + (1 - keep) * cnt[p.id][k] / n
    else:
        for p, tgt in tpts:
            if tgt == 'C':
                for k in PH:
                    p.focus[k] *= (1 - mu)
            else:
                for k in PH:
                    p.focus[k] = (1 - mu) * p.focus[k] + (mu if k == tgt else 0.0)
    for p in roster:
        check_specs(p, 'train')
    lu = lineup_tuple(c)
    ys['chem_sum'] += chemistry(c, lu)
    ys['mood_sum'] += mean(p.mood for p in st)
    ys['stress_sum'] += mean(p.stress for p in st)
    ys['segs'] += 1


def funds_check(lg, c, seg):
    """economy rev 2 F7: self-rescue (player and AI), then bankruptcy (player) / league loan (AI)."""
    if c.funds >= 0:
        c.fund_trace.append(c.funds)
        return
    ys = c.ys
    ys['rescue'] += 1
    st = starters(c)
    min_ros = P['ai_fire_min'] if (c.ctrl == 'ai' and not c.is_player) else 5
    while c.funds < 0 and len(c.roster) > min_ros:
        bench = [p for p in c.roster if not any(p is q for q in st)]
        if not bench:
            break
        p = min(bench, key=r_main)
        amt = value_of(p) * P['fire_sale']
        income_with_repay(c, amt)
        ys['fire_sales'] += 1
        ys['fire_amt'] += amt
        release(lg, c, p)
        lg.log[lg.year]['fire_sales'] += 1
    prune_fam(c)
    if c.funds < 0:
        if c.is_player and c.ctrl != 'ai' and c.bankrupt is None:
            c.bankrupt = (lg.year, seg)
            c.ctrl = 'ai'  # game over for the player; club keeps running under AI rules (bookkeeping only)
        elif c.is_player and c.bankrupt is None:
            c.bankrupt = (lg.year, seg)  # 'ai' bot as player club: no loan for the player -> bankrupt
        ys['loan'] += -c.funds
        c.loan_taken += -c.funds
        c.debt += -c.funds
        c.funds = 0.0
        lg.log[lg.year]['loans'] += 1
    c.fund_trace.append(c.funds)


def income_with_repay(c, amt):
    if c.debt > 0:
        rep = min(c.debt, P['repay'] * amt)
        c.debt -= rep
        c.funds += amt - rep
    else:
        c.funds += amt


# ============================================================ tournaments (numbers.md 4.18; tactic per stage)
def round_robin(n=8):
    t = list(range(n))
    rounds = []
    for r in range(n - 1):
        rounds.append([(t[i], t[n - 1 - i]) for i in range(n // 2)])
        t = [t[0]] + [t[-1]] + t[1:-1]
    return rounds


RR = round_robin(8)


def style_tactic(c):
    if c.style == 'star':
        return TAC_OF_PHASE[c.star_phase or 'E']
    return TAC_OF_PHASE[STYLE_PHASE[c.style]]


def ai_stage_tactic(lg, c, sides, opp, stage_key):
    me = sides[c.id][0].S
    if opp is None:
        rd = rng_for(lg.seed, 'tac', c.id, lg.year, lg.ename, stage_key)
        if rd.random() < P['p_style_noise']:
            return style_tactic(c)
        vals = {k: [me[k]] + [radar5(sides[o.id][0].S)[k] for o in lg.clubs if o is not c and o.id in sides] for k in PH}
        gap = {k: me[k] - statistics.median(vals[k]) for k in PH}
    else:
        os_ = radar5(sides[opp.id][0].S)
        gap = {k: me[k] - os_[k] for k in PH}
    return TAC_OF_PHASE[max(PH, key=lambda k: gap[k])]


def own_focus(lg, c, sides, opp):
    me = sides[c.id][0].S
    if opp is None:
        vals = {k: [me[k]] + [radar5(sides[o.id][0].S)[k] for o in lg.clubs if o is not c and o.id in sides] for k in PH}
        gap = {k: me[k] - statistics.median(vals[k]) for k in PH}
    else:
        os_ = radar5(sides[opp.id][0].S)
        gap = {k: me[k] - os_[k] for k in PH}
    return TAC_OF_PHASE[max(PH, key=lambda k: gap[k])]


def predicted_tac(lg, o, sides):
    if o.tac_hist:
        return o.tac_hist[-1]
    return own_focus(lg, o, sides, None)


def best_response(lg, c, sides, opps, bo):
    me = sides[c.id][0]
    best = None
    for t in TACTICS:
        v = 0.0
        for o in opps:
            B = sides[o.id][0]
            v += series_prob(pwin(gtac(core(me, B), me, B, t, predicted_tac(lg, o, sides))), bo)
        if best is None or v > best[0] + 1e-12:
            best = (v, t)
    return best[1]


def stage_tactic(lg, c, sides, opp, stage_key, bo):
    if c.ctrl == 'ai':
        return ai_stage_tactic(lg, c, sides, opp, stage_key)
    bot = c.ctrl
    if bot == 'idle':
        return 'invade'
    if bot in ('early', 'mid', 'late'):
        return {'early': 'invade', 'mid': 'teamfight', 'late': 'develop'}[bot]
    if bot == 'kda':
        return 'invade'
    if bot in ('counter', 'greedy'):
        opps = [o for o in lg.clubs if o is not c and o.id in sides] if opp is None else [opp]
        return best_response(lg, c, sides, opps, bo)
    return own_focus(lg, c, sides, opp)


def stage_effects(lg, c, series_list, rng):
    if not series_list:
        return
    cnt = collections.Counter()
    lost = collections.Counter()
    for ids, won in series_list:
        for pid in ids:
            cnt[pid] += 1
            if not won:
                lost[pid] += 1
    for p in c.roster:
        if p.id in cnt:
            r = rng.uniform(0.9, 1.1)
            p.stress = clamp(p.stress + (P['s_matchst'] * cnt[p.id] + P['s_loss'] * lost[p.id]) * p.tm['stress_gain'] * r, 0, 100)
    ros = {p.id: p for p in c.roster}
    for ids, won in series_list:
        if won:
            for i in range(5):
                for j in range(i + 1, 5):
                    a, b = ids[i], ids[j]
                    if a in ros and b in ros:
                        k = (a, b) if a < b else (b, a)
                        c.fam[k] = clamp(c.fam.get(k, 0.0) + P['f_win'], 0, 100)


def event_end(lg, c, series_list, rng):
    if not series_list:
        return
    won = collections.Counter()
    lost = collections.Counter()
    for ids, w in series_list:
        for pid in ids:
            if w:
                won[pid] += 1
            else:
                lost[pid] += 1
    for p in c.roster:
        if p.id in won or p.id in lost:
            r = rng.uniform(0.85, 1.15)
            sgn = (won[p.id] > lost[p.id]) - (won[p.id] < lost[p.id])
            if sgn > 0:
                d = P['m_result'] * p.tm['win_mood_gain']
            elif sgn < 0:
                d = -P['m_result'] * p.tm['loss_mood_drop'] * p.tm['negative_mood_change']
            else:
                d = 0.0
            p.mood = clamp(p.mood + d * p.tm['mood_change_magnitude'] * r, 0, 100)
    c.recent = (c.recent + [w for ids, w in series_list])[-10:]  # economy rev 2: rolling last 10 series


def record_sample(lg, A, B, sides):
    if lg.samples is None:
        return
    SA, SB = sides[A.id][0], sides[B.id][0]
    v, M = eq_value(SA, SB)
    lg.samples.append((tuple(round(SA.S[k], 2) for k in PH), tuple(round(SB.S[k], 2) for k in PH), round(v, 4),
                       round(max(max(M[i][j] for i in range(3)) - min(M[i][j] for i in range(3)) for j in range(3)), 4)))


def play_one(lg, A, B, sides, tac, key, bo, sl):
    rs = rng_for(lg.seed, 'm', lg.year, lg.ename) if False else rng_for(lg.seed, 'm', lg.year, lg.ename, *key)
    wa, wb = play_series(sides[A.id][0], sides[A.id][1], sides[B.id][0], sides[B.id][1], tac[A.id], tac[B.id], bo, rs)
    record_sample(lg, A, B, sides)
    sl[A.id].append((tuple(p.id for p, l in sides[A.id][2]), wa > wb))
    sl[B.id].append((tuple(p.id for p, l in sides[B.id][2]), wb > wa))
    return wa, wb


def run_bracket(lg, seeds4, bo_semi, bo_final, sides, tag, sl):
    tac = {}
    for i, c in enumerate(seeds4):
        opp = seeds4[3 - i]
        tac[c.id] = stage_tactic(lg, c, sides, opp, tag, bo_semi)
        c.last_tac = tac[c.id]
        c.tac_hist = (c.tac_hist + [tac[c.id]])[-5:]
    winners, losers = [], []
    for slot, (a, b) in enumerate(((0, 3), (1, 2))):
        A, B = seeds4[a], seeds4[b]
        wa, wb = play_one(lg, A, B, sides, tac, (tag, 'semi', slot), bo_semi, sl)
        winners.append(A if wa > wb else B)
        losers.append(B if wa > wb else A)
    A, B = winners
    wa, wb = play_one(lg, A, B, sides, tac, (tag, 'final'), bo_final, sl)
    ch, ru = (A, B) if wa > wb else (B, A)
    return ch, ru, losers


def award(lg, c, prize, honor_score, place):
    if prize:
        income_with_repay(c, prize)
        c.ys['inc_prize'] += prize
        c.ys['income'] += prize
    for p in c.roster:
        p.honor_year = max(p.honor_year, honor_score)


def run_split(lg, ename):
    lg.ename = ename
    yr = lg.year
    clubs = lg.clubs
    for c in clubs:
        pick_lineup(lg, c)
    sides = {c.id: club_sides(c) for c in clubs}
    tac = {}
    for c in clubs:
        tac[c.id] = stage_tactic(lg, c, sides, None, 'reg', 3)
        c.last_tac = tac[c.id]
        c.tac_hist = (c.tac_hist + [tac[c.id]])[-5:]
    table = {c.id: [0, 0, 0, 0] for c in clubs}
    tr = rng_for(lg.seed, 'tie', yr, ename)
    tie = {c.id: tr.random() for c in clubs}
    sl = {c.id: [] for c in clubs}
    for ri, pairs in enumerate(RR):
        for pi, (i, j) in enumerate(pairs):
            A, B = clubs[i], clubs[j]
            wa, wb = play_one(lg, A, B, sides, tac, ('reg', ri, pi), 3, sl)
            ta, tb_ = table[i], table[j]
            if wa > wb:
                ta[0] += 1
                tb_[1] += 1
            else:
                tb_[0] += 1
                ta[1] += 1
            ta[2] += wa
            ta[3] += wb
            tb_[2] += wb
            tb_[3] += wa
    ordr = sorted(clubs, key=lambda c: (-table[c.id][0], -(table[c.id][2] - table[c.id][3]), tie[c.id]))
    reg_rank = {c.id: k + 1 for k, c in enumerate(ordr)}
    rse = rng_for(lg.seed, 'stage', yr, ename)
    for c in clubs:
        stage_effects(lg, c, sl[c.id], rse)
    top4 = ordr[:4]
    for c in top4:
        pick_lineup(lg, c)
    sides2 = dict(sides)
    for c in top4:
        sides2[c.id] = club_sides(c)
    sl2 = {c.id: [] for c in clubs}
    ch, ru, losers = run_bracket(lg, top4, 5, 5, sides2, 'po', sl2)
    for c in clubs:
        stage_effects(lg, c, sl2[c.id], rse)
    for c in clubs:
        event_end(lg, c, sl[c.id] + sl2[c.id], rse)
    losers = sorted(losers, key=lambda c: reg_rank[c.id])
    places = {ch.id: 1, ru.id: 2, losers[0].id: 3, losers[1].id: 4}
    rest = [c for c in ordr if c.id not in places]
    for k, c in enumerate(rest):
        places[c.id] = 5 + k
    for c in clubs:
        pl = places[c.id]
        award(lg, c, SPLIT_PRIZE[pl - 1], HONOR_SPLIT[pl - 1], pl)
        c.rank_score = (8 - (3.5 if pl in (3, 4) else pl)) / 7.0 * 100.0
        c.year_events.append((ename, pl, table[c.id][0]))
    lg.log[yr]['champ_' + ename] = ch.id
    if P['fat_on']:
        g0_ = yr * 12 + lg.seg
        ch.fat.append((g0_ + 1, g0_ + 12, P['fat_split']))
    return places


def run_finals(lg):
    lg.ename = 'finals'
    yr = lg.year
    clubs = lg.clubs
    tr = rng_for(lg.seed, 'tie', yr, 'finals')
    tie = {c.id: tr.random() for c in clubs}
    pts = {}
    for c in clubs:
        evs = c.year_events[-2:]
        pts[c.id] = (sum(FINAL_PTS[pl - 1] for e, pl, w in evs), sum(w for e, pl, w in evs))
    ordr = sorted(clubs, key=lambda c: (-pts[c.id][0], -pts[c.id][1], tie[c.id]))
    top4 = ordr[:4]
    for c in top4:
        pick_lineup(lg, c)
    sides = {c.id: club_sides(c) for c in top4}
    sl = {c.id: [] for c in clubs}
    ch, ru, losers = run_bracket(lg, top4, 5, 7, sides, 'fin', sl)
    rse = rng_for(lg.seed, 'stage', yr, 'finals')
    for c in clubs:
        stage_effects(lg, c, sl[c.id], rse)
    for c in clubs:
        event_end(lg, c, sl[c.id], rse)
    seedrank = {c.id: k for k, c in enumerate(ordr)}
    losers = sorted(losers, key=lambda c: seedrank[c.id])
    places = {ch.id: 1, ru.id: 2, losers[0].id: 3, losers[1].id: 4}
    rest = [c for c in ordr if c.id not in places]
    for k, c in enumerate(rest):
        places[c.id] = 5 + k
    for c in clubs:
        pl = places[c.id]
        award(lg, c, FINAL_PRIZE[pl - 1] * P['final_prize_mult'], HONOR_FINAL[pl - 1], pl)
        c.rank_score = (8 - (3.5 if pl in (3, 4) else pl)) / 7.0 * 100.0
        c.year_events.append(('finals', pl, 0))
    lg.log[yr]['champ_finals'] = ch.id
    if P['fat_on']:
        ch.fat.append((yr * 12 + 12, yr * 12 + 23, 1.0))
    lg.champs.append((yr, ch.id, ch.style, ch.ctrl if ch.is_player else 'AI'))
    return places


# ============================================================ league-level yearly rules
def style_label(lg, c, sides=None):
    sides = sides or {o.id: club_sides(o)[0] for o in lg.clubs}
    med = {k: statistics.median(sides[o.id].S[k] for o in lg.clubs) for k in PH}
    lead_lane, lead = star_lane_lead(lg, c)
    if lead >= P['label_star_lead']:
        return 'star'
    k = max(PH, key=lambda k: sides[c.id].S[k] - med[k])
    return STYLE_OF_PHASE[k]


def star_lane_lead(lg, c):
    best = (None, -1e9)
    for l in LANES:
        p = c.lineup.get(l)
        if p is None or p.lane != l:
            continue
        vals = [r_main(o.lineup[l], l) for o in lg.clubs if l in o.lineup]
        d = r_main(p) - statistics.median(vals)
        if d > best[1]:
            best = (l, d)
    return best


def style_eval(lg):
    """league.md rev 2 风格调整 (after finals, AI clubs incl. the 'ai' bot)."""
    yr = lg.year
    sides = {o.id: club_sides(o)[0] for o in lg.clubs}
    for c in lg.clubs:
        c.events_since_switch += 3
        if c.ctrl != 'ai':
            continue
        forced = c.style == 'star' and c.star_pid not in {p.id for p in c.roster}
        missed = sum(1 for e, pl, w in c.year_events if pl > 4)
        if not forced:
            if missed < 2 or c.events_since_switch < P['style_lock']:
                continue
            u = rng_for(lg.seed, 'style', c.id, yr).random()
            if u >= c.adapt * P['p_switch']:
                continue
        me = sides[c.id].S
        vals = {k: [me[k]] + [radar5(sides[o.id].S)[k] for o in lg.clubs if o is not c] for k in PH}
        gap = {k: me[k] - statistics.median(vals[k]) for k in PH}
        cur_ph = c.star_phase if c.style == 'star' else STYLE_PHASE[c.style]
        lane, lead = star_lane_lead(lg, c)
        n_star = sum(1 for o in lg.clubs if o is not c and o.style == 'star')
        old = c.style
        no_star_return = P['style_noreturn_star'] and c.style_hist and c.style_hist[-1][1] == 'star'
        if lane is not None and lead >= P['star_lead'] and n_star <= 1 and c.style != 'star' and not no_star_return:  # AMB-5
            c.style = 'star'
            c.star_lane = lane
            sp = c.lineup[lane]
            c.star_pid = sp.id
            c.star_phase = 'E' if praw(sp, 'E') >= praw(sp, 'L') else 'L'
        else:
            cands = [k for k in PH if k != cur_ph] if c.style != 'star' or not forced else list(PH)
            if c.style_hist and (P['style_noreturn'] or (P['style_noreturn_years'] and yr - c.style_hist[-1][0] < P['style_noreturn_years'])):
                prev = c.style_hist[-1][1]
                if prev in STYLE_PHASE and len(cands) > 1:
                    cands = [k for k in cands if k != STYLE_PHASE[prev]] or cands
            if P['style_prefer_rare']:
                rare = [k for k in cands if sum(1 for o in lg.clubs if o is not c and o.style == STYLE_OF_PHASE[k]) <= 1]
                cands = rare or cands
            k = max(cands, key=lambda k: gap[k])
            c.style = STYLE_OF_PHASE[k]
            c.star_lane = c.star_pid = c.star_phase = None
        if c.style != old:
            c.events_since_switch = 0
            c.style_hist.append((yr, old, c.style))
            lg.log[yr]['style_switch'] += 1


def end_year(lg):
    for c in lg.clubs:
        for p in c.roster:
            p.honor = p.honor_year
            p.honor_year = 0.0
    for p in lg.free:
        p.honor = 0.0
        p.honor_year = 0.0
    style_eval(lg)


def run_year(lg, yr):
    lg.year = yr
    for c in lg.clubs:
        c.ys = new_ys()
        c.year_events = []
        c.fund_trace = []
        c.ys['funds_start'] = c.funds
        c.ys['wb_start'] = wage_total(c)
        c.ys['debt_start'] = c.debt
        for p in c.roster:
            p.snap = (p.age, r_main(p), mean(praw(p, k) for k in PH), p.lane)
    for seg in range(12):
        lg.seg = seg
        for c in lg.clubs:
            run_segment(lg, c, seg)
        if seg == 2:
            run_split(lg, 'spring')
        elif seg == 5:
            run_split(lg, 'summer')
        elif seg == 11:
            run_finals(lg)
        for c in lg.clubs:
            funds_check(lg, c, seg)
    end_year(lg)


# ============================================================ transfer window (transfer.md rev 2)
def ai_reneg(lg, c, p, m):
    if P['ai_reneg_zero']:
        return c.funds >= 0 and any(p is q for q in starters(c))
    return c.funds >= floor_of(c) and any(p is q for q in starters(c))


def bot_reneg(lg, c, p, m):
    bot = c.ctrl
    if bot in ('idle', 'contract', 'business'):
        return False
    if not any(p is q for q in starters(c)):
        return False
    return c.funds >= 4 * (wage_total(c) - p.salary + m) / 12.0


def ai_renew(lg, c, p):
    years = 3 if p.age <= 23 else (2 if p.age <= 27 else 1)
    ask = sign_salary(lg, p, years, 'renew', renewal=True)
    bill = wage_total(c) - p.salary + ask
    fl = floor_of(c, bill)
    if any(p is q for q in starters(c)) or sole_main(c, p):
        ok = c.funds >= (fl if P['ai_renew_mode'] == 'floor' else 0.0)
        if ok and P['ai_renew_age_test'] and p.age > 28 and not sole_main(c, p):
            ok = value_of(p) / (ask / lg.WI) >= P['bench_renew'] + c.off_renew
    else:
        ok = value_of(p) / (ask / lg.WI) >= P['bench_renew'] + c.off_renew and c.funds >= fl
    return (years, ask) if ok else (0, ask)


def bot_renew(lg, c, p):
    bot = c.ctrl
    if bot == 'contract':
        years = 3
    elif bot == 'idle':
        years = 2
    else:
        years = 3 if p.age <= 23 else (2 if p.age <= 27 else 1)
    ask = sign_salary(lg, p, years, 'renew', renewal=True)
    if bot == 'idle':
        return years, ask  # AMB-6: idle renews everyone
    bill = wage_total(c) - p.salary + ask
    st = any(p is q for q in starters(c))
    if bot == 'business':
        keep = sole_main(c, p) or (st and value_of(p) / (ask / lg.WI) >= 1.5)
        return (years, ask) if keep and c.funds >= 2 * bill / 12.0 else (0, ask)
    if P['bot_renew_zero'] and bot in SENSIBLE and (st or sole_main(c, p)):
        return (years, ask) if c.funds >= 0 else (0, ask)
    keep = st or sole_main(c, p) or (p.age <= 21 and pot_est(p) >= 65) or value_of(p) / (ask / lg.WI) >= 2.0
    return (years, ask) if keep and c.funds >= 3 * bill / 12.0 else (0, ask)


def poach_fee(lg, buyer, p):
    return value_of(p) * rng_for(lg.seed, 'fee', lg.year, buyer.id, p.id).uniform(P['fee_lo'], P['fee_hi'])


def ai_poach_target(lg, c, offered):
    if len(c.roster) >= 8:
        return None
    if P['spend_reserve_W'] and P['poach_use_floor']:
        cap_fee = c.funds - floor_of(c)
    elif P['spend_reserve_W']:
        cap_fee = c.funds - P['spend_reserve_W'] * wseg(c)
    else:
        if c.funds < target_of(c):
            return None
        cap_fee = c.funds - floor_of(c)
    wl = weakest_lane(c)
    best = None
    best_ch = None
    champ_id = lg.champs[-1][1] if (P['poach_champ_first'] and lg.champs) else None
    for o in lg.clubs:
        if o is c or len(o.roster) <= 5:
            continue
        for l, p in o.lineup.items():
            if p.id in offered or l != p.lane or sole_main(o, p):
                continue
            sc = r_main(p) * (1.5 if p.lane == wl else 1.0) * (2.0 if p.wants else 1.0)
            if P['poach_honor']:
                sc *= 1 + p.honor / 100.0
            fee = poach_fee(lg, c, p)
            if fee > cap_fee:
                continue
            if best is None or sc > best[0]:
                best = (sc, p, o, fee)
            if o.id == champ_id and (best_ch is None or sc > best_ch[0]):
                best_ch = (sc, p, o, fee)
    pick = best_ch if best_ch is not None else best
    return pick[1:] if pick else None


def bot_poach_target(lg, c, offered):
    if c.ctrl in ('idle', 'business') or len(c.roster) >= 8:
        return None
    wl = weakest_lane(c)
    cur = c.lineup.get(wl)
    cur_r = r_main(cur) if cur is not None and cur.lane == wl else 0.0
    best = None
    for o in lg.clubs:
        if o is c or len(o.roster) <= 5:
            continue
        for l, p in o.lineup.items():
            if p.id in offered or l != p.lane or p.lane != wl or sole_main(o, p):
                continue
            if r_main(p) < cur_r + 3:
                continue
            fee = poach_fee(lg, c, p)
            bill = wage_total(c) + market_salary(p, lg.WI)
            if c.funds - fee < 6 * bill / 12.0:
                continue
            if best is None or r_main(p) > best[0]:
                best = (r_main(p), p, o, fee)
    return best[1:] if best else None


def ai_poach_resp(lg, o, p, fee, new_sal):
    if p.wants:
        return 'accept'
    if o.funds < floor_of(o) and fee >= P['ai_accept_fee'] * value_of(p):
        return 'accept'
    if not p.retained:
        rs = new_sal * P['retain_mult']
        if rs <= value_of(p) / 1.5 and o.funds >= floor_of(o, wage_total(o) - p.salary + rs):
            return 'retain'
    return 'accept'


def bot_poach_resp(lg, o, p, fee, new_sal):
    if o.ctrl in ('idle', 'business') or p.retained or not any(p is q for q in starters(o)):
        return 'accept'
    rs = new_sal * P['retain_mult']
    return 'retain' if o.funds >= 3 * (wage_total(o) - p.salary + rs) / 12.0 else 'accept'


def can_list(lg, c, p, n_listed):
    if len(c.roster) - n_listed - 1 < 5:
        return False
    if any(p is q for q in starters(c)) and sole_main(c, p):
        return False
    if p.joined == lg.year + 0.5:
        return False
    return True


def ai_three(lg, c, min_lot):
    W = wseg(c)
    fl = floor_of(c)
    st = starters(c)
    if c.debt > P['debt_line'] * wage_total(c):
        cands = sorted(st, key=lambda p: -p.salary)
        for p in cands:
            if can_list(lg, c, p, 0):
                return 'sell', [p]
        return 'skip', None
    if c.funds < fl:
        bench = sorted([p for p in c.roster if not any(p is q for q in st)], key=lambda p: value_of(p) / max(1.0, p.salary))
        out = []
        for p in bench:
            if len(out) >= (2 if len(c.roster) > 6 else 1):
                break
            if can_list(lg, c, p, len(out)):
                out.append(p)
        return ('sell', out) if out else ('skip', None)
    missing = [l for l in LANES if not any(p.lane == l for p in c.roster)]
    res = P['spend_reserve_W'] * W if P['spend_reserve_W'] else fl
    if len(c.roster) < 6 or missing:
        if c.funds - min_lot >= res:
            return 'auction', None
        if len(c.roster) < 8:
            return 'scout', (missing[0] if missing else weakest_lane(c))
        return 'skip', None
    if (c.funds - min_lot >= res and len(c.roster) < 8) if P['spend_reserve_W'] else c.funds >= target_of(c):
        return 'auction', None
    if len(c.roster) <= 7:
        return 'scout', weakest_lane(c)
    return 'skip', None


def bot_three(lg, c, min_lot):
    bot = c.ctrl
    W = wseg(c)
    st = starters(c)
    if bot == 'idle':
        return 'skip', None
    if bot == 'business':
        if len(c.roster) >= 7:
            bench = sorted([p for p in c.roster if not any(p is q for q in st)], key=lambda p: -value_of(p))
            out = []
            for p in bench:
                if len(out) >= 2:
                    break
                if can_list(lg, c, p, len(out)):
                    out.append(p)
            if out:
                return 'sell', out
        return 'skip', None
    if bot == 'bidup':
        return 'auction', None
    if bot == 'scout':
        if len(c.roster) < 8 and c.funds >= P['scout_cost'] + 2 * W:
            return 'scout', weakest_lane(c)
        return 'skip', None
    if c.funds < 3 * W:
        bench = sorted([p for p in c.roster if not any(p is q for q in st)], key=lambda p: value_of(p) / max(1.0, p.salary))
        out = [p for p in bench[:1] if can_list(lg, c, p, 0)]
        return ('sell', out) if out else ('skip', None)
    missing = [l for l in LANES if not any(p.lane == l for p in c.roster)]
    if len(c.roster) < 6 or missing:
        if c.funds >= min_lot + 4 * W:
            return 'auction', None
        if len(c.roster) < 8 and c.funds >= P['scout_cost'] + 2 * W:
            return 'scout', (missing[0] if missing else weakest_lane(c))
        return 'skip', None
    if c.funds >= 6 * W + 300000:
        return 'auction', None
    if len(c.roster) <= 6 and c.funds >= P['scout_cost'] + 4 * W:
        return 'scout', weakest_lane(c)
    return 'skip', None


def ai_max_bid(lg, c, p):
    u = rng_for(lg.seed, 'bid', lg.year, c.id, p.id).uniform(0.9, 1.1)
    return value_of(p) * (0.9 + 0.5 * need_of(c, p.lane)) * (1 + 0.2 * c.aggr) * (1 + c.off_bid) * u


def bot_bid_limit(lg, c, p):
    bot = c.ctrl
    W = wseg(c)
    if bot == 'bidup':
        return min(0.9 * value_of(p), c.funds - 3 * W)
    nd = need_of(c, p.lane)
    if nd == 0 and len(c.roster) >= 6:
        return 0.0
    return min(value_of(p) * (0.9 + 0.5 * nd) * 1.1, c.funds - 4 * W)


def scout_need(lg, c, lane):
    if P['scout_need_mode'] != 'median5':
        return need_of(c, lane)
    mains = [p for p in c.roster if p.lane == lane]
    if not mains:
        return 1.0
    best = max(r_main(p) for p in mains)
    med = statistics.median([max([r_main(q) for q in o.roster if q.lane == lane] or [0.0]) for o in lg.clubs])
    if best <= med - 5:
        return 1.0
    return 0.5 if len(mains) == 1 else 0.0


def scout(lg, c, lane, yr):
    nd = scout_need(lg, c, lane)
    tp = clamp(P['t0'] + P['t1'] * nd, P['t0'], P['t_max'])
    mu = P['y0'] + P['y1'] * mean(x.pop for x in lg.clubs) / 100.0
    p = gen_player(lg, rng_for(lg.seed, 'scout', yr, c.id), lane, 17, 20, mu, talent_p=tp, origin='scout')
    c.funds -= P['scout_cost']
    c.ys['xfer_out'] += P['scout_cost']
    sign(lg, c, p, 3, 'scout')
    lg.log[yr]['scouted'] += 1
    lg.log[yr]['scout_pot_sum'] += p.pot
    lg.log[yr]['scout_talent'] += 1 if p.pot >= 85 else 0
    c.xs['scouted'] += 1


def resign_fill(lg, c, yr):
    """step 7 (AI rule; bots: same shape with own affordability)."""
    is_ai = c.ctrl == 'ai'
    bot = c.ctrl
    target = 6
    if bot == 'idle':
        target = 5
    elif bot == 'scout':
        target = 7

    def afford(p):
        sal = sign_salary(lg, p, 2, 'resign')
        bill = wage_total(c) + sal
        if is_ai:
            return c.funds >= (floor_of(c, bill) if P['ai_fill_mode'] == 'floor' else 0.0)
        return c.funds >= 3 * bill / 12.0

    def pool(lane=None):
        return [p for p in lg.free if p.lock_year != yr and (lane is None or p.lane == lane)]

    for l in LANES:
        if len(c.roster) >= 8:
            break
        if any(p.lane == l for p in c.roster):
            continue
        cands = [p for p in pool(l) if afford(p)]
        if cands:
            p = max(cands, key=r_main)
            lg.free.remove(p)
            sign(lg, c, p, 2, 'resign')
            lg.log[yr]['resigned'] += 1
    while len(c.roster) < target:
        wl = weakest_lane(c)
        cands = [p for p in pool(wl) if afford(p)] or [p for p in pool() if afford(p)]
        if not cands:
            break
        p = max(cands, key=r_main)
        lg.free.remove(p)
        sign(lg, c, p, 2, 'resign')
        lg.log[yr]['resigned'] += 1
    while len(c.roster) < 5 or any(not any(p.lane == l for p in c.roster) for l in LANES) and len(c.roster) < 8:
        missing = [l for l in LANES if not any(p.lane == l for p in c.roster)]
        cands = [p for p in pool() if p.lane in missing] if missing else pool()
        if not cands:
            if len(c.roster) >= 5:
                break
            emergency_sign(lg, c)
            continue
        p = max(cands, key=r_main)
        lg.free.remove(p)
        sign(lg, c, p, 2, 'resign')
        lg.log[yr]['resigned_forced'] += 1


def run_lot(lg, lot, parts, yr):
    p, seller, kind, start = lot
    order = sorted([c for c in parts if c is not seller and len(c.roster) < 8], key=lambda c: -c.pop)
    lim = {}
    for c in order:
        if c.ctrl == 'ai':
            lim[c.id] = ai_max_bid(lg, c, p)
        else:
            lim[c.id] = bot_bid_limit(lg, c, p)
    active = {c.id for c in order}
    cur = start
    high = None
    bidders = set()
    while True:
        raised = False
        for c in order:
            if c.id not in active or c.id == high:
                continue
            step = round100(max(P['step_min'], P['step_pct'] * cur))
            bid = cur + step
            if c.ctrl == 'ai':
                resv = P['spend_reserve_W'] * wseg(c) if P['spend_reserve_W'] else floor_of(c)
                ok = bid <= lim[c.id] and c.funds - bid >= resv and len(c.roster) + 1 <= 8
            else:
                ok = bid <= lim[c.id] and len(c.roster) + 1 <= 8 and c.funds - bid >= 0
            if ok:
                cur = bid
                high = c.id
                raised = True
                bidders.add(c.id)
            else:
                active.discard(c.id)
        if not raised:
            break
    return high, cur, bidders


def transfer_window(lg, yr):
    clubs = lg.clubs
    L = lg.log[yr]
    seed = lg.seed
    for c in clubs:
        c.xs = collections.defaultdict(float)
        c.xs['wb_before'] = wage_total(c)
    # ---- 1. rollover
    allp = [p for c in clubs for p in c.roster] + lg.free
    for p in allp:
        d = decline(p.age)
        if d:
            for l in LANES:
                v = p.radar[l]
                for i in range(6):
                    v[i] = max(0.0, v[i] - d)
        p.age += 1
    for c in clubs:
        for p in c.roster:
            p.contract -= 1
            check_specs(p, 'train')
    for p in lg.free:
        check_specs(p, 'train')
    for c in clubs:
        for p in list(c.roster):
            if p.contract <= 0 and p.age >= 31:
                if p.age >= 33 or rng_for(seed, 'ret', yr, p.id).random() < 0.5:
                    remove_from(c, p)
                    L['retired_signed'] += 1
        prune_fam(c)
    for p in list(lg.free):
        if p.age > 30 or (p.age >= 28 and rng_for(seed, 'ret', yr, p.id).random() < 0.3):
            lg.free.remove(p)
            L['retired_free'] += 1
    lp = mean(c.pop for c in clubs)
    mu = P['y0'] + P['y1'] * lp / 100.0
    L['league_pop'] = lp
    rt = rng_for(seed, 'talent', yr)
    youth = []
    for _ in range(P['n_youth']):
        p = gen_player(lg, rt, rt.choice(LANES), 17, 19, mu, origin='youth')
        p.youth_year = yr
        youth.append(p)
    frees = [gen_player(lg, rt, rt.choice(LANES), 21, 27, mu, origin='free') for _ in range(P['n_free'])]
    lg.free += youth + frees
    if len(lg.free) > P['pool_cap']:  # AMB-7: this year's youth batch is exempt from the cap
        others = sorted([p for p in lg.free if p.youth_year != yr], key=r_main)
        n_rm = len(lg.free) - P['pool_cap']
        for p in others[:n_rm]:
            lg.free.remove(p)
            L['pool_cap_removed'] += 1
    if P['window_share']:
        for c in clubs:
            income_with_repay(c, P['window_share'])
            c.ys['income'] += P['window_share']
            c.ys['inc_share'] += P['window_share']
    inc = sum(c.ys['income'] for c in clubs)
    exp = sum(c.ys['wages'] + c.ys['exp_ev'] for c in clubs)
    R = inc / max(1.0, exp)
    L['R_league'] = R
    lg.WI = clamp(lg.WI * (1 + P['kappa_wi'] * (R / P['R_ref'] - 1)), P['WI_min'], P['WI_max'])
    L['WI'] = lg.WI
    for c in clubs:
        pick_lineup(lg, c)
    # ---- 2. renegotiation
    for c in clubs:
        for p in list(c.roster):
            if p.contract >= 1 and p.reneg_block != yr:
                m = market_salary(p, lg.WI)
                if m >= P['r_ratio'] * p.salary:
                    L['reneg_asked'] += 1
                    c.xs['reneg_asked'] += 1
                    ok = ai_reneg(lg, c, p, m) if c.ctrl == 'ai' else bot_reneg(lg, c, p, m)
                    if ok:
                        c.xs['reneg_raise'] += m - p.salary
                        p.salary = float(round(m))
                        L['reneg_agree'] += 1
                    else:
                        p.mood = clamp(p.mood - 15, 0, 100)
                        p.wants = True
                        p.reneg_block = yr + 1
                        if P['reneg_autolist']:
                            p.autolist = yr
                        L['reneg_refuse'] += 1
                        c.xs['reneg_refuse'] += 1
    # ---- 3. renewals
    for c in clubs:
        st = starters(c)
        exp_ = [p for p in c.roster if p.contract <= 0]
        exp_.sort(key=lambda p: (0 if any(p is q for q in st) else 1, -r_main(p)))
        for p in exp_:
            years, ask = ai_renew(lg, c, p) if c.ctrl == 'ai' else bot_renew(lg, c, p)
            c.xs['renew_asked'] += 1
            if years:
                c.xs['renew_raise'] += ask - p.salary
                p.salary = ask
                p.contract = years
                p.retained = False
                c.xs['renewed'] += 1
                L['renewed'] += 1
            else:
                release(lg, c, p)
                c.xs['released'] += 1
                L['released'] += 1
                if any(p is q for q in st):
                    c.xs['released_starter'] += 1
                    if c.ctrl == 'ai':
                        L['released_starter_ai'] += 1
        prune_fam(c)
        if len(c.roster) >= 5:
            pick_lineup(lg, c)
    for c in clubs:
        if len(c.roster) < 5:
            resign_fill(lg, c, yr)  # keep invariants for the poach step
            pick_lineup(lg, c)
    # ---- 4. poaching
    offered = set()
    for c in clubs:
        t = ai_poach_target(lg, c, offered) if c.ctrl == 'ai' else bot_poach_target(lg, c, offered)
        if t is None:
            continue
        p, o, fee = t
        if p not in o.roster or len(o.roster) <= 5 or sole_main(o, p):
            continue
        offered.add(p.id)
        L['poach_offers'] += 1
        o.xs['poach_recv'] += 1
        c.xs['poach_made'] += 1
        new_sal = sign_salary(lg, p, 2, 'poach')
        resp = ai_poach_resp(lg, o, p, fee, new_sal) if o.ctrl == 'ai' else bot_poach_resp(lg, o, p, fee, new_sal)
        if resp == 'accept':
            remove_from(o, p)
            income_with_repay(o, fee)
            o.ys['xfer_in'] += fee
            c.funds -= fee
            c.ys['xfer_out'] += fee
            p.salary = new_sal
            p.contract = 2
            p.obs = 0.0
            p.retained = False
            p.wants = False
            p.joined = yr + 0.5
            c.roster.append(p)
            L['poach_acc'] += 1
            o.xs['poach_lost'] += 1
            if c.ctrl == 'ai' and o.ctrl == 'ai' and not c.is_player and not o.is_player:
                L['ai_ai_poach'] += 1
            prune_fam(o)
            pick_lineup(lg, o)
            pick_lineup(lg, c)
        else:
            o.xs['retain_cost'] += new_sal * P['retain_mult'] - p.salary
            p.salary = float(round(new_sal * P['retain_mult']))
            p.contract += 1
            p.retained = True
            L['poach_ret'] += 1
            o.xs['poach_retained'] += 1
    # ---- 5. three-choice (player first, AI by id)
    youth_pool = [p for p in lg.free if p.youth_year == yr]
    carry = [p for p in lg.free if p.carry]
    ylots = sorted(youth_pool, key=lambda p: -pot_est(p))[:P['n_lots_youth']]
    mature = [p for p in lg.free if p.youth_year != yr and not p.carry]
    flots = sorted(mature, key=lambda p: -r_main(p))[:P['n_lots_free']]
    league_lots = [(p, None, 'youth', value_of(p) * 0.4) for p in ylots] + \
                  [(p, None, 'carry', value_of(p) * 0.4 * P['unsold_mult']) for p in carry] + \
                  [(p, None, 'free', value_of(p) * 0.4) for p in flots]
    min_lot = min([s + round100(max(P['step_min'], P['step_pct'] * s)) for p, _, _, s in league_lots] or [1e9])
    choice = {}
    listed = []
    order = sorted(clubs, key=lambda c: (0 if c.is_player else 1, c.id))
    for c in order:
        ch, info = ai_three(lg, c, min_lot) if c.ctrl == 'ai' else bot_three(lg, c, min_lot)
        if ch == 'scout' and len(c.roster) >= 8:
            ch = 'skip'
        choice[c.id] = ch
        L['choice_' + ch] += 1
        c.xs['choice_' + ch] += 1
        if ch == 'sell':
            for p in info:
                u = rng_for(seed, 'lot', yr, p.id).uniform(0.5, 0.8)
                listed.append((p, c, 'listed', value_of(p) * u * (0.85 + 0.1 * max(0, p.contract))))
                L['listed'] += 1
        elif ch == 'scout':
            scout(lg, c, info, yr)
    if P['reneg_autolist']:
        for c in order:
            nl = sum(1 for x in listed if x[1] is c)
            for p in list(c.roster):
                if p.autolist == yr and not any(x[0] is p for x in listed) and can_list(lg, c, p, nl):
                    u = rng_for(seed, 'lot', yr, p.id).uniform(0.5, 0.8)
                    listed.append((p, c, 'listed', value_of(p) * u * (0.85 + 0.1 * max(0, p.contract))))
                    nl += 1
                    L['listed'] += 1
                    L['autolisted'] += 1
    # ---- 6. auction
    parts = [c for c in clubs if choice[c.id] == 'auction']
    lots = sorted(listed, key=lambda x: -x[3]) + sorted([x for x in league_lots if x[2] in ('youth', 'carry')], key=lambda x: -x[3]) + \
        sorted([x for x in league_lots if x[2] == 'free'], key=lambda x: -x[3])
    for lot in lots:
        p, seller, kind, start = lot
        if seller is not None and p not in seller.roster:
            continue
        if seller is None and p not in lg.free:
            continue
        L['lots'] += 1
        L['lots_' + kind] += 1
        winner, price, bidders = run_lot(lg, lot, parts, yr)
        nai = sum(1 for cid in bidders if clubs[cid].ctrl == 'ai' and not clubs[cid].is_player)
        L['lot_ai_bidders'] += nai
        if winner is None:
            L['unsold'] += 1
            if seller is None:
                p.lock_year = yr
                p.carry = bool(P['carry_on'])
            continue
        w = clubs[winner]
        L['sold'] += 1
        L['sold_' + kind] += 1
        L['sold_price_' + kind] += price
        if kind in ('youth', 'carry'):
            L['sold_youth_pot'] += p.pot
            L['sold_youth_n'] += 1
            L['sold_youth_price'] += price
        w.funds -= price
        w.ys['xfer_out'] += price
        w.xs['bought'] += 1
        if seller is None:
            lg.free.remove(p)
            p.carry = False
            sign(lg, w, p, 3 if kind in ('youth', 'carry') else 2, 'auction')
        else:
            income_with_repay(seller, price)
            seller.ys['xfer_in'] += price
            remove_from(seller, p)
            p.obs = 0.0
            p.joined = yr + 0.5
            w.roster.append(p)
            prune_fam(seller)
            pick_lineup(lg, seller)
            if w.ctrl == 'ai' and seller.ctrl == 'ai' and not w.is_player and not seller.is_player:
                L['ai_ai_listed'] += 1
    for p in youth_pool:
        if p in lg.free and not p.carry:
            p.carry = False
    # ---- 7. re-sign
    for c in clubs:
        resign_fill(lg, c, yr)
        prune_fam(c)
        pick_lineup(lg, c)
        c.xs['wb_after'] = wage_total(c)
    if P['cap_after'] and len(lg.free) > P['pool_cap']:
        for p in sorted(lg.free, key=r_main)[:len(lg.free) - P['pool_cap']]:
            lg.free.remove(p)
            L['pool_cap_removed'] += 1
    L['n_players'] = sum(len(c.roster) for c in clubs) + len(lg.free)
    L['n_free'] = len(lg.free)
    L['n_signed'] = sum(len(c.roster) for c in clubs)
    L['spec_signed'] = sum(1 for c in clubs for p in c.roster if p.specs)
    L['spec_signed_train'] = sum(1 for c in clubs for p in c.roster if any(v == 'train' for v in p.spec_src.values()))


# ============================================================ league generation, warm-up, offers
def draft_score(c, p, have):
    bonus = 0.0
    if c.style in STYLE_PHASE:
        bonus = P['style_w'] * praw(p, STYLE_PHASE[c.style])
    elif c.style == 'star' and p.lane == c.star_lane and p.lane not in have:
        bonus = P['star_w'] * max(praw(p, 'E'), praw(p, 'L'))  # AMB-8
    return r_main(p) + bonus - P['age_pen'] * max(0, p.age - 26)


def gen_league(seed):
    lg = League(seed)
    rg = rng_for(seed, 'gen')
    deck = ['invade', 'invade', 'teamfight', 'teamfight', 'late', 'late', 'star', 'star']
    rg.shuffle(deck)
    for i in range(8):
        c = Club(i)
        c.style = deck[i]
        c.star_lane = rg.choice(LANES) if c.style == 'star' else None
        c.aggr, c.frugal, c.adapt = rg.random(), rg.random(), rg.random()
        c.off_rest = rg.uniform(-5, 5)
        c.off_cash = rg.uniform(-1, 1)
        c.off_renew = rg.uniform(-0.2, 0.2)
        c.off_bid = rg.uniform(-0.1, 0.1)
        c.funds = rg.uniform(400000, 1000000)
        c.pop = rg.uniform(20, 80)
        c.contracts = [[round(P['c0'] + P['c1'] * c.pop), rg.randint(1, 4)] for _ in range(2)]
        lg.clubs.append(c)
    mu = P['y0'] + P['y1'] * mean(c.pop for c in lg.clubs) / 100.0
    pool = [gen_player(lg, rg, lane, 17, 30, mu) for lane in LANES for _ in range(16)]
    order = list(range(8))
    rg.shuffle(order)
    sevens = set(rg.sample(range(8), 4))
    for rnd in range(7):
        seq = order if rnd % 2 == 0 else order[::-1]
        for ci in seq:
            if rnd == 6 and ci not in sevens:
                continue
            c = lg.clubs[ci]
            have = {p.lane for p in c.roster}
            cands = [p for p in pool if len(have) >= 5 or p.lane not in have]
            b = max(cands, key=lambda p: draft_score(c, p, have))
            pool.remove(b)
            yrs = rg.randint(1, 3)
            b.salary = float(round(sign_salary(lg, b, yrs, 'init') * P['open_sal']))
            b.contract = yrs
            b.joined = -1
            c.roster.append(b)
    lg.free = pool
    for c in lg.clubs:
        pick_lineup(lg, c)
        if c.style == 'star':
            sp = c.lineup[c.star_lane]
            c.star_pid = sp.id
            c.star_phase = 'E' if praw(sp, 'E') >= praw(sp, 'L') else 'L'
        st = starters(c)
        for i in range(5):
            for j in range(i + 1, 5):
                c.fam[pair_key(st[i], st[j])] = rg.uniform(40, 70)
    return lg


def make_offers(lg):
    pw = sorted(lg.clubs, key=lambda c: (-club_power(c)[0], c.id))
    r = rng_for(lg.seed, 'offers')
    oi = P['offer_idx']
    return {'strong': pw[r.choice(oi[0])].id, 'mid': pw[r.choice(oi[1])].id, 'weak': pw[r.choice(oi[2])].id,
            'ranks': [c.id for c in pw]}


def takeover(lg, c, segs=None):
    """league.md 预热与三份聘书 step 4."""
    c.debt = 0.0
    yr = lg.year
    for l in LANES:
        if not any(p.lane == l for p in c.roster) and len(c.roster) < 8:
            cands = [p for p in lg.free if p.lane == l]
            if cands:
                p = max(cands, key=r_main)
                lg.free.remove(p)
                sign(lg, c, p, 2, 'takeover')
    while len(c.roster) < 6 and lg.free:
        p = max(lg.free, key=r_main)
        lg.free.remove(p)
        sign(lg, c, p, 2, 'takeover')
    pick_lineup(lg, c)
    need = (segs or P['inject_segs']) * wseg(c) if P['inject_on'] else 0.0
    c.injected = max(0.0, need - c.funds)
    c.funds = max(c.funds, need)


# ============================================================ records
def club_year_record(lg, c):
    ys = c.ys
    pw, sd = club_power(c)
    fin = [pl for e, pl, w in c.year_events if e == 'finals']
    st = starters(c)
    ph_gain = []
    st_gain = []
    for p in c.roster:
        if p.snap and p.snap[3] == p.lane:
            ph_gain.append(mean(praw(p, k) for k in PH) - p.snap[2])
            if any(p is q for q in st):
                st_gain.append(mean(praw(p, k) for k in PH) - p.snap[2])
    others = [o for o in lg.clubs if o is not c]
    nonprize = {o.id: o.ys['income'] - o.ys['inc_prize'] for o in lg.clubs}
    rank_np = 1 + sum(1 for o in lg.clubs if nonprize[o.id] > nonprize[c.id])
    return dict(
        events=[(e, pl) for e, pl, w in c.year_events], fin=fin[0] if fin else 8,
        bankrupt=c.bankrupt, funds=c.funds, funds_start=ys['funds_start'], trace=list(c.fund_trace),
        inc=dict(sp=ys['inc_sp'], live=ys['inc_live'], biz=ys['inc_biz'], prize=ys['inc_prize'], ev=ys['inc_ev']),
        income=ys['income'], wages=ys['wages'], evx=ys['exp_ev'], xin=ys['xfer_in'], xout=ys['xfer_out'],
        wb_start=ys['wb_start'], wb_end=wage_total(c), loan=ys['loan'], fire=ys['fire_sales'], rescue=ys['rescue'],
        chem=ys['chem_sum'] / max(1, ys['segs']), mood=ys['mood_sum'] / max(1, ys['segs']),
        stress=ys['stress_sum'] / max(1, ys['segs']),
        ai_chem=mean(o.ys['chem_sum'] / max(1, o.ys['segs']) for o in others),
        ai_mood=mean(o.ys['mood_sum'] / max(1, o.ys['segs']) for o in others),
        power=pw, S=dict(sd.S), raw=mean(r_main(p) for p in st), ph_gain=mean(ph_gain) if ph_gain else 0.0,
        st_gain=mean(st_gain) if st_gain else 0.0, nspec_st=sum(len(p.specs) for p in st),
        nspec=sum(len(p.specs) for p in c.roster), nspec_train=sum(1 for p in c.roster for s in p.spec_src.values() if s == 'train'),
        tp=ys['train_pts'], rest=ys['rest_pts'], sp_att=ys['sp_att'], sp_succ=ys['sp_succ'], rank_np=rank_np,
        roster=len(c.roster), sat=ys['sal_sat'] / max(1, ys['sal_sat_n']),
        tE=ys['train_E'], tM=ys['train_M'], tL=ys['train_L'], tC=ys['train_C'], fat_segs=ys['fat_segs'],
        choices={k: v for k, v in ys.items() if k.startswith('choice_')},
    )


def league_year_record(lg):
    sides = {c.id: club_sides(c)[0] for c in lg.clubs}
    recs = []
    for c in lg.clubs:
        st = starters(c)
        recs.append(dict(id=c.id, style=c.style, label=style_label(lg, c, sides), power=mean(sides[c.id].S.values()),
                         raw=mean(r_main(p) for p in st), chem=c.ys['chem_sum'] / max(1, c.ys['segs']),
                         mood=c.ys['mood_sum'] / max(1, c.ys['segs']), debt=c.debt, loan=c.ys['loan'],
                         fin=[pl for e, pl, w in c.year_events if e == 'finals'][0], ctrl=c.ctrl, is_player=c.is_player,
                         nspec_players=sum(1 for p in c.roster if p.specs), roster=len(c.roster),
                         funds=c.funds, income=c.ys['income'], wages=c.ys['wages'], inc_prize=c.ys['inc_prize'],
                         sp=c.ys['inc_sp'], live=c.ys['inc_live'], biz=c.ys['inc_biz']))
    champ = [r for r in recs if r['fin'] == 1][0]
    return dict(clubs=recs, champ=champ['id'], champ_style=champ['style'], WI=lg.WI,
                n_players=sum(len(c.roster) for c in lg.clubs) + len(lg.free), n_free=len(lg.free),
                league_pop=mean(c.pop for c in lg.clubs),
                style_hist={c.id: list(c.style_hist) for c in lg.clubs},
                log={k: v for k, v in lg.log[lg.year].items() if isinstance(v, (int, float))})


BOTS = ('idle', 'ai', 'balanced', 'early', 'mid', 'late', 'star', 'business', 'counter', 'greedy',
        'prerest', 'contract', 'scout', 'kda', 'bidup', 'norest')
OFFERS = ('strong', 'mid', 'weak')


def build_specs(table):
    SPECS.clear()
    SPECS.update(copy.deepcopy(table))
    for l in LANES:
        LANE_SPECS[l] = []
    for sid, (lanes, ph, hk) in SPECS.items():
        for l in LANES:
            if lanes == '*':
                LANE_SPECS[l].append((sid, CMD, None))
            elif l in lanes:
                LANE_SPECS[l].append((sid, DIMS[l].index(lanes[l]), ph))
    SPEC_DIM.clear()
    for l in LANES:
        for sid, di, ph in LANE_SPECS[l]:
            if ph:
                SPEC_DIM.setdefault((l, ph), di)
    _HK.clear()


def apply_variant():
    if P['spec_table'] == 'G':
        t = copy.deepcopy(SPECS_G)
        for sid, v in (P['specG_flat'] or {}).items():
            k = t[sid][1] or 'M'
            t[sid][2]['flat'] = {k: v}
        build_specs(t)
    else:
        build_specs(SPECS_BASE)
        SPECS['jungle_king'][2]['flat']['E'] = P['jk_E']
        SPECS['late_king'][2]['flat']['L'] = P['lk_L']
    _HK.clear()


FIXA = dict(ai_sp_fill=True, ai_maint_stream=2, c0=8000.0, l_base=3200.0, tgt_a=3.0, tgt_b=0.0,
            ai_renew_mode='zero', ai_fill_mode='zero', kappa_s=0.03, offer_idx=((2, 3), (4, 5), (6, 7)),
            h=0.7, r_ratio=1.25, cap_after=True, t_max=0.10, scout_need_mode='median5',
            style_noreturn_years=2, style_prefer_rare=True, label_star_lead=15.0, stress_t_mode='thresh',
            unlock_v=76.0, lose_v=73.0, focus_redistribute=True, focus_counts=True, jk_E=1.8, lk_L=2.5,
            s_cyc=0.30, idle_sponsor=True)


FIXC = dict(FIXA, window_share=200000.0, ai_renew_age_test=True, k_th=2.3, style_noreturn=True, s_cyc=0.25, t_max=0.05,
            spend_reserve_W=2.0, reneg_autolist=True, ai_reneg_zero=True, cyc_nostar=True,
            offer_idx=((2, 3), (3, 4), (5, 6)), open_sal=0.85, ai_fire_min=6)


FIXD = dict(FIXC, window_share=0.0, c0=8800.0, l_base=3500.0, kappa_wi=0.25, poach_use_floor=True, ai_fire_min=5,
            poach_honor=True, n_lots_youth=2, n_lots_free=1, carry_on=False, r_ratio=1.2, n_youth=6, scout_cost=50000.0)


FIXE = dict(FIXD, ai_reneg_zero=False, c0=9500.0, final_prize_mult=1.5, bot_renew_zero=True, inject_weak=8.0,
            style_noreturn_star=True)


FIXF = dict(FIXE, fat_on=True, fat_stress=3.0, fat_biz=0.5, fat_ap=1.0, fat_split=0.5,
            c0=11000.0, sp_dur=5, l_base=5500.0, biz_mult=1.6)


FIXF2 = dict(FIXF, fat_stress=6.0, fat_biz=1.0, fat_ap=2.0, R_ref=0.914)
FIXG = dict(FIXF2, l_base=7500.0, biz_mult=2.0, poach_champ_first=True, n_youth=7, spec_table='G')


def run_seed(seed, bots=BOTS, offers=OFFERS, overrides=None, years=3, league_years=5, samples=True):
    if overrides:
        P.update(overrides)
    apply_variant()
    lg0 = gen_league(seed)
    run_year(lg0, 0)
    y0 = league_year_record(lg0)
    transfer_window(lg0, 0)
    y0['xlog'] = {k: v for k, v in lg0.log[0].items() if isinstance(v, (int, float))}
    offs = make_offers(lg0)
    out = dict(seed=seed, offers=offs, y0=y0, runs={}, allai=None, params={k: v for k, v in P.items()})
    if league_years:
        lgA = copy.deepcopy(lg0)
        lgA.samples = [] if samples else None
        recs = []
        for y in range(1, league_years + 1):
            run_year(lgA, y)
            r = league_year_record(lgA)
            transfer_window(lgA, y)
            if P['window_share']:
                for cc in r['clubs']:
                    cc['income'] += P['window_share']
                    cc['inc_prize'] += P['window_share']
            r['xlog'] = {k: v for k, v in lgA.log[y].items() if isinstance(v, (int, float))}
            recs.append(r)
        out['allai'] = dict(years=recs, samples=lgA.samples, champs=list(lgA.champs))
    for off in offers:
        cid = offs[off]
        for bot in bots:
            lg = copy.deepcopy(lg0)
            c = lg.clubs[cid]
            c.is_player = True
            c.ctrl = bot
            c.bot = bot
            takeover(lg, c, P['inject_weak'] if (off == 'weak' and P['inject_weak']) else None)
            yrs = []
            for y in range(1, years + 1):
                run_year(lg, y)
                rec = club_year_record(lg, c)
                rec['champ'] = [o.id for o in lg.clubs if [pl for e, pl, w in o.year_events if e == 'finals'][0] == 1][0]
                rec['ai_raw'] = mean(mean(r_main(p) for p in starters(o)) for o in lg.clubs if o is not c)
                transfer_window(lg, y)
                if P['window_share']:
                    rec['income'] += P['window_share']
                    rec['inc']['prize'] += P['window_share']
                rec['xs'] = dict(c.xs)
                rec['xlog'] = {k: v for k, v in lg.log[y].items() if isinstance(v, (int, float))}
                yrs.append(rec)
            out['runs'][(off, bot)] = dict(cid=cid, years=yrs, injected=getattr(c, 'injected', 0.0))
    return out


if __name__ == '__main__':
    t = time.time()
    s = int(sys.argv[1]) if len(sys.argv) > 1 else 1
    r = run_seed(s, bots=('ai', 'balanced', 'greedy'), offers=('mid',), league_years=2)
    print('time %.1fs' % (time.time() - t))
    for k, v in r['runs'].items():
        print(k, [(y['fin'], round(y['funds']), round(y['power'], 1), y['nspec'], y['bankrupt']) for y in v['years']])
    for y in r['allai']['years']:
        print('allai', y['champ'], y['champ_style'], round(y['WI'], 3), y['n_players'], y['n_free'],
              {k: y['log'].get(k, 0) for k in ('lots', 'sold', 'unsold', 'poach_offers', 'poach_acc', 'ai_ai_poach', 'ai_ai_listed', 'style_switch', 'loans', 'fire_sales')})
