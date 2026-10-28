import math, random, itertools
L=['top','jg','mid','adc','sup']
Lw={'E':dict(top=1,jg=1.5,mid=1.2,adc=1,sup=1),'M':dict(top=1.2,jg=1,mid=1,adc=1,sup=1.3),'L':dict(top=1,jg=1,mid=1.2,adc=1.6,sup=1)}
FOCUS={'inv':'E','tf':'M','dev':'L'}; T=list(FOCUS)
beats={('inv','dev'),('dev','tf'),('tf','inv')}
def mk(**kw):
    P=dict(lamE=.15,lamM=.5,lamL=.15,cM=.85,e=(.3,.4,.3),eta=.3,s=.15,wmin=.6,a=.1,F=1.0,C=.5,k=8,pf=.15,starcap=15)
    P.update(kw); return P
def S(t,ph,P):
    r=t[ph]; w=[Lw[ph][l] for l in L]
    ws=sum(a*b for a,b in zip(r,w))/sum(w); lam=P['lam'+ph]; s=ws-lam*(ws-min(r))
    if ph=='M': s+=P.get('chi',1.0)*(t['chem']-50)/10
    return s
def ex(t,P): e=P['e']; return e[0]+e[1]*t['chem']/100+e[2]*t['cmd']/100
def star(a,b,ph,P):
    lead=[x-y for x,y in zip(a[ph],b[ph])]; m=sum(lead)/5
    return min(P['starcap'],max(0,max(lead)-max(0,m)-P.get('st0',0)))
# cycle: winner of phase X suppresses phase Y weight when leaders differ: E>L, M>E, L>M
CYC=[('E','L'),('M','E'),('L','M')]
def G(a,b,ta,tb,P,cycle=True):
    d={ph:S(a,ph,P)-S(b,ph,P) for ph in 'EML'}
    for ph in 'EL': d[ph]+=P['eta']*(star(a,b,ph,P)-star(b,a,ph,P))
    f={'E':1,'M':1,'L':1}
    if cycle:
        for x,y in CYC:
            if d[x]*d[y]<0: f[y]=min(f[y],max(P['wmin'],1-P['s']*abs(d[x])/10))
    else:
        if d['E']*d['L']<0: f['L']=max(P['wmin'],1-P['s']*abs(d['E'])/10)
    B=P.get('B',{'E':1,'M':1,'L':1}); base=sum(B[p]*f[p]*d[p] for p in 'EML')/sum(B.values())
    xa,xb=ex(a,P),ex(b,P)
    fa=max(-P['F'],min(P['F'],P['a']*xa*d[FOCUS[ta]])); fb=max(-P['F'],min(P['F'],P['a']*xb*(-d[FOCUS[tb]])))
    cn=(P['C']*xa if (ta,tb) in beats else 0)-(P['C']*xb if (tb,ta) in beats else 0)
    return base+fa-fb+cn
def p(g,P): return max(P['pf'],min(1-P['pf'],1/(1+math.exp(-g/P['k']))))
def team(rnd,style,bias,mean=60):
    t={}
    for ph in 'EML':
        off=0 if style is None else (2*bias/3 if ph==style else -bias/3)
        t[ph]=[mean+off+rnd.gauss(0,4) for _ in L]
    t['chem']=70;t['cmd']=70; return t
def eq(a,b,P,cycle):
    M=[[p(G(a,b,x,y,P,cycle),P) for y in T] for x in T]
    va=max(min(r) for r in M); vb=min(max(M[i][j] for i in range(3)) for j in range(3)); return (va+vb)/2
def pairs(P,cycle,bias=9,n=1500,seed=1):
    rnd=random.Random(seed); out={}
    for s1,s2 in [('E','L'),('M','E'),('L','M'),('E',None),('M',None),('L',None)]:
        v=[eq(team(rnd,s1,bias),team(rnd,s2,bias),P,cycle) for _ in range(n)]
        out[f"{s1}v{s2 or 'bal'}"]=round(sum(v)/n,3)
    return out
