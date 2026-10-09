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

    def rescale(s, mx, full):
        s.v = s.v / s.mx * mx; s.mx = mx; s.full = full


LEVEL_BONUS, COLA_LEVEL_SECONDS = 5, 10.0
STAMINA_MAX, STAMINA_REGEN = 0.15, 0.10


def duty_table(P, W):
    """Measured share of time a character that always wants to hit actually can, per stamina level."""
    out = {}
    for S in range(0, 11):
        res = []
        for base in (P, W):
            m = Meter(*base); m.rescale(base[0] * (1 + STAMINA_MAX * S), base[2] * (1 + STAMINA_REGEN * S))
            hits = 0; t = 0.0
            while t < 120:
                m.tick(0.05)
                if m.can(): m.spend(); hits += 1
                t += 0.05
            res.append(hits * 0.05 / 120 * 1.0)   # hits per second per 20 Hz attempt budget
        out[S] = res
    return out


def sim(plans, hammer, worker, stamina, P, W, hit_rate=5.5, interval=1.1, cola_every=45.0, tmax=3600, seed=1):
    random.seed(seed)
    remaining = [f * w for f, w, b in plans]; bonus = [b for f, w, b in plans]
    pm = Meter(*P); workers = []          # each: [meter, hit timer]
    coins = 0.0; L = 0; S = 0; t = 0.0; dt = 0.05; done = {}; buys = 0; cd = 0.0; next_cola = cola_every; buff_until = 0.0
    cost_h = lambda L: hammer[0] * hammer[1] ** L
    cost_w = lambda n: worker[0] * worker[1] ** n
    cost_s = lambda S: stamina[0] * stamina[1] ** S
    duty = duty_table(P, W)
    # rough income used only by the greedy buy heuristic: player hits per second is capped by their duty cycle
    nominal = lambda L, n, S: min(hit_rate, duty[S][0] * 20 / 1.0) * (1 + L) + n * min(1 / interval, duty[S][1] * 20) * (1 + L // 2)

    def hit(j, power):
        nonlocal coins
        use = min(power, remaining[j]); remaining[j] -= use; coins += use
        if remaining[j] <= 1e-9 and j not in done:
            done[j] = t; coins += bonus[j]

    while t < tmax and any(r > 0 for r in remaining):
        if t >= next_cola:
            pm.fill(); [w[0].fill() for w in workers]; next_cola += cola_every; buff_until = t + COLA_LEVEL_SECONDS
        eff = L + (LEVEL_BONUS if t < buff_until else 0)
        pm.tick(dt)
        for w in workers: w[0].tick(dt)

        cd -= dt
        unfinished = [j for j in range(len(plans)) if remaining[j] > 0]
        if cd <= 0 and pm.can() and unfinished:
            j = min(unfinished, key=lambda j: remaining[j])
            pm.spend(); hit(j, 1 + eff); cd = 1.0 / hit_rate

        for w in workers:
            w[1] -= dt
            if w[1] > 0 or not w[0].can(): continue
            un = [j for j in range(len(plans)) if remaining[j] > 0]
            if not un: break
            j = random.choice(un)
            w[0].spend(); hit(j, 1 + eff // 2); w[1] = interval

        opts = []
        base_now = nominal(L, len(workers), S)
        if L < 25: opts.append(((nominal(L + 1, len(workers), S) - base_now) / cost_h(L), 'H', cost_h(L)))
        if len(workers) < 8: opts.append(((nominal(L, len(workers) + 1, S) - base_now) / cost_w(len(workers)), 'W', cost_w(len(workers))))
        if S < 10: opts.append(((nominal(L, len(workers), S + 1) - base_now) / cost_s(S), 'S', cost_s(S)))
        if opts:
            opts.sort(reverse=True); _, kind, c = opts[0]
            if coins >= c:
                coins -= c; buys += 1
                if kind == 'H': L += 1
                elif kind == 'W':
                    m = Meter(*W); m.rescale(W[0] * (1 + STAMINA_MAX * S), W[2] * (1 + STAMINA_REGEN * S))
                    workers.append([m, random.random() * interval])
                else:
                    S += 1
                    pm.rescale(P[0] * (1 + STAMINA_MAX * S), P[2] * (1 + STAMINA_REGEN * S))
                    for w in workers: w[0].rescale(W[0] * (1 + STAMINA_MAX * S), W[2] * (1 + STAMINA_REGEN * S))
        t += dt
    return t, L, len(workers), done, buys, S


BASE = [(2, 6, 15), (3, 25, 60), (5, 70, 220), (7, 170, 800), (10, 420, 3000)]
PLAYER = (100, 4, 20, 0.6, 0.0, 0.40)   # max, cost, full regen/s, rest delay, regen while acting, resume fraction


def scaled(k): return [(f, int(w * k), int(b * k * 0.5)) for f, w, b in BASE]


WORKER = (100, 8, 12, 1.0, 0, 0.4)

if __name__ == "__main__":
    print("k, stamina cost -> full-run minutes (mean of 3 seeds), upgrades reached, building finish times")
    for k in (4, 5, 6):
        for sb, sg in ((40, 1.6), (60, 1.6), (90, 1.6)):
            res = [sim(scaled(k), (20, 1.5, 25), (90, 1.75, 8), (sb, sg, 10), PLAYER, WORKER, seed=sd) for sd in (1, 2, 3)]
            r = res[0]
            print(f" k={k} stamina=({sb},{sg}): {sum(x[0] for x in res) / 180:5.1f} min, hammer L{r[1]}, workers {r[2]}, stamina {r[5]}, "
                  f"buys {r[4]}, plots {[round(v / 60, 1) for _, v in sorted(r[3].items())]}")
