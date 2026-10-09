"""Balance simulation for Balance.cs (energy meters, upgrades, workers, free cola every 45 s).
Usage: python3 tools/balance_sim.py   -- prints full-run times; copy the numbers into Balance.cs when they look right.
Keep the model in sync with Balance.cs by hand; it is a planning aid, real play is ~25% slower."""
import random
class Meter:
    def __init__(s,mx,cost,full_regen,rest_delay,active_regen,resume):
        s.mx=mx; s.v=mx; s.cost=cost; s.full=full_regen; s.delay=rest_delay; s.act=active_regen; s.resume=resume
        s.idle=99.0; s.ex=False
    def can(s): return (not s.ex) and s.v>=s.cost
    def spend(s):
        s.v-=s.cost; s.idle=0
        if s.v<s.cost: s.ex=True
    def tick(s,dt):
        s.idle+=dt
        r = s.full if s.idle>=s.delay else s.act
        s.v=min(s.mx,s.v+r*dt)
        if s.ex and s.v>=s.resume*s.mx: s.ex=False
    def fill(s): s.v=s.mx; s.ex=False

def sim(plans, hammer, worker, P, S, hit_rate=5.5, interval=1.1, cola_every=45.0, tmax=3600, seed=1):
    random.seed(seed)
    remaining=[f*w for f,w,b in plans]; bonus=[b for f,w,b in plans]
    n_plots=len(plans)
    pm=Meter(*P); sm=[Meter(*S) for _ in plans]
    coins=0.0; L=0; nw=0; t=0.0; dt=0.05
    worker_site=[]   # plot index per worker
    done_t={}; nbuys=0; cd=0.0; wtimers=[]; next_cola=cola_every
    cost_h=lambda L: hammer[0]*hammer[1]**L
    cost_w=lambda n: worker[0]*worker[1]**n
    def nominal(L,n): return 3.0*(1+L)+n*(1+L//2)/interval   # rough income for greedy buying
    while t<tmax and any(r>0 for r in remaining):
        if t>=next_cola: pm.fill(); [m.fill() for m in sm]; next_cola+=cola_every
        pm.tick(dt); [m.tick(dt) for m in sm]
        def hit(j,power):
            nonlocal coins
            use=min(power,remaining[j]); remaining[j]-=use; coins+=use
            if remaining[j]<=1e-9 and j not in done_t:
                done_t[j]=t; coins+=bonus[j]
        # player: best plot = unfinished, can(), with most energy
        cd-=dt
        if cd<=0 and pm.can():
            cands=[j for j in range(n_plots) if remaining[j]>0 and sm[j].can()]
            if cands:
                j=max(cands,key=lambda j:sm[j].v)
                pm.spend(); sm[j].spend(); hit(j,1+L); cd=1.0/hit_rate
        # workers
        for i in range(len(worker_site)):
            wtimers[i]-=dt
            if wtimers[i]>0: continue
            j=worker_site[i]
            if remaining[j]<=0:
                un=[k for k in range(n_plots) if remaining[k]>0]
                if not un: continue
                cnt=lambda k: sum(1 for x in worker_site if x==k)
                j=min(un,key=lambda k:(cnt(k),k)); worker_site[i]=j
            if sm[j].can():
                sm[j].spend(); hit(j,1+L//2); wtimers[i]=interval
            else: wtimers[i]=0.1
        # buying
        opts=[]
        if L<25: opts.append(((nominal(L+1,nw)-nominal(L,nw))/cost_h(L),'H',cost_h(L)))
        if nw<8: opts.append(((nominal(L,nw+1)-nominal(L,nw))/cost_w(nw),'W',cost_w(nw)))
        if opts:
            opts.sort(reverse=True); r,k,c=opts[0]
            if coins>=c:
                coins-=c; nbuys+=1
                if k=='H': L+=1
                else:
                    nw+=1; un=[k2 for k2 in range(n_plots) if remaining[k2]>0]
                    worker_site.append(random.choice(un) if un else 0); wtimers.append(random.random()*interval)
        t+=dt
    return t,L,nw,done_t,nbuys

base=[(2,6,15),(3,25,60),(5,70,220),(7,170,800),(10,420,3000)]
P=(100,4,20,0.6,0.0,0.40)     # player: max,cost,full regen,rest delay,regen while acting,resume fraction
S=(100,3,15,1.0,3.0,0.30)     # site
import sys
print("\nspeed-up candidates (with energy, free cola every 45s):")
for k,(hb,hg),(wb,wg) in [(3,(20,1.5),(90,1.75)),(3,(25,1.55),(120,1.8)),(3.5,(20,1.5),(90,1.75)),(4,(20,1.5),(90,1.75))]:
    plans=[(f,int(w*k),int(b*k*0.5)) for f,w,b in base]
    res=[sim(plans,(hb,hg,25),(wb,wg,8),P,S,seed=s) for s in (1,2,3)]
    ts=[r[0]/60 for r in res]; 
    first=[round(min(r[3].values())/60,1) for r in res]
    print(f" k={k} hammer=({hb},{hg}) worker=({wb},{wg}): {sum(ts)/3:.1f} min (runs {', '.join('%.1f'%x for x in ts)}), first building at ~{first} min, hammer L{res[0][1]}, workers {res[0][2]}, purchases {res[0][4]}")
