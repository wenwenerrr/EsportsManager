"""Analysis of sim5 batches: every Balance Validation item of the six revised GDDs + creative-director targets."""
import sys, os, pickle, math, statistics, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

BOTS = ('idle', 'ai', 'balanced', 'early', 'mid', 'late', 'star', 'business', 'counter', 'greedy',
        'prerest', 'contract', 'scout', 'kda', 'bidup', 'norest')
COMP7 = ('balanced', 'early', 'mid', 'late', 'star', 'counter', 'greedy')
SENS = ('ai', 'balanced', 'early', 'mid', 'late', 'star', 'counter', 'greedy', 'prerest', 'contract', 'scout', 'kda', 'bidup')
OFFERS = ('strong', 'mid', 'weak')
CUT = {'spring': 2, 'summer': 5, 'finals': 11}


def load(name):
    R = pickle.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'results5', name + '.pkl'), 'rb'))
    return [r for r in R if 'error' not in r]


def mean(x):
    x = list(x)
    return sum(x) / len(x) if x else float('nan')


def pct(x):
    return 100.0 * mean(x)


def q(x, p):
    x = sorted(x)
    if not x:
        return float('nan')
    k = (len(x) - 1) * p
    f = math.floor(k)
    c = math.ceil(k)
    return x[f] + (x[c] - x[f]) * (k - f)


def bk_of(run):
    for y in run['years']:
        if y['bankrupt']:
            return y['bankrupt']
    return None


def eff(run, yi):
    """(finals rank, title) for played year yi (1-based); bankruptcy = game over."""
    bk = bk_of(run)
    y = run['years'][yi - 1]
    if bk is not None and bk[0] < yi:
        return 8, 0
    if bk is not None and bk[0] == yi:
        if bk[1] < 11:
            return 8, 0
    return y['fin'], int(y['fin'] == 1)


def spearman(a, b):
    n = len(a)
    ra = {v: i for i, v in enumerate(sorted(range(n), key=lambda i: a[i]))}
    rb = {v: i for i, v in enumerate(sorted(range(n), key=lambda i: b[i]))}
    d2 = sum((ra[i] - rb[i]) ** 2 for i in range(n))
    return 1 - 6 * d2 / (n * (n * n - 1))


def verdict(ok):
    return 'PASS' if ok else 'FAIL'


def analyze(R, label='base', out=print, detail=True):
    rows = []  # (id, item, measured, target, verdict)
    n = len(R)
    out('## %s: %d seeds x 3 offers x 3 played years (+ warm-up); all-AI league 5 years' % (label, n))
    # ------------------------------------------------------------ per-bot outcomes
    T = {}
    for b in BOTS:
        t = {o: {1: [], 2: [], 3: []} for o in OFFERS}
        rk = {o: {1: [], 2: [], 3: []} for o in OFFERS}
        bk2 = {o: [] for o in OFFERS}
        bk3 = {o: [] for o in OFFERS}
        for r in R:
            for o in OFFERS:
                run = r['runs'].get((o, b))
                if run is None:
                    continue
                for yi in (1, 2, 3):
                    rr, tt = eff(run, yi)
                    t[o][yi].append(tt)
                    rk[o][yi].append(rr)
                bk = bk_of(run)
                bk2[o].append(bk is not None and bk[0] <= 2)
                bk3[o].append(bk is not None)
        T[b] = (t, rk, bk2, bk3)
    if detail:
        out('\n### Per-bot outcomes (pooled over offers; title = annual finals champion; bankruptcy = game over)')
        out('| bot | title% Y1-3 | Y1 title% strong/mid/weak | mean finals rank | top-4% | bankrupt by Y2 | by Y3 |')
        out('|---|---|---|---|---|---|---|')
    tr_all = {}
    for b in BOTS:
        t, rk, bk2, bk3 = T[b]
        allt = [x for o in OFFERS for yi in (1, 2, 3) for x in t[o][yi]]
        allr = [x for o in OFFERS for yi in (1, 2, 3) for x in rk[o][yi]]
        tr_all[b] = pct(allt)
        if detail:
            out('| %s | %.1f | %s | %.2f | %.0f | %.1f%% | %.1f%% |' % (
                b, tr_all[b], '/'.join('%.1f' % pct(t[o][1]) for o in OFFERS), mean(allr), pct(x <= 4 for x in allr),
                pct([x for o in OFFERS for x in bk2[o]]), pct([x for o in OFFERS for x in bk3[o]])))
    # ------------------------------------------------------------ BV4 / creative-director
    play_bots = [b for b in BOTS if b != 'norest']
    mx = max(play_bots, key=lambda b: tr_all[b])
    viable = [b for b in play_bots if tr_all[b] >= 5]
    rows.append(('BV4a / CD', 'max annual title rate of any bot', '%s %.1f%%' % (mx, tr_all[mx]), '<= 35%', verdict(tr_all[mx] <= 35)))
    rows.append(('BV4b / CD', 'bots with >= 5% titles', '%d (%s)' % (len(viable), ','.join(viable)), '>= 4', verdict(len(viable) >= 4)))
    edge = tr_all['prerest'] - tr_all['balanced']
    rows.append(('BV4c / CD', 'prerest title edge over balanced', '%+.1f pp (%.1f vs %.1f)' % (edge, tr_all['prerest'], tr_all['balanced']), '<= +5 pp', verdict(edge <= 5)))
    # LV9 per offer
    for o in OFFERS:
        y1 = {b: pct(T[b][0][o][1]) for b in play_bots}
        bmax = max(y1, key=y1.get)
        sens = {b: y1[b] for b in SENS}
        if o == 'strong':
            rows.append(('LV9 / CD', 'strong offer: max Y1 title rate over bots', '%s %.1f%% (sensible median %.1f%%)' % (bmax, y1[bmax], statistics.median(sens.values())), '<= 35%', verdict(y1[bmax] <= 35)))
        if o == 'weak':
            rows.append(('LV9 / CD', 'weak offer: best-bot Y1 title rate', '%s %.1f%% (sensible median %.1f%%)' % (bmax, y1[bmax], statistics.median(sens.values())), '>= 3%', verdict(y1[bmax] >= 3)))
        if o == 'mid':
            rows.append(('LV9 (info)', 'mid offer: max Y1 title rate', '%s %.1f%%' % (bmax, y1[bmax]), 'record', 'INFO'))
    # bankruptcy
    bkr = {b: pct([x for o in OFFERS for x in T[b][2][o]]) for b in BOTS}
    bkr3 = {b: pct([x for o in OFFERS for x in T[b][3][o]]) for b in BOTS}
    worst = max(SENS, key=lambda b: bkr[b])
    rows.append(('EV4 / CD', 'sensible bots bankrupt within 2 yrs (worst)', '%s %.1f%% (by Y3 worst %s %.1f%%)' % (worst, bkr[worst], max(SENS, key=lambda b: bkr3[b]), max(bkr3[b] for b in SENS)), '< 10%', verdict(bkr[worst] < 10)))
    # ------------------------------------------------------------ fun signal: dead seed-years
    dead = []
    spread = []
    for r in R:
        for o in OFFERS:
            for yi in (1, 2, 3):
                ranks = [eff(r['runs'][(o, b)], yi)[0] for b in COMP7]
                spread.append(max(ranks) - min(ranks))
                dead.append(max(ranks) - min(ranks) <= 1)
    rows.append(('CD fun', 'seed-years where the 7 competitive plans finish within one rank', '%.1f%% (median spread %.1f)' % (pct(dead), q(spread, .5)), 'well below 54-77%', verdict(pct(dead) < 40)))
    # best strategy per seed-offer
    best = collections.Counter()
    for r in R:
        for o in OFFERS:
            sc = {}
            for b in play_bots:
                run = r['runs'][(o, b)]
                ys = [eff(run, yi) for yi in (1, 2, 3)]
                sc[b] = sum(rk for rk, _ in ys) - 10 * sum(t for _, t in ys)
            m = min(sc.values())
            w = [b for b in sc if sc[b] == m]
            for b in w:
                best[b] += 1.0 / len(w)
    tot = sum(best.values())
    # ------------------------------------------------------------ repeat champions
    rep = {}
    for b in SENS + ('idle', 'business'):
        num = den = 0
        for r in R:
            for o in OFFERS:
                run = r['runs'][(o, b)]
                for yi in (1, 2):
                    a = eff(run, yi)[1]
                    c = eff(run, yi + 1)[1]
                    if a:
                        den += 1
                        num += c
        rep[b] = (num / den if den else float('nan'), den)
    rb = max([b for b in rep if rep[b][1] >= 10], key=lambda b: rep[b][0])
    rows.append(('LV3a / CD', 'same fixed strategy wins twice in a row: P(title t+1 | title t), worst bot', '%s %.0f%% (n=%d)' % (rb, 100 * rep[rb][0], rep[rb][1]), '<= 30%', verdict(rep[rb][0] <= 0.30)))
    repA = []
    for r in R:
        ch = [c[1] for c in r['allai']['champs']]
        for i in range(len(ch) - 1):
            repA.append(ch[i] == ch[i + 1])
    rows.append(('LV3b', 'all-AI league repeat champion (same club next year)', '%.1f%%' % pct(repA), '<= 35%', verdict(pct(repA) <= 35)))
    # ------------------------------------------------------------ chemistry / mood gap (LV2)
    gaps = {}
    for b in SENS:
        cg = []
        mg = []
        for r in R:
            for o in OFFERS:
                run = r['runs'][(o, b)]
                if bk_of(run):
                    continue
                y = run['years'][1]
                cg.append(y['chem'] - y['ai_chem'])
                mg.append(y['mood'] - y['ai_mood'])
        gaps[b] = (mean(cg), mean(mg))
    wc = max(gaps, key=lambda b: abs(gaps[b][0]))
    wm = max(gaps, key=lambda b: abs(gaps[b][1]))
    rows.append(('LV2 / CD', 'Y2 player-vs-AI starters chemistry gap (worst sensible bot)', '%s %+.1f' % (wc, gaps[wc][0]), '|gap| <= 10', verdict(abs(gaps[wc][0]) <= 10)))
    rows.append(('LV2', 'Y2 player-vs-AI mood gap (worst sensible bot)', '%s %+.1f' % (wm, gaps[wm][1]), '|gap| <= 10', verdict(abs(gaps[wm][1]) <= 10)))
    # ------------------------------------------------------------ early vs late in the full system (all-AI samples)
    def lean(S):
        m = sum(S) / 3
        d = [s - m for s in S]
        k = max(range(3), key=lambda i: d[i])
        return k, d[k]
    PI = {'E': 0, 'M': 1, 'L': 2}
    pairs = {('E', 'L'): [], ('M', 'E'): [], ('L', 'M'): []}
    allv = []
    for r in R:
        for SA, SB, v, sw in r['allai']['samples']:
            allv.append((SA, SB, v))
            ka, la = lean(SA)
            kb, lb = lean(SB)
            if abs(sum(SA) / 3 - sum(SB) / 3) > 1.5 or la < 2 or lb < 2:
                continue
            for (x, y_) in pairs:
                if ka == PI[x] and kb == PI[y_]:
                    pairs[(x, y_)].append(v)
                elif kb == PI[x] and ka == PI[y_]:
                    pairs[(x, y_)].append(1 - v)
    pv = {k: (mean(v), len(v)) for k, v in pairs.items()}
    okc = all(0.45 <= v[0] <= 0.55 for v in pv.values() if v[1] >= 30)
    rows.append(('CD cycle', 'full-system X-lean vs Y-lean (X beats Y, |strength diff|<=1.5, lean>=2): eq p of X side',
                 ' / '.join('%s>%s %.3f (n=%d)' % (k[0], k[1], v[0], v[1]) for k, v in pv.items()), '0.45-0.55 each', verdict(okc)))
    # lean distribution
    leans = [lean(SA)[1] for SA, SB, v in allv]
    # ------------------------------------------------------------ poaching (XV2)
    def poach_share(bots, champs_only=False):
        hit = []
        for r in R:
            for o in OFFERS:
                for b in bots:
                    run = r['runs'][(o, b)]
                    bk = bk_of(run)
                    for yi in (1, 2, 3):
                        if bk is not None and bk[0] <= yi:
                            break
                        y = run['years'][yi - 1]
                        if champs_only and y['fin'] != 1:
                            continue
                        hit.append(y['xs'].get('poach_recv', 0) >= 1)
        return pct(hit), len(hit)
    ps, nps = poach_share(SENS)
    psc, npsc = poach_share(SENS, True)
    rows.append(('XV2 / CD', 'player seed-years with >= 1 poach offer (sensible bots pooled)', '%.1f%% (n=%d)' % (ps, nps), '>= 50%', verdict(ps >= 50)))
    rows.append(('XV2', 'champion seed-years with >= 1 poach offer', '%.1f%% (n=%d)' % (psc, npsc), '>= 80%', verdict(psc >= 80)))
    # ------------------------------------------------------------ all-AI league dynamics
    A = [r['allai']['years'] for r in R]
    x = lambda key: [y['xlog'].get(key, 0.0) for yrs in A for y in yrs]
    aiai = [y['xlog'].get('ai_ai_poach', 0) + y['xlog'].get('ai_ai_listed', 0) for yrs in A for y in yrs]
    rows.append(('XV3 / CD', 'AI-AI deals (listed + poach) per window', 'mean %.2f (poach %.2f, listed %.2f)' % (mean(aiai), mean(x('ai_ai_poach')), mean(x('ai_ai_listed'))), '>= 3', verdict(mean(aiai) >= 3)))
    lots = sum(x('lots'))
    uns = sum(x('unsold'))
    bid = sum(x('lot_ai_bidders'))
    rows.append(('XV4a', 'auction unsold rate (all-AI league)', '%.1f%% of %.1f lots/yr' % (100 * uns / max(1, lots), lots / max(1, len(x('lots')))), '<= 40%', verdict(uns / max(1, lots) <= 0.40)))
    rows.append(('XV4b / LV7b', 'AI bidders per lot', '%.2f' % (bid / max(1, lots)), '>= 1.5', verdict(bid / max(1, lots) >= 1.5)))
    poa = x('poach_acc')
    poo = x('poach_offers')
    rows.append(('LV7c', 'AI poach offers per year (all-AI league)', 'offers %.2f, accepted %.2f; years with >= 1 offer %.0f%%' % (mean(poo), mean(poa), pct(v >= 1 for v in poo)), '>= 1 / yr', verdict(mean(poo) >= 1)))
    loans = [c['loan'] > 0 or c['debt'] > 0 for yrs in A for y in yrs for c in y['clubs']]
    rows.append(('LV7a', 'AI club-years with a league loan / debt', '%.1f%%' % pct(loans), '<= 10%', verdict(pct(loans) <= 10)))
    # LV8 AI rating trend & rank correlation
    raw_y = [[mean(c['raw'] for c in r['y0']['clubs'])] + [mean(c['raw'] for c in y['clubs']) for y in yrs] for r, yrs in zip(R, A)]
    trend = [mean(v[i] for v in raw_y) for i in range(6)]
    dec_share = pct(raw_y[s][i + 1] < raw_y[s][i] for s in range(len(raw_y)) for i in range(5))
    rows.append(('LV8a / CD', 'AI starters mean R_main by year Y0..Y5 (all-AI league)', ' '.join('%.1f' % t for t in trend) + ' (seed-years declining %.0f%%)' % dec_share, 'non-decreasing', verdict(all(trend[i + 1] >= trend[i] - 1e-9 for i in range(5)))))
    cors = []
    corf = []
    for r, yrs in zip(R, A):
        seq = [r['y0']] + yrs
        for i in range(len(seq) - 1):
            a = [c['power'] for c in seq[i]['clubs']]
            b_ = [c['power'] for c in seq[i + 1]['clubs']]
            cors.append(spearman(a, b_))
            corf.append(spearman([-c['fin'] for c in seq[i]['clubs']], [-c['fin'] for c in seq[i + 1]['clubs']]))
    rows.append(('LV8b', 'year-to-year rank correlation (strength / finals place)', '%.2f / %.2f' % (mean(cors), mean(corf)), '0.3-0.7', verdict(0.3 <= mean(cors) <= 0.7)))
    # LV4 champion style
    st_cnt = collections.Counter()
    ch_cnt = collections.Counter()
    for r, yrs in zip(R, A):
        for y in yrs:
            for c in y['clubs']:
                st_cnt[c['style']] += 1
        for yr_, cid, sty, who in r['allai']['champs']:
            ch_cnt[sty] += 1
    ratio = {s: (ch_cnt[s] / sum(ch_cnt.values())) / (st_cnt[s] / sum(st_cnt.values())) for s in st_cnt}
    ws = max(ratio, key=ratio.get)
    rows.append(('LV4', 'AI champion style / style frequency (max)', '%s %.2f (%s)' % (ws, ratio[ws], ', '.join('%s %.2f' % (k, v) for k, v in sorted(ratio.items()))), '<= 1.6', verdict(ratio[ws] <= 1.6)))
    # LV5 four styles present
    four = [len({c['style'] for c in y['clubs']}) == 4 for yrs in A for y in yrs]
    four_lab = [len({c['label'] for c in y['clubs']}) == 4 for yrs in A for y in yrs]
    labs = collections.Counter(c['label'] for yrs in A for y in yrs for c in y['clubs'])
    rows.append(('LV5', 'year-ends with all four styles present (declared style / visible label)', '%.0f%% / %.0f%% (labels: %s)' % (pct(four), pct(four_lab), dict(labs.most_common())), '>= 80%', verdict(pct(four) >= 80)))
    # LV6 switches
    sw_per = []
    aba = 0
    nsw = 0
    for r, yrs in zip(R, A):
        hist = yrs[-1]['style_hist']
        for cid, h in hist.items():
            sw_per.append(len([e for e in h if e[0] >= 1]) / 5.0)
            for i in range(len(h) - 1):
                nsw += 1
                if h[i + 1][2] == h[i][1]:
                    aba += 1
    rows.append(('LV6', 'AI style switches per club-year; A->B->A share', '%.2f; %.0f%%' % (mean(sw_per), 100 * aba / max(1, nsw)), '0.1-0.5; <= 10%', verdict(0.1 <= mean(sw_per) <= 0.5 and aba / max(1, nsw) <= 0.10)))
    # LV1 idle
    idle_r2 = [eff(r['runs'][(o, 'idle')], 2)[0] for r in R for o in OFFERS]
    idle_raw = [r['runs'][(o, 'idle')]['years'][1]['fin'] for r in R for o in OFFERS]
    idle_bk = pct(bk_of(r['runs'][(o, 'idle')]) is not None and bk_of(r['runs'][(o, 'idle')])[0] <= 2 for r in R for o in OFFERS)
    idle_y1 = [eff(r['runs'][(o, 'idle')], 1)[0] for r in R for o in OFFERS]
    idle_ok = pct(eff(r['runs'][(o, 'idle')], 1)[0] >= 6 or bk_of(r['runs'][(o, 'idle')]) is not None for r in R for o in OFFERS)
    rows.append(('LV1 (restated)', 'idle: Y1 mean rank (bankrupt=8); seed-offers with Y1 rank >= 6 or bankrupt; bankrupt by Y2', '%.2f; %.0f%%; %.0f%%' % (mean(idle_y1), idle_ok, idle_bk), 'Y1 rank >= 6 or bankrupt', verdict(mean(idle_y1) >= 6)))
    # ------------------------------------------------------------ growth
    def mean_rank3(run):
        return mean(eff(run, yi)[0] for yi in (1, 2, 3))
    gv2 = {}
    for sb in ('early', 'mid', 'late', 'star', 'balanced'):
        beat = []
        for r in R:
            for o in OFFERS:
                a = mean_rank3(r['runs'][(o, sb)])
                z = mean_rank3(r['runs'][(o, 'idle')])
                if a != z:
                    beat.append(a < z)
        gv2[sb] = pct(beat)
    vs = {}
    for sb in ('early', 'mid', 'late', 'star'):
        better = worse = 0
        for r in R:
            for o in OFFERS:
                a = mean_rank3(r['runs'][(o, sb)])
                z = mean_rank3(r['runs'][(o, 'balanced')])
                if a < z:
                    better += 1
                elif a > z:
                    worse += 1
        vs[sb] = (100 * better / max(1, better + worse), 100 * worse / max(1, better + worse))
    ok2 = all(v >= 70 for v in gv2.values()) and all(min(v) >= 25 for v in vs.values())
    rows.append(('GV2', 'specialists & balanced beat idle (per seed-offer, mean rank Y1-3); specialist vs balanced each better',
                 'beat idle: ' + ', '.join('%s %.0f%%' % (k, v) for k, v in gv2.items()) + '; vs balanced (spec better/worse): ' +
                 ', '.join('%s %.0f/%.0f' % (k, v[0], v[1]) for k, v in vs.items()), '>= 70%; each >= 25%', verdict(ok2)))
    ns = [r['runs'][(o, 'balanced')]['years'][0]['nspec'] for r in R for o in OFFERS]
    sig = [y['xlog'].get('spec_signed', 0) / max(1, y['xlog'].get('n_signed', 1)) for yrs in A for y in yrs[:1]]
    sigall = [y['xlog'].get('spec_signed', 0) / max(1, y['xlog'].get('n_signed', 1)) for yrs in A for y in yrs]
    trn = sum(y['xlog'].get('spec_signed_train', 0) for yrs in A for y in yrs) / max(1, sum(y['xlog'].get('spec_signed', 0) for yrs in A for y in yrs))
    gb = {b: mean(r['runs'][(o, b)]['years'][0]['nspec'] for r in R for o in OFFERS) for b in ('early', 'mid', 'late', 'star', 'greedy')}
    rows.append(('GV3 (restated)', 'specialties end Y1: early/mid/late/star/greedy (balanced record); league signed share Y1 / Y1-5; from training',
                 '%s (bal %.2f); %.1f%% / %.1f%%; %.0f%%' % ('/'.join('%.2f' % v for v in gb.values()), mean(ns), 100 * mean(sig), 100 * mean(sigall), 100 * trn), 'each 1-3; 10-25%; >= 50%',
                 verdict(all(1 <= v <= 3 for v in gb.values()) and 0.10 <= mean(sig) <= 0.25 and trn >= 0.5)))
    g_bal = [r['runs'][(o, 'balanced')]['years'][0]['ph_gain'] for r in R for o in OFFERS]
    g_nor = [r['runs'][(o, 'norest')]['years'][0]['ph_gain'] for r in R for o in OFFERS]
    s_nor = [r['runs'][(o, 'norest')]['years'][0]['stress'] for r in R for o in OFFERS]
    s_bal = [r['runs'][(o, 'balanced')]['years'][0]['stress'] for r in R for o in OFFERS]
    red = 1 - mean(g_nor) / mean(g_bal)
    rows.append(('GV4', 'no-rest (high stress) vs resting team: Y1 roster phase-rating growth', 'norest %.2f vs balanced %.2f (-%.0f%%); avg starter stress %.0f vs %.0f' % (mean(g_nor), mean(g_bal), 100 * red, mean(s_nor), mean(s_bal)), '>= 30% less', verdict(red >= 0.30)))
    # ------------------------------------------------------------ economy
    bus = [(r, o) for r in R for o in OFFERS]
    bus_bk = pct(bk_of(r['runs'][(o, 'business')]) is not None for r, o in bus)
    bus_np1 = pct(y['rank_np'] <= 2 for r, o in bus for y in r['runs'][(o, 'business')]['years'])
    bus_r2 = pct(eff(r['runs'][(o, 'business')], 2)[0] >= 5 for r, o in bus)
    rows.append(('EV1 (restated)', 'business bot: bankrupt; non-prize income top-2 (seed-years); Y2 finals rank 5-8', '%.1f%%; %.0f%%; %.0f%%' % (bus_bk, bus_np1, bus_r2), '0; >= 80%; >= 80%', verdict(bus_bk == 0 and bus_np1 >= 80 and bus_r2 >= 80)))
    mono = {}
    for b in play_bots:
        hit = 0
        tot_ = 0
        for r in R:
            for o in OFFERS:
                run = r['runs'][(o, b)]
                tr = run['years'][0]['trace'] + run['years'][1]['trace']
                title = eff(run, 1)[1] or eff(run, 2)[1]
                tot_ += 1
                if title and len(tr) >= 24 and all(tr[i + 1] >= tr[i] for i in range(len(tr) - 1)):
                    hit += 1
        mono[b] = 100.0 * hit / max(1, tot_)
    wm_ = max(mono, key=mono.get)
    rows.append(('EV2a', '"funds never fall over 24 segments AND >= 1 title" (worst bot)', '%s %.1f%%' % (wm_, mono[wm_]), '<= 5%', verdict(mono[wm_] <= 5)))
    wr = []
    for r, yrs in zip(R, A):
        ch1 = [c for c in yrs[0]['clubs'] if c['fin'] == 1][0]
        c2 = [c for c in yrs[1]['clubs'] if c['id'] == ch1['id']][0]
        wr.append(c2['wages'] / max(1, ch1['wages']))
    wrp = []
    for b in SENS:
        for r in R:
            for o in OFFERS:
                run = r['runs'][(o, b)]
                if eff(run, 1)[1] and not bk_of(run):
                    wrp.append(run['years'][1]['wages'] / max(1, run['years'][0]['wages']))
    rows.append(('EV2b / XV1', 'Y1 champion: Y2 wages paid / Y1 (median; all-AI league / player bots)', '%.2f / %.2f (n=%d)' % (q(wr, .5), q(wrp, .5), len(wrp)), '>= 1.20', verdict(q(wr, .5) >= 1.2 and q(wrp, .5) >= 1.2)))
    for b in ('balanced', 'ai'):
        iw = [r['runs'][(o, b)]['years'][1]['income'] / max(1, r['runs'][(o, b)]['years'][1]['wages']) for r in R for o in OFFERS if not bk_of(r['runs'][(o, b)])]
        mix = collections.defaultdict(list)
        for r in R:
            for o in OFFERS:
                y = r['runs'][(o, b)]['years'][1]
                inc = max(1.0, sum(y['inc'].values()))
                for k in ('sp', 'live', 'biz', 'prize', 'ev'):
                    mix[k].append(100 * y['inc'][k] / inc)
        tgt = {'sp': 40, 'live': 25, 'biz': 20, 'prize': 15}
        okm = all(abs(mean(mix[k]) - tgt[k]) <= 8 for k in tgt)
        rows.append(('EV3 (%s)' % b, 'Y2 income/wages median; income mix sp/live/biz/prize/ev %', '%.2f; %s' % (q(iw, .5), '/'.join('%.0f' % mean(mix[k]) for k in ('sp', 'live', 'biz', 'prize', 'ev'))),
                     '0.9-1.1; 40/25/20/15 +-8', verdict(0.9 <= q(iw, .5) <= 1.1 and okm)))
    wi = [[y['WI'] for y in yrs] for yrs in A]
    allwi = [v for w in wi for v in w]
    inr = pct(all(0.85 <= v <= 1.2 for v in w) for w in wi)
    rows.append(('EV5', 'WI over 5 years (all-AI league): min/median/max; seeds always in range', '%.2f/%.2f/%.2f; %.0f%%' % (min(allwi), q(allwi, .5), max(allwi), inr), '0.85-1.2', verdict(inr >= 80)))
    # ------------------------------------------------------------ talent
    npl = [y['xlog'].get('n_players', 0) for yrs in A for y in yrs]
    nfr = [y['xlog'].get('n_free', 0) for yrs in A for y in yrs]
    rows.append(('TV1', 'league players (signed+free) after each window, 5 yrs: min/median/max; free pool max', '%d/%d/%d; %d' % (min(npl), q(npl, .5), max(npl), max(nfr)), '80-95; <= 35', verdict(min(npl) >= 80 and max(npl) <= 95 and max(nfr) <= 35)))
    sp_ = sum(x('scout_pot_sum')) / max(1, sum(x('scouted')))
    yp = sum(x('sold_youth_pot')) / max(1, sum(x('sold_youth_n')))
    ypr = sum(x('sold_youth_price')) / max(1, sum(x('sold_youth_n')))
    # include player-bot runs for auction youth if AI never buys
    ypp = []
    ypr2 = []
    for r in R:
        for o in OFFERS:
            for b in BOTS:
                for y in r['runs'][(o, b)]['years']:
                    xl = y['xlog']
                    if xl.get('sold_youth_n', 0):
                        ypp.append(xl['sold_youth_pot'] / xl['sold_youth_n'])
                        ypr2.append(xl['sold_youth_price'] / xl['sold_youth_n'])
    rows.append(('TV2', 'scouted mean true pot vs auctioned youth (all-AI / all runs); scout cost / youth price',
                 'scout %.1f vs youth %.1f / %.1f; cost/price %s' % (sp_, yp if sum(x('sold_youth_n')) else float('nan'), mean(ypp),
                                                                       ('%.2f' % (R[0].get('params', {}).get('scout_cost', 80000.0) / mean(ypr2))) if ypr2 else 'n/a (no sales)'),
                 '>= 5 lower; <= 0.3', verdict(bool(ypr2) and mean(ypp) - sp_ >= 5 and R[0].get('params', {}).get('scout_cost', 80000.0) / mean(ypr2) <= 0.3)))
    lpop = [y['league_pop'] for yrs in A for y in yrs]
    rows.append(('TV3', 'league popularity range (record)', '%.1f-%.1f (median %.1f)' % (min(lpop), max(lpop), q(lpop, .5)), 'record', 'INFO'))
    # ------------------------------------------------------------ print
    out('\n### Balance Validation and creative-director targets')
    out('| id | item | measured | target | verdict |')
    out('|---|---|---|---|---|')
    for row in rows:
        out('| %s | %s | %s | %s | %s |' % row)
    if detail:
        out('\nBest bot per seed-offer (score = ranks - 10/title over 3 yrs, ties split): ' +
            ', '.join('%s %.0f%%' % (k, 100 * v / tot) for k, v in best.most_common()))
        out('Repeat-title rate by bot: ' + ', '.join('%s %.0f%% (n=%d)' % (b, 100 * v[0], v[1]) for b, v in rep.items() if v[1]))
        out('Chem/mood gaps by bot (Y2, player - AI): ' + ', '.join('%s %+.1f/%+.1f' % (b, v[0], v[1]) for b, v in gaps.items()))
        out('Full-system |lean| (S_k - mean S, max): median %.1f, p90 %.1f' % (q(leans, .5), q(leans, .9)))
    return rows


def diag(R, out=print):
    """diagnostics for structural explanations"""
    A = [r['allai']['years'] for r in R]
    out('\n### Diagnostics (all-AI league unless stated)')
    for i in range(5):
        cl = [c for yrs in A for c in yrs[i]['clubs']]
        out('Y%d: AI income/wages median %.2f; funds median %.1f wan; roster mean %.2f; R_league %.2f; WI %.2f; choices auction/scout/sell/skip %s; released %.1f renewed %.1f resigned_forced %.1f; fire sales %.1f; retired %.1f; reneg asked/agree %.1f/%.1f' % (
            i + 1, q([c['income'] / max(1, c['wages']) for c in cl], .5), q([c['funds'] for c in cl], .5) / 1e4, mean(c['roster'] for c in cl),
            mean(yrs[i]['xlog'].get('R_league', 0) for yrs in A), mean(yrs[i]['WI'] for yrs in A),
            '/'.join('%.1f' % mean(yrs[i]['xlog'].get('choice_' + k, 0) for yrs in A) for k in ('auction', 'scout', 'sell', 'skip')),
            mean(yrs[i]['xlog'].get('released', 0) for yrs in A), mean(yrs[i]['xlog'].get('renewed', 0) for yrs in A),
            mean(yrs[i]['xlog'].get('resigned_forced', 0) for yrs in A), mean(yrs[i]['log'].get('fire_sales', 0) for yrs in A),
            mean(yrs[i]['xlog'].get('retired_signed', 0) + yrs[i]['xlog'].get('retired_free', 0) for yrs in A),
            mean(yrs[i]['xlog'].get('reneg_asked', 0) for yrs in A), mean(yrs[i]['xlog'].get('reneg_agree', 0) for yrs in A)))
    y0 = [c for r in R for c in r['y0']['clubs']]
    out('Warm-up Y0: AI income/wages median %.2f; funds median %.1f wan; prize share %.0f%%' % (
        q([c['income'] / max(1, c['wages']) for c in y0], .5), q([c['funds'] for c in y0], .5) / 1e4,
        100 * sum(c['inc_prize'] for c in y0) / sum(c['income'] for c in y0)))
    mix = collections.defaultdict(float)
    for yrs in A:
        for c in yrs[0]['clubs']:
            for k in ('sp', 'live', 'biz', 'inc_prize'):
                mix[k] += c[k]
    tot = sum(mix.values())
    out('All-AI Y1 income mix sp/live/biz/prize: ' + '/'.join('%.0f%%' % (100 * mix[k] / tot) for k in ('sp', 'live', 'biz', 'inc_prize')))
    chase = sum(y['log'].get('chase', 0) for yrs in A for y in yrs)
    fut = sum(y['log'].get('chase_futile', 0) for yrs in A for y in yrs)
    out('AI specialty chases: %.0f per league-year, futile (cap < 80) %.1f%%' % (chase / (len(A) * 5), 100 * fut / max(1, chase)))


if __name__ == '__main__':
    name = sys.argv[1] if len(sys.argv) > 1 else 'base'
    R = load(name)
    analyze(R, name)
    diag(R)
