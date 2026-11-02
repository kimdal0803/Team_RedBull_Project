"""Which frame shows when — shared by the GIF previews and (mirrored by hand) the Unity defaults.

Key = (event, offset_seconds, frame, flags)
  event: 'start' (telegraph begins) | 'attack' (telegraph ends) | 'recovery' (attack ends)
         'dashStart' | 'dashEnd' | ('hit', i)
  'eachHit' entries expand to one strike/after pair per hit, so DDA adding hits still animates.
flags: 'punch' (stretch pop), 'shake' (screen shake: 1 light, 2 heavy)
"""

# pattern timings copied from Assets/Data/Boss/Arakus/*.asset (speed x1, no DDA)
PATTERNS = {
    'Arakus_NormalAttack_R_PT':  dict(id='Arakus_NormalAttack_R', tele=1.05, hits=[0.0], att=0.0, rec=1.05),
    'Arakus_NormalAttack_L_PT':  dict(id='Arakus_NormalAttack_L', tele=0.85, hits=[0.0], att=0.0, rec=1.05),
    'Arakus_NormalAttack_F_PT':  dict(id='Arakus_NormalAttack_F', tele=0.80, hits=[0.0], att=0.0, rec=0.40),
    'Arakus_Sword_Swinging_PT':  dict(id='Arakus_SwordSwinging', tele=0.85, hits=[0.0], att=0.0, rec=0.75),
    'Arakus_QuickStep_PT':       dict(id='Arakus_QuickStep', tele=0.10, hits=[], att=0.0, rec=0.35, dash=(0.0, 0.3, -0.75)),
    'Arakus_ShieldThrowing_DashAttack_PT': dict(id='Arakus_ShieldThrowDash', tele=0.60, hits=[0.0, 0.8], att=0.8, rec=0.5, dash=(0.2, 0.6, 6.0)),
    'Arakus_ShieldDash_PT':      dict(id='Arakus_ShieldDash', tele=0.50, hits=[0.25 * i for i in range(8)], att=0.0, rec=0.35, dash=(0.0, 1.75, 5.0)),
    'Arakus_Shield_Throwing_Fly_Attack_PT': dict(id='Arakus_ShieldThrowSlam', tele=0.60, hits=[0.0, 0.75], att=0.75, rec=0.45, dash=(0.15, 0.5, 5.0)),
    'Arakus_Aggressive_Attack_PT': dict(id='Arakus_SweepingStrike', tele=1.00, hits=[0.0, 1.0, 2.0], att=0.0, rec=0.80),
    'Arakus_Of_Wrath_PT':        dict(id='Arakus_Rage', tele=2.50, hits=[], att=0.0, rec=2.50),
    'Arakus_Charge_PT':          dict(id='Arakus_Charge', tele=1.10, hits=[0.0], att=0.0, rec=0.0, dash=(0.0, 0.4, 20.0)),
}

EACH = 'eachHit'  # (EACH, strikeOffset, strikeFrame, afterOffset, afterFrame, lastAfterFrame, shake)

TIMELINES = {
    'Arakus_NormalAttack_R_PT': [('start', 0, 0, ''), (EACH, -0.05, 1, 0.22, 2, 2, 1)],
    'Arakus_NormalAttack_L_PT': [('start', 0, 0, ''), (EACH, -0.05, 1, 0.25, 2, 2, 2)],
    'Arakus_NormalAttack_F_PT': [('start', 0, 0, ''), (EACH, -0.05, 1, 0.20, 2, 2, 1)],
    'Arakus_Sword_Swinging_PT': [('start', 0, 0, ''), (EACH, -0.05, 1, 0.30, 2, 2, 2)],
    'Arakus_QuickStep_PT': [('start', 0, 0, ''), ('attack', 0, 1, 'punch'), ('recovery', 0, 2, 'punch')],
    'Arakus_ShieldThrowing_DashAttack_PT': [('start', 0, 0, ''), ('attack', 0, 1, 'punch'),
                                            ('recovery', -0.05, 1, 'punch shake1'), ('recovery', 0.15, 2, '')],
    'Arakus_ShieldDash_PT': [('start', 0, 0, ''), ('attack', 0, 1, 'punch'), ('recovery', 0, 2, 'punch')],
    'Arakus_Shield_Throwing_Fly_Attack_PT': [('start', 0, 0, ''), ('attack', 0, 1, 'punch'), ('dashStart', 0, 2, ''),
                                             ('recovery', -0.12, 3, ''), ('recovery', 0, 4, 'punch shake2')],
    'Arakus_Aggressive_Attack_PT': [('start', 0, 0, ''), (EACH, -0.05, 1, 0.25, 2, 3, 1)],
    'Arakus_Of_Wrath_PT': [('start', 0, 0, ''), ('attack', 0, 1, 'punch shake2'), ('attack', 0.45, 2, '')],
    'Arakus_Charge_PT': [('start', 0, 0, ''), ('attack', 0, 1, 'punch shake1'), ('recovery', 0, 2, '')],
}

# visual jump arcs (world units): sheet -> (from event, to event, height)
ARCS = {
    'Arakus_QuickStep_PT': ('attack', 'recovery', 0.35),
    'Arakus_Shield_Throwing_Fly_Attack_PT': ('dashStart', 'recovery', 1.8),
}

TREMBLE_BEFORE_ATTACK = 0.2   # seconds of 1px shiver at the end of a telegraph (only if telegraph >= 0.45s)


def event_times(p):
    tele = p['tele']
    hits = p['hits']
    last_end = max(hits) if hits else 0.0
    dash = p.get('dash')
    att = max(p['att'], last_end, (dash[0] + dash[1]) if dash else 0.0)
    ev = {'start': 0.0, 'attack': tele, 'recovery': tele + att, 'end': tele + att + p['rec']}
    if dash:
        ev['dashStart'] = tele + dash[0]
        ev['dashEnd'] = tele + dash[0] + dash[1]
    for i, h in enumerate(hits):
        ev[('hit', i)] = tele + h
    return ev


def expand(sheet):
    """-> sorted list of (time, frame, flags)"""
    p = PATTERNS[sheet]
    ev = event_times(p)
    keys = []
    for k in TIMELINES[sheet]:
        if k[0] == EACH:
            _, so, sf, ao, af, laf, shake = k
            n = len(p['hits'])
            for i in range(n):
                t = ev[('hit', i)]
                keys.append((t + so, sf, 'punch' + (f' shake{shake}' if shake else '')))
                keys.append((t + ao, laf if i == n - 1 else af, ''))
        else:
            e, off, fr, flags = k
            if e not in ev:
                continue
            keys.append((ev[e] + off, fr, flags))
    keys.sort(key=lambda x: x[0])
    return keys, ev
