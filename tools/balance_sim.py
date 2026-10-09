"""Balance simulation for Balance.cs: every character (the player and each hired worker) has its own energy
meter; a free Sarı Kola refills all of them every 45 s. Greedy upgrade buying, plots worked smallest-first.

Usage: python3 tools/balance_sim.py
Keep the model in sync with Balance.cs by hand; it is a planning aid, real play runs ~25% slower."""
import random


class Meter:
    """Mirror of EnergyMeter.cs."""
    def __init__(s, mx, cost, full, delay, active, resume):
        s.mx, s.cost, s.full, s.delay, s.act, s.resume = mx, cost, full, delay, active, resume
        s.v, s.since, s.ex = mx, delay, False

    def can(s): return (not s.ex) and s.v >= s.cost

    def spend(s):
        s.v -= s.cost; s.since = 0.0
        if s.v < s.cost: s.ex = True

    def tick(s, dt):
        before = s.since; s.since += dt
        rest = max(0.0, s.since - max(before, s.delay))
        s.v = min(s.mx, s.v + s.full * rest + s.act * (dt - rest))
        if s.ex and s.v >= s.resume * s.mx: s.ex = False

    def fill(s): s.v, s.ex = s.mx, False


def sim(plans, hammer, worker, P, W, hit_rate=5.5, interval=1.1, cola_every=45.0, tmax=3600, seed=1):
    random.seed(seed)
    remaining = [f * w for f, w, b in plans]; bonus = [b for f, w, b in plans]
    pm = Meter(*P); workers = []          # each: [meter, hit timer]
    coins = 0.0; L = 0; t = 0.0; dt = 0.05; done = {}; buys = 0; cd = 0.0; next_cola = cola_every
    cost_h = lambda L: hammer[0] * hammer[1] ** L
    cost_w = lambda n: worker[0] * worker[1] ** n
    duty = 0.65   # rough share of time a worker is not resting, only used for the greedy buy heuristic
    nominal = lambda L, n: 3.0 * (1 + L) + n * duty * (1 + L // 2) / interval

    def hit(j, power):
        nonlocal coins
        use = min(power, remaining[j]); remaining[j] -= use; coins += use
        if remaining[j] <= 1e-9 and j not in done:
            done[j] = t; coins += bonus[j]

    while t < tmax and any(r > 0 for r in remaining):
        if t >= next_cola:
            pm.fill(); [w[0].fill() for w in workers]; next_cola += cola_every
        pm.tick(dt)
        for w in workers: w[0].tick(dt)

        cd -= dt
        unfinished = [j for j in range(len(plans)) if remaining[j] > 0]
        if cd <= 0 and pm.can() and unfinished:
            j = min(unfinished, key=lambda j: remaining[j])
            pm.spend(); hit(j, 1 + L); cd = 1.0 / hit_rate

        for w in workers:
            w[1] -= dt
            if w[1] > 0 or not w[0].can(): continue
            un = [j for j in range(len(plans)) if remaining[j] > 0]
            if not un: break
            j = random.choice(un)
            w[0].spend(); hit(j, 1 + L // 2); w[1] = interval

        opts = []
        if L < 25: opts.append(((nominal(L + 1, len(workers)) - nominal(L, len(workers))) / cost_h(L), 'H', cost_h(L)))
        if len(workers) < 8: opts.append(((nominal(L, len(workers) + 1) - nominal(L, len(workers))) / cost_w(len(workers)), 'W', cost_w(len(workers))))
        if opts:
            opts.sort(reverse=True); _, kind, c = opts[0]
            if coins >= c:
                coins -= c; buys += 1
                if kind == 'H': L += 1
                else: workers.append([Meter(*W), random.random() * interval])
        t += dt
    return t, L, len(workers), done, buys


BASE = [(2, 6, 15), (3, 25, 60), (5, 70, 220), (7, 170, 800), (10, 420, 3000)]
PLAYER = (100, 4, 20, 0.6, 0.0, 0.40)   # max, cost, full regen/s, rest delay, regen while acting, resume fraction


def scaled(k): return [(f, int(w * k), int(b * k * 0.5)) for f, w, b in BASE]


if __name__ == "__main__":
    print("worker energy candidates (max, cost, full regen, delay, active regen, resume) x work scale k:")
    for k in (3, 4):
        for W in [(100, 6, 12, 1.0, 0, 0.4), (100, 8, 12, 1.0, 0, 0.4), (100, 10, 15, 1.0, 0, 0.5), (100, 12, 15, 1.0, 0, 0.5)]:
            res = [sim(scaled(k), (20, 1.5, 25), (90, 1.75, 8), PLAYER, W, seed=s) for s in (1, 2, 3)]
            ts = [r[0] / 60 for r in res]
            first = min(res[0][3].values()) / 60
            hits_per_burst = int(W[0] // W[1])
            print(f" k={k} worker={W}: {sum(ts) / 3:5.1f} min, first building {first:.1f} min, hammer L{res[0][1]}, workers {res[0][2]}, "
                  f"buys {res[0][4]}, burst {hits_per_burst} hits")
