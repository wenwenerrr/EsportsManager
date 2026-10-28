import sys, os, pickle, statistics, time, traceback, copy
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from multiprocessing import Pool
import sim5
SAVED = dict(sim5.P)


def work(args):
    seed, ov, bots, offers, ly = args
    sim5.P.clear(); sim5.P.update(SAVED)
    try:
        return sim5.run_seed(seed, bots=bots, offers=offers, overrides=dict(ov), league_years=ly)
    except Exception:
        return dict(seed=seed, error=traceback.format_exc())


def calibrate(ov):
    """dG of each table-G specialty on the match.md fixture (A invade vs B develop), fixG match settings; adjust flats to 0.6-0.9."""
    sim5.P.clear(); sim5.P.update(SAVED); sim5.P.update(ov)
    AE = [66, 70, 66, 64, 66]; AM = [60, 62, 60, 58, 62]; AL = [56, 56, 57, 56, 57]
    BE = [58, 60, 58, 56, 58]; BM = [62, 62, 63, 60, 62]; BL = [70, 68, 70, 72, 68]
    def side(rE, rM, rL, hk):
        return sim5.side_from([(rE[i], rM[i], rL[i], 70.0 if i == 0 else 0.0, 1.0, 1.0, 0.0) for i in range(5)], 70, hk)
    def hk_of(entry, flat_v=None):
        lanes, ph, h = entry
        k = ph or 'M'
        flat = {'E': 0.0, 'M': 0.0, 'L': 0.0}
        flat[k] = h['flat'][k] if flat_v is None else flat_v
        ss = {'E': 1.0, 'L': 1.0}
        for kk, v in h.get('ss', {}).items():
            ss[kk] = v
        return (flat, ss, h.get('exa', 0.0), h.get('sg', 1.0))
    A = side(AE, AM, AL, sim5.NEUTRAL_HK); B = side(BE, BM, BL, sim5.NEUTRAL_HK)
    G0 = sim5.gtac(sim5.core(A, B), A, B, 'invade', 'develop')
    def dG(entry, v=None):
        As = side(AE, AM, AL, hk_of(entry, v))
        return sim5.gtac(sim5.core(As, B), As, B, 'invade', 'develop') - G0
    final = {}
    rows = []
    for sid, entry in sim5.SPECS_G.items():
        k = entry[1] or 'M'
        v0 = entry[2]['flat'][k]
        g0 = dG(entry)
        v = v0
        if not (0.6 <= g0 <= 0.9):
            cands = [round(0.1 * i, 1) for i in range(0, 61)]
            ok = [x for x in cands if 0.62 <= dG(entry, x) <= 0.88]
            v = min(ok, key=lambda x: abs(x - v0)) if ok else v0
        final[sid] = v
        rows.append((sid, k, v0, g0, v, dG(entry, v)))
    return final, rows


if __name__ == '__main__':
    ov = dict(sim5.FIXG)
    final, rows = calibrate(ov)
    print('specialty calibration (fixture A invade vs B develop; s_cyc %.2f, star-free cycle):' % ov['s_cyc'])
    for sid, k, v0, g0, v, g1 in rows:
        print('  %-15s flat %s %+.1f -> dG %.3f%s' % (sid, k, v0, g0, ('' if v == v0 else '  => adjusted to %+.1f -> dG %.3f' % (v, g1))))
    ov['specG_flat'] = final
    t = time.time()
    with Pool(10) as pool:
        r1 = pool.map(work, [(1000 + i, dict(ov, kappa_wi=0.0), (), (), 5) for i in range(120)], chunksize=1)
        ok = [r for r in r1 if 'error' not in r]
        if len(ok) < len(r1):
            print([r for r in r1 if 'error' in r][0]['error'])
        Rs = [y['xlog']['R_league'] for r in ok for y in r['allai']['years']]
        iw = [c['income'] / max(1, c['wages']) for r in ok for y in r['allai']['years'][1:] for c in y['clubs']]
        R_ref = statistics.median(Rs)
        print('pass1 (WI frozen at 1.0): median league R %.3f (by year %s); median AI club income/wages Y2-5 %.3f; %.0fs' % (
            R_ref, ' '.join('%.3f' % statistics.median(r['allai']['years'][i]['xlog']['R_league'] for r in ok) for i in range(5)),
            statistics.median(iw), time.time() - t), flush=True)
        t = time.time()
        res = pool.map(work, [(1000 + i, dict(ov, R_ref=R_ref), sim5.BOTS, sim5.OFFERS, 5) for i in range(120)], chunksize=1)
        errs = [r for r in res if 'error' in r]
        pickle.dump(res, open('results5/abl_fixG.pkl', 'wb'))
        print('fixG errors', len(errs), '%.0fs' % (time.time() - t), flush=True)
        if errs:
            print(errs[0]['error'])
