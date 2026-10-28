import sys, os, pickle, collections, statistics
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import analyze5 as A5
from analyze5 import eff, bk_of, mean, pct, q
def L(name):
    return [r for r in pickle.load(open('results5/abl_%s.pkl' % name, 'rb')) if 'error' not in r]
cfg = [('base', A5.load('base')[:120]), ('fixF2', L('fixF2')), ('fixG', L('fixG'))]
rows = {n: A5.analyze(R, n, out=lambda *a: None, detail=False) for n, R in cfg}
names = [n for n, _ in cfg]
print('| id | item | target | ' + ' | '.join(names) + ' |')
print('|---|---|---|' + '---|' * len(names))
for i, row in enumerate(rows['base']):
    print('| %s | %s | %s | ' % (row[0], row[1], row[3]) + ' | '.join('%s **%s**' % (rows[n][i][2], rows[n][i][4]) for n in names) + ' |')
for n in names:
    v = [r[4] for r in rows[n]]
    print('%s: PASS %d FAIL %d' % (n, v.count('PASS'), v.count('FAIL')))
print()
for n, R in cfg:
    t = {b: pct(eff(r['runs'][(o, b)], yi)[1] for r in R for o in A5.OFFERS for yi in (1, 2, 3)) for b in A5.BOTS}
    dead = {}
    for o in A5.OFFERS:
        d = []
        for r in R:
            for yi in (1, 2, 3):
                rk = [eff(r['runs'][(o, b)], yi)[0] for b in A5.COMP7]
                d.append(max(rk) - min(rk) <= 1)
        dead[o] = pct(d)
    best = collections.Counter()
    for r in R:
        for o in A5.OFFERS:
            sc = {b: sum(eff(r['runs'][(o, b)], yi)[0] - 10 * eff(r['runs'][(o, b)], yi)[1] for yi in (1, 2, 3)) for b in A5.BOTS if b != 'norest'}
            m = min(sc.values()); w = [b for b in sc if sc[b] == m]
            for b in w: best[b] += 1.0 / len(w)
    tot = sum(best.values())
    A = [r['allai']['years'] for r in R]
    x = lambda k: [y['xlog'].get(k, 0.0) for yrs in A for y in yrs]
    print('%s: titles %s' % (n, ' '.join('%s %.1f' % (b, v) for b, v in t.items())))
    print('   dead seed-years strong/mid/weak: %s | best plan per seed-offer: %s' % ('/'.join('%.0f%%' % dead[o] for o in A5.OFFERS), ', '.join('%s %.0f%%' % (k, 100 * v / tot) for k, v in best.most_common(6))))
    print('   all-AI per window: auction entries %.1f, lots %.1f, sold %.1f, AI-bought %.2f, autolisted %.2f, reneg asked/agree %.1f/%.1f, poach offers/acc %.2f/%.2f, R_league %s, AI funds Y2 median %.1f wan' % (
        mean(x('choice_auction')), mean(x('lots')), mean(x('sold')), mean(x('lot_ai_bidders')) and (sum(x('sold')) - 0) / len(x('sold')), mean(x('autolisted')),
        mean(x('reneg_asked')), mean(x('reneg_agree')), mean(x('poach_offers')), mean(x('poach_acc')),
        ' '.join('%.2f' % mean(yrs[i]['xlog'].get('R_league', 0) for yrs in A) for i in range(5)), q([c['funds'] for yrs in A for c in yrs[1]['clubs']], .5) / 1e4))
    sold_kind = {k: mean(x('sold_' + k)) for k in ('youth', 'carry', 'free', 'listed')}
    print('   sold by kind/yr: %s; AI-AI listed %.2f poach %.2f' % (sold_kind, mean(x('ai_ai_listed')), mean(x('ai_ai_poach'))))
print('\n### Y3 sustainability')
for n, R in cfg:
    bk3 = {b: pct(bk_of(r['runs'][(o, b)]) is not None for r in R for o in A5.OFFERS) for b in A5.BOTS}
    bko = {o: {b: pct(bk_of(r['runs'][(o, b)]) is not None for r in R) for b in ('ai', 'balanced', 'greedy', 'late')} for o in A5.OFFERS}
    A = [r['allai']['years'] for r in R]
    loans = [pct(c['loan'] > 0 or c['debt'] > 0 for yrs in A for c in yrs[i]['clubs']) for i in range(5)]
    fm = [q([c['funds'] for yrs in A for c in yrs[i]['clubs']], .5) / 1e4 for i in range(5)]
    iw = [q([c['income'] / max(1, c['wages']) for yrs in A for c in yrs[i]['clubs']], .5) for i in range(5)]
    sens = [b for b in A5.SENS]
    worst = max(sens, key=lambda b: bk3[b])
    print('%-5s bankrupt by Y3: worst sensible %s %.1f%%, sensible median %.1f%%, business %.1f%%, idle %.1f%% | by offer (ai/balanced/greedy/late) %s' % (
        n, worst, bk3[worst], statistics.median(bk3[b] for b in sens), bk3['business'], bk3['idle'],
        ' '.join('%s %s' % (o[0], '/'.join('%.0f' % v for v in bko[o].values())) for o in A5.OFFERS)))
    print('      all-AI Y1-5: loan club-years %s %% | AI funds median %s wan | AI income/wages median %s' % (
        ' '.join('%.0f' % v for v in loans), ' '.join('%.1f' % v for v in fm), ' '.join('%.2f' % v for v in iw)))
print('\n### economy / WI / fatigue diagnostics')
for n, R in cfg:
    A = [r['allai']['years'] for r in R]
    wi = [statistics.median(yrs[i]['WI'] for yrs in A) for i in range(5)]
    Rl = [statistics.median(yrs[i]['xlog'].get('R_league', 0) for yrs in A) for i in range(5)]
    fs = []
    for r in R:
        for o in A5.OFFERS:
            for b in ('greedy', 'balanced', 'prerest'):
                ys = r['runs'][(o, b)]['years']
                for i in range(2):
                    if ys[i]['fin'] == 1:
                        fs.append((ys[i + 1]['stress'], ys[i + 1]['tp'], ys[i + 1].get('fat_segs', 0)))
    print('%-5s R_ref %.3f | WI median Y1-5 %s | league R median %s | champions next year: starter stress %.1f, training pts %.1f, fatigue segs %.1f (n=%d)' % (
        n, R[0].get('params', {}).get('R_ref', 1.0), ' '.join('%.2f' % v for v in wi), ' '.join('%.2f' % v for v in Rl),
        mean(x[0] for x in fs), mean(x[1] for x in fs), mean(x[2] for x in fs), len(fs)))
print('\n### GV3 per plan (player-club specialties, mean at end of Y1 / Y2 / Y3)')
for n, R in cfg:
    print('%-5s ' % n + ' | '.join('%s %s' % (b, '/'.join('%.2f' % mean(r['runs'][(o, b)]['years'][i]['nspec'] for r in R for o in A5.OFFERS) for i in range(3)))
                                  for b in ('early', 'mid', 'late', 'star', 'greedy', 'balanced', 'ai')))
print('\n### XV2 detail: player seed-years with >= 1 poach offer, by bot / champions; AI poach offers to the reigning champion')
for n, R in cfg:
    bys = {}
    for b in ('balanced', 'greedy', 'prerest', 'ai', 'star'):
        hit = []; ch = []
        for r in R:
            for o in A5.OFFERS:
                run = r['runs'][(o, b)]
                for yi in range(3):
                    y = run['years'][yi]
                    hit.append(y['xs'].get('poach_recv', 0) >= 1)
                    if y['fin'] == 1:
                        ch.append(y['xs'].get('poach_recv', 0) >= 1)
        bys[b] = (pct(hit), pct(ch), len(ch))
    print('%-5s %s; fixF2 R_ref param %s' % (n, ' | '.join('%s all %.0f%% champ %.0f%% (n=%d)' % (b, v[0], v[1], v[2]) for b, v in bys.items()), R[0].get('params', {}).get('R_ref')))
