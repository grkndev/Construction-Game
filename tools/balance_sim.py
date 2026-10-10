"""Balance simulation for Balance.cs (five districts of six buildings, upgrades restart in each): every character (the player and each hired worker) has its own energy
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
SPEED_PER_LEVEL = 0.15
COINS_PER_WORK = 0.6   # Balance.CoinsPerWork: coins earned per point of work (and on the completion bonus)
STAMINA_MAX, STAMINA_REGEN = 0.20, 0.15


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


def sim(plans, hammer, worker, stamina, P, W, speed=None, hit_rate=5.5, interval=0.8, cola_every=45.0, tmax=3600, seed=1, scale=1, coins0=0.0):
    random.seed(seed)
    remaining = [f * w for f, w, b in plans]; bonus = [b for f, w, b in plans]
    pm = Meter(*P); workers = [[Meter(*W), 0.0]]   # each: [meter, hit timer]; every district starts with one free helper
    SP = 0; pace = 1.0; coins = coins0; L = 0; S = 0; t = 0.0; dt = 0.05; done = {}; buys = 0; cd = 0.0; next_cola = cola_every; buff_until = 0.0
    cost_h = lambda L: hammer[0] * hammer[1] ** L
    cost_w = lambda n: worker[0] * worker[1] ** n
    cost_s = lambda S: stamina[0] * stamina[1] ** S
    duty = duty_table(P, W)
    # rough income used only by the greedy buy heuristic: player hits per second is capped by their duty cycle
    cost_p = lambda SP: speed[0] * speed[1] ** SP
    pace_of = lambda SP: 1.0 + SPEED_PER_LEVEL * SP
    nominal = lambda L, n, S, SP=0: pace_of(SP) * (min(hit_rate, duty[S][0] * 20 / 1.0) * (1 + L) * scale + n * min(1 / interval, duty[S][1] * 20) * (1 + L // 2) * scale)

    def hit(j, power):
        nonlocal coins
        use = min(power, remaining[j]); remaining[j] -= use; coins += use * COINS_PER_WORK
        if remaining[j] <= 1e-9 and j not in done:
            done[j] = t; coins += bonus[j] * COINS_PER_WORK

    while t < tmax and any(r > 0 for r in remaining):
        if t >= next_cola:
            pm.fill(); [w[0].fill() for w in workers]; next_cola += cola_every; buff_until = t + COLA_LEVEL_SECONDS
        eff = L + (LEVEL_BONUS if t < buff_until else 0)
        pm.tick(dt * pace)
        for w in workers: w[0].tick(dt * pace)

        cd -= dt * pace
        unfinished = [j for j in range(len(plans)) if remaining[j] > 0]
        if cd <= 0 and pm.can() and unfinished:
            j = min(unfinished, key=lambda j: remaining[j])
            pm.spend(); hit(j, (1 + eff) * scale); cd = 1.0 / hit_rate

        for w in workers:
            w[1] -= dt * pace
            if w[1] > 0 or not w[0].can(): continue
            un = [j for j in range(len(plans)) if remaining[j] > 0]
            if not un: break
            j = random.choice(un)
            w[0].spend(); hit(j, (1 + eff // 2) * scale); w[1] = interval

        opts = []
        base_now = nominal(L, len(workers), S, SP)
        if L < 24: opts.append(((nominal(L + 1, len(workers), S, SP) - base_now) / cost_h(L), 'H', cost_h(L)))
        if len(workers) < 8: opts.append(((nominal(L, len(workers) + 1, S, SP) - base_now) / cost_w(len(workers)), 'W', cost_w(len(workers))))
        if S < 9: opts.append(((nominal(L, len(workers), S + 1, SP) - base_now) / cost_s(S), 'S', cost_s(S)))
        if speed is not None and SP < 4: opts.append(((nominal(L, len(workers), S, SP + 1) - base_now) / cost_p(SP), 'P', cost_p(SP)))
        if opts:
            opts.sort(reverse=True); _, kind, c = opts[0]
            if coins >= c:
                coins -= c; buys += 1
                if kind == 'H': L += 1
                elif kind == 'P': SP += 1; pace = pace_of(SP)
                elif kind == 'W':
                    m = Meter(*W); m.rescale(W[0] * (1 + STAMINA_MAX * S), W[2] * (1 + STAMINA_REGEN * S))
                    workers.append([m, random.random() * interval])
                else:
                    S += 1
                    pm.rescale(P[0] * (1 + STAMINA_MAX * S), P[2] * (1 + STAMINA_REGEN * S))
                    for w in workers: w[0].rescale(W[0] * (1 + STAMINA_MAX * S), W[2] * (1 + STAMINA_REGEN * S))
        t += dt
    return t, L, len(workers), done, buys, S, coins, SP


PLAYER = (100, 4, 20, 0.6, 0.0, 0.40)   # max, cost, full regen/s, rest delay, regen while acting, resume fraction
WORKER = (100, 8, 12, 1.0, 0, 0.4)

# The districts are read straight from Balance.cs (first-district numbers: the game scales work/bonus/prices/power by `scale`).
def load_districts():
    import os, re
    src = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets/_Project/Scripts/Runtime/Core/Balance.cs"), encoding="utf-8").read()
    out = []
    for m in re.finditer(r'new DistrictDef\("([^"]+)",\s*requiredLevel:\s*\d+,\s*scale:\s*(\d+),(.*?)\)\),', src, re.S):
        plans = [(int(f), int(w), int(b)) for f, w, b in re.findall(r'new BuildPlan\("[^"]+",\s*(\d+),\s*(\d+),\s*(\d+)', m.group(3))]
        out.append((m.group(1), int(m.group(2)), plans))
    return out


DISTRICTS = load_districts()
HIT_RATE = 1 / 0.35   # PlayerHammer attacks as soon as the cooldown allows (Balance.HitCooldown); energy is the real limit


def run_city(seeds=(1, 2, 3)):
    """Plays the districts in order (upgrades restart in each, coins carry over) and prints minutes per district."""
    totals = [0.0] * len(DISTRICTS)
    details = []
    for sd in seeds:
        for i, (name, scale, shape) in enumerate(DISTRICTS):
            coins = 0.0   # coins are per district
            plans = [(f, w * scale, b * scale) for f, w, b in shape]
            r = sim(plans, (20 * scale, 1.5, 25), (90 * scale, 1.75, 8), (60 * scale, 1.6, 10), PLAYER, WORKER, speed=(100 * scale, 2.0),
                    hit_rate=HIT_RATE, seed=sd, scale=scale, coins0=coins)
            totals[i] += r[0]
            if sd == seeds[0]:
                details.append((name, r[1], r[2], r[5], r[7], [round(v / 60, 1) for _, v in sorted(r[3].items())]))
    return [t / len(seeds) for t in totals], details


if __name__ == "__main__":
    mins, details = run_city()
    print("district: minutes (attentive player model; real play is ~25% slower) | hammer lvl, workers, stamina lvl, building finish minutes")
    for (name, h, w, st, sp, plots), m in zip(details, mins):
        print(f"  {name:7s} {m / 60:5.1f} min | hammer {h:2d}, workers {w}, stamina {st:2d}, speed {sp}, plots {plots}")
    print(f"  total   {sum(mins) / 60:5.1f} min (x1.25 real play = {sum(mins) / 60 * 1.25:4.1f})")
