import sys, pickle, time, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from multiprocessing import Pool
import sim5

CONFIGS = {
    'base': {},
}

def work(args):
    seed, name, ov = args
    ov = dict(ov); bots = ov.pop('_bots', sim5.BOTS); offers = ov.pop('_offers', sim5.OFFERS)
    ly = ov.pop('_league_years', 5)
    try:
        return sim5.run_seed(seed, bots=bots, offers=offers, overrides=ov, league_years=ly)
    except Exception as e:
        import traceback
        return dict(seed=seed, error=traceback.format_exc())

if __name__ == '__main__':
    name = sys.argv[1]; n = int(sys.argv[2]); s0 = int(sys.argv[3]) if len(sys.argv) > 3 else 1000
    ov = CONFIGS[name] if name in CONFIGS else eval(sys.argv[4])
    t = time.time()
    with Pool(10) as pool:
        res = pool.map(work, [(s0 + i, name, ov) for i in range(n)], chunksize=1)
    errs = [r for r in res if 'error' in r]
    os.makedirs('results5', exist_ok=True)
    pickle.dump(res, open('results5/%s.pkl' % name, 'wb'))
    print(name, n, 'seeds done in %.1fs' % (time.time() - t), 'errors', len(errs))
    if errs: print(errs[0]['error'])
