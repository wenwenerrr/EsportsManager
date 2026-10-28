"""Run sim5 ablations (each: same seeds 1000.., subset of bots). Defaults untouched except the listed knobs."""
import sys, os, pickle, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from multiprocessing import Pool
import sim5

ECO = ('ai', 'balanced', 'greedy', 'prerest', 'business', 'idle')
ABL = {
    # name: (overrides, bots, offers, league_years)
    'ref':          ({}, ECO + ('norest', 'star'), sim5.OFFERS, 5),
    'econ_tgt':     (dict(tgt_a=2.0, tgt_b=2.0), ECO, sim5.OFFERS, 5),
    'econ_stream':  (dict(ai_maint_stream=2), ECO, sim5.OFFERS, 5),
    'renew0':       (dict(ai_renew_mode='zero', ai_fill_mode='zero'), ECO, sim5.OFFERS, 5),
    'econ_all':     (dict(tgt_a=2.0, tgt_b=2.0, ai_maint_stream=2, ai_renew_mode='zero', ai_fill_mode='zero'), ECO, sim5.OFFERS, 5),
    'econ_all_succ': (dict(tgt_a=2.0, tgt_b=2.0, ai_maint_stream=2, ai_renew_mode='zero', ai_fill_mode='zero', h=0.8, r_ratio=1.25), ECO, sim5.OFFERS, 5),
    'g1_3':         (dict(g1=3.0), ('balanced', 'prerest'), sim5.OFFERS, 0),
    'g1_0':         (dict(g1=0.0), ('balanced', 'prerest'), sim5.OFFERS, 0),
    'kappa_025':    (dict(kappa_s=0.025), ('balanced', 'prerest'), sim5.OFFERS, 0),
    'ks_08':        (dict(k_s=0.8), ('balanced', 'norest'), sim5.OFFERS, 0),
    'offer_34':     (dict(offer_idx=((2, 3), (4, 5), (6, 7))), ('greedy', 'prerest', 'balanced', 'star', 'ai'), sim5.OFFERS, 0),
    'offer_34_econ': (dict(offer_idx=((2, 3), (4, 5), (6, 7)), tgt_a=2.0, tgt_b=2.0, ai_maint_stream=2, ai_renew_mode='zero', ai_fill_mode='zero'), ('greedy', 'prerest', 'balanced', 'star', 'ai'), sim5.OFFERS, 0),
    'tmax_10':      (dict(t_max=0.10), ('balanced',), ('mid',), 5),
    'style_fix':    (dict(style_noreturn=True, label_star_lead=15.0), ('balanced',), ('mid',), 5),
    'scyc_030':     (dict(s_cyc=0.30), ('balanced',), ('mid',), 5),
    'cap_after':    (dict(cap_after=True, ai_fill_mode='zero'), ('balanced',), ('mid',), 5),
    'fixA':         (dict(sim5.FIXA), sim5.BOTS, sim5.OFFERS, 5),
    'fixB_share':   (dict(sim5.FIXA, window_share=200000.0), sim5.BOTS, sim5.OFFERS, 5),
    'fixB_smax':    (dict(sim5.FIXA, S_max=160000.0), sim5.BOTS, sim5.OFFERS, 5),
    'fixB_all':     (dict(sim5.FIXA, window_share=200000.0, ai_renew_age_test=True, k_th=2.0, style_noreturn=True,
                          s_cyc=0.25, t_max=0.05), sim5.BOTS, sim5.OFFERS, 5),
    'fixC':         (dict(sim5.FIXC), sim5.BOTS, sim5.OFFERS, 5),
    'fixD':         (dict(sim5.FIXD), sim5.BOTS, sim5.OFFERS, 5),
    'fixE':         (dict(sim5.FIXE), sim5.BOTS, sim5.OFFERS, 5),
}


def work(args):
    seed, name = args
    ov, bots, offers, ly = ABL[name]
    sim5.P.update(SAVED)
    try:
        return sim5.run_seed(seed, bots=bots, offers=offers, overrides=dict(ov), league_years=ly)
    except Exception:
        import traceback
        return dict(seed=seed, error=traceback.format_exc())


SAVED = dict(sim5.P)

if __name__ == '__main__':
    names = sys.argv[1].split(',')
    n = int(sys.argv[2]) if len(sys.argv) > 2 else 120
    os.makedirs('results5', exist_ok=True)
    with Pool(10) as pool:
        for name in names:
            t = time.time()
            res = pool.map(work, [(1000 + i, name) for i in range(n)], chunksize=1)
            errs = [r for r in res if 'error' in r]
            pickle.dump(res, open('results5/abl_%s.pkl' % name, 'wb'))
            print(name, n, 'seeds %.0fs' % (time.time() - t), 'errors', len(errs), flush=True)
            if errs:
                print(errs[0]['error'], flush=True)
